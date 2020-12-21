using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;
using BasicCancel = RabbitMQ.Client.Framing.Impl.BasicCancel;
using BasicCancelOk = RabbitMQ.Client.Framing.Impl.BasicCancelOk;
using BasicConsumeOk = RabbitMQ.Client.Framing.Impl.BasicConsumeOk;
using BasicDeliver = RabbitMQ.Client.Framing.Impl.BasicDeliver;

namespace RabbitMQ.Client.client.impl.Channel
{
    #nullable enable
    /// <summary>
    /// A channel of a <see cref="IConnection"/>.
    /// </summary>
    public class Channel : ChannelBase, IChannel
    {
        internal static readonly BasicProperties EmptyBasicProperties = new Framing.BasicProperties();

        private readonly Dictionary<string, IBasicConsumer> _consumers = new Dictionary<string, IBasicConsumer>();

        private ulong _nextPublishTag;

        private bool IsPublishAcksEnabled => _nextPublishTag != ulong.MaxValue;

        // only exist due to tests
        internal IConsumerDispatcher ConsumerDispatcher { get; }

        internal Channel(ISession session, ConsumerWorkService workService)
            : base(session)
        {
            _nextPublishTag = ulong.MaxValue;
            if (workService is AsyncConsumerWorkService asyncConsumerWorkService)
            {
                ConsumerDispatcher = new AsyncConsumerDispatcher(this, asyncConsumerWorkService);
            }
            else
            {
                ConsumerDispatcher = new ConcurrentConsumerDispatcher(this, workService);
            }
        }

        //**************************** Various methods ****************************
        internal ValueTask OpenChannelAsync()
        {
            ModelRpc<ChannelOpenOk>(new ChannelOpen(string.Empty));
            return default;
        }

        /// <inheritdoc />
        public ValueTask SetQosAsync(uint prefetchSize, ushort prefetchCount, bool global)
        {
            ModelRpc<BasicQosOk>(new BasicQos(prefetchSize, prefetchCount, global));
            return default;
        }

        // TODO 8.0 - Missing Tests
        /// <inheritdoc />
        public event Action<bool>? FlowControlChanged;

        private void HandleChannelFlow(bool active)
        {
            if (active)
            {
                _flowControlBlock.Set();
            }
            else
            {
                _flowControlBlock.Reset();
            }
            ModelSend(new ChannelFlowOk(active));

            var handler = FlowControlChanged;
            if (handler != null)
            {
                foreach (Action<bool> action in handler.GetInvocationList())
                {
                    try
                    {
                        action(active);
                    }
                    catch (Exception e)
                    {
                        OnUnhandledExceptionOccurred(e, "OnFlowControl");
                    }
                }
            }
        }

        //**************************** Message retrieval methods ****************************
        /// <inheritdoc />
        public virtual ValueTask AckMessageAsync(ulong deliveryTag, bool multiple)
        {
            ModelSend(new BasicAck(deliveryTag, multiple));
            return default;
        }

        /// <inheritdoc />
        public virtual ValueTask NackMessageAsync(ulong deliveryTag, bool multiple, bool requeue)
        {
            ModelSend(new BasicNack(deliveryTag, multiple, requeue));
            return default;
        }

        /// <inheritdoc />
        public ValueTask<string> ActivateConsumerAsync(IBasicConsumer consumer, string queue, bool autoAck, string consumerTag = "", bool noLocal = false, bool exclusive = false, IDictionary<string, object>? arguments = null)
        {
            // TODO: Replace with flag
            if (ConsumerDispatcher is AsyncConsumerDispatcher)
            {
                if (!(consumer is IAsyncBasicConsumer))
                {
                    // TODO: Friendly message
                    throw new InvalidOperationException("In the async mode you have to use an async consumer");
                }
            }

            // TODO 8.0 - Call ModelRpc
            var k = new ModelBase.BasicConsumerRpcContinuation { m_consumer = consumer };

            lock (_rpcLock)
            {
                Enqueue(k);
                // Non-nowait. We have an unconventional means of getting the RPC response, but a response is still expected.
                ModelSend(new BasicConsume(default, queue, consumerTag, noLocal, autoAck, exclusive, false, arguments));
                k.GetReply(ContinuationTimeout);
            }

            return new ValueTask<string>(k.m_consumerTag);
        }

        private void HandleBasicConsumeOk(string consumerTag)
        {
            var k = (ModelBase.BasicConsumerRpcContinuation)GetRpcContinuation();
            k.m_consumerTag = consumerTag;
            lock (_consumers)
            {
                _consumers[consumerTag] = k.m_consumer;
            }
            ConsumerDispatcher.HandleBasicConsumeOk(k.m_consumer, consumerTag);
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        /// <inheritdoc />
        public ValueTask CancelConsumerAsync(string consumerTag, bool waitForConfirmation = true)
        {
            if (waitForConfirmation)
            {
                // TODO 8.0 - Call ModelRpc
                var k = new ModelBase.BasicConsumerRpcContinuation { m_consumerTag = consumerTag };

                lock (_rpcLock)
                {
                    Enqueue(k);
                    ModelSend(new BasicCancel(consumerTag, false));
                    k.GetReply(ContinuationTimeout);
                }
            }
            else
            {
                ModelSend(new BasicCancel(consumerTag, true));

                lock (_consumers)
                {
                    _consumers.Remove(consumerTag);
                }
                // TODO 8.0 - ConsumerDispatcher on no wait - Check if dispatcher shall be called
            }

            return default;
        }

        private void HandleBasicCancelOk(string consumerTag)
        {
            var k = (ModelBase.BasicConsumerRpcContinuation)GetRpcContinuation();
            lock (_consumers)
            {
                k.m_consumer = _consumers[consumerTag];
                _consumers.Remove(consumerTag);
            }
            ConsumerDispatcher.HandleBasicCancelOk(k.m_consumer, consumerTag);
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        private void HandleBasicCancel(string consumerTag)
        {
            IBasicConsumer consumer;
            lock (_consumers)
            {
                consumer = _consumers[consumerTag];
                _consumers.Remove(consumerTag);
            }
            ConsumerDispatcher.HandleBasicCancel(consumer, consumerTag);
        }

        protected virtual void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties basicProperties, ReadOnlyMemory<byte> body, byte[] rentedArray)
        {
            IBasicConsumer consumer;
            lock (_consumers)
            {
                consumer = _consumers[consumerTag];
            }

            ConsumerDispatcher.HandleBasicDeliver(consumer, consumerTag, deliveryTag, redelivered, exchange, routingKey, basicProperties, body, rentedArray);
        }

        /// <inheritdoc />
        public ValueTask<SingleMessageRetrieval> RetrieveSingleMessageAsync(string queue, bool autoAck)
        {
            // TODO 8.0 - Call ModelRpc
            var k = new ModelBase.BasicGetRpcContinuation();
            lock (_rpcLock)
            {
                Enqueue(k);
                ModelSend(new BasicGet(default, queue, autoAck));
                k.GetReply(ContinuationTimeout);
            }

            return new ValueTask<SingleMessageRetrieval>(k.m_result);
        }

        protected virtual void HandleBasicGetOk(ulong deliveryTag, bool redelivered, string exchange, string routingKey, uint messageCount, IBasicProperties basicProperties, ReadOnlyMemory<byte> body, byte[] rentedArray)
        {
            var k = (ModelBase.BasicGetRpcContinuation)GetRpcContinuation();
            k.m_result = new SingleMessageRetrieval(rentedArray, basicProperties, body, exchange, routingKey, deliveryTag, messageCount, redelivered);
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        private void HandleBasicGetEmpty()
        {
            var k = (ModelBase.BasicGetRpcContinuation)GetRpcContinuation();
            k.m_result = new SingleMessageRetrieval(EmptyBasicProperties);
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        //**************************** Message publication methods ****************************
        // TODO 8.0 - API extension: waitForConfirmation
        /// <inheritdoc />
        public ValueTask ActivatePublishTagsAsync()
        {
            if (Interlocked.CompareExchange(ref _nextPublishTag, 0UL, ulong.MaxValue) == ulong.MaxValue)
            {
                ModelRpc<ConfirmSelectOk>(new ConfirmSelect(false));
            }

            return default;
        }

        /// <inheritdoc />
        public event Action<ulong, bool, bool>? PublishTagAcknowledged;

        private void HandleBasicAck(ulong deliveryTag, bool multiple)
        {
            OnPublishAckReceived(deliveryTag, multiple, true);
        }

        private void HandleBasicNack(ulong deliveryTag, bool multiple)
        {
            OnPublishAckReceived(deliveryTag, multiple, false);
        }

        private void OnPublishAckReceived(ulong deliveryTag, bool multiple, bool isAck)
        {
            var handler = PublishTagAcknowledged;
            if (handler != null)
            {
                foreach (Action<ulong, bool, bool> action in handler.GetInvocationList())
                {
                    try
                    {
                        action(deliveryTag, multiple, isAck);
                    }
                    catch (Exception e)
                    {
                        OnUnhandledExceptionOccurred(e, "OnPublishAckReceived");
                    }
                }
            }

            // TODO 8.0 - Wait for confirms - Remove when moved
            if (IsPublishAcksEnabled)
            {
                // let's take a lock so we can assume that deliveryTags are unique, never duplicated and always sorted
                lock (_confirmLock)
                {
                    // No need to do anything if there are no delivery tags in the list
                    if (_pendingDeliveryTags.Count > 0)
                    {
                        if (multiple)
                        {
                            int count = 0;
                            while (_pendingDeliveryTags.First!.Value < deliveryTag)
                            {
                                _pendingDeliveryTags.RemoveFirst();
                                count++;
                            }

                            if (_pendingDeliveryTags.First!.Value == deliveryTag)
                            {
                                _pendingDeliveryTags.RemoveFirst();
                                count++;
                            }

                            if (count > 0)
                            {
                                _deliveryTagsCountdown.Signal(count);
                            }
                        }
                        else
                        {
                            if (_pendingDeliveryTags.Remove(deliveryTag))
                            {
                                _deliveryTagsCountdown.Signal();
                            }
                        }
                    }

                    _onlyAcksReceived = _onlyAcksReceived && isAck;
                }
            }
        }

        /// <inheritdoc />
        public event Action<ulong>? NewPublishTagUsed;

        /// <inheritdoc />
        public ValueTask PublishMessageAsync(string exchange, string routingKey, IBasicProperties? basicProperties, ReadOnlyMemory<byte> body, bool mandatory = false)
        {
            if (IsPublishAcksEnabled)
            {
                AllocatePublishTagUsed();
            }

            basicProperties ??= EmptyBasicProperties;
            ModelSend(new BasicPublish(default, exchange, routingKey, mandatory, default), (BasicProperties) basicProperties, body);
            return default;
        }

        /// <inheritdoc />
        public ValueTask PublishBatchAsync(MessageBatch batch)
        {
            if (IsPublishAcksEnabled)
            {
                AllocatePublishTagUsed(batch.Commands.Count);
            }

            SendCommands(batch.Commands);

            return default;
        }

        private void AllocatePublishTagUsed(int count)
        {
            // TODO 8.0 - Wait for confirms - Remove when moved
            ulong endTag;
            lock (_confirmLock)
            {
                if (_deliveryTagsCountdown.IsSet)
                {
                    _deliveryTagsCountdown.Reset(count);
                }
                else
                {
                    _deliveryTagsCountdown.AddCount(count);
                }

                for (int i = 0; i < count; i++)
                {
                    ulong tag = Interlocked.Increment(ref _nextPublishTag);
                    _pendingDeliveryTags.AddLast(tag);
                }
                endTag = _nextPublishTag;
            }

            var handler = NewPublishTagUsed;
            if (handler != null)
            {
                foreach (Action<ulong> action in handler.GetInvocationList())
                {
                    try
                    {
                        for (ulong tag = endTag - (uint)count; tag <= endTag; tag++)
                        {
                            action(tag);
                        }
                    }
                    catch (Exception e)
                    {
                        OnUnhandledExceptionOccurred(e, "AllocatePublishTagUsed");
                    }
                }
            }
        }

        private ulong AllocatePublishTagUsed()
        {
            ulong tag;

            // TODO 8.0 - Wait for confirms - Remove when moved
            lock (_confirmLock)
            {
                if (_deliveryTagsCountdown.IsSet)
                {
                    _deliveryTagsCountdown.Reset(1);
                }
                else
                {
                    _deliveryTagsCountdown.AddCount();
                }

                tag = Interlocked.Increment(ref _nextPublishTag);
                _pendingDeliveryTags.AddLast(tag);
            }

            var handler = NewPublishTagUsed;
            if (handler != null)
            {
                foreach (Action<ulong> action in handler.GetInvocationList())
                {
                    try
                    {
                        action(tag);
                    }
                    catch (Exception e)
                    {
                        OnUnhandledExceptionOccurred(e, "AllocatePublishTagUsed");
                    }
                }
            }

            return tag;
        }

        /// <inheritdoc />
        public event MessageDeliveryFailedDelegate? MessageDeliveryFailed;

        private void HandleBasicReturn(ushort replyCode, string replyText, string exchange, string routingKey, IBasicProperties basicProperties, ReadOnlyMemory<byte> body, byte[] rentedArray)
        {
            var eventHandler = MessageDeliveryFailed;
            if (eventHandler != null)
            {
                var message = new Message(exchange, routingKey, basicProperties, body);
                foreach (Action<Message, string, ushort> action in eventHandler.GetInvocationList())
                {
                    try
                    {
                        action(message, replyText, replyCode);
                    }
                    catch (Exception exception)
                    {
                        OnUnhandledExceptionOccurred(exception, "OnBasicReturn");
                    }
                }
            }
            ArrayPool<byte>.Shared.Return(rentedArray);
        }

        //**************************** Exchange methods ****************************
        /// <inheritdoc />
        public ValueTask DeclareExchangeAsync(string exchange, string type, bool durable, bool autoDelete, bool throwOnMismatch = true, IDictionary<string, object>? arguments = null, bool waitForConfirmation = true)
        {
            ExchangeDeclare method = new ExchangeDeclare(default, exchange, type, !throwOnMismatch, durable, autoDelete, false, !waitForConfirmation, arguments);
            if (waitForConfirmation)
            {
                ModelRpc<ExchangeDeclareOk>(method);
            }
            else
            {
                ModelSend(method);
            }

            return default;
        }

        /// <inheritdoc />
        public ValueTask DeleteExchangeAsync(string exchange, bool ifUnused = false, bool waitForConfirmation = true)
        {
            ExchangeDelete method = new ExchangeDelete(default, exchange, ifUnused, !waitForConfirmation);
            if (waitForConfirmation)
            {
                ModelRpc<ExchangeDeleteOk>(method);
            }
            else
            {
                ModelSend(method);
            }

            return default;
        }

        /// <inheritdoc />
        public ValueTask BindExchangeAsync(string destination, string source, string routingKey, IDictionary<string, object>? arguments = null, bool waitForConfirmation = true)
        {
            ExchangeBind method = new ExchangeBind(default, destination, source, routingKey, !waitForConfirmation, arguments);
            if (waitForConfirmation)
            {
                ModelRpc<ExchangeBindOk>(method);
            }
            else
            {
                ModelSend(method);
            }

            return default;
        }

        /// <inheritdoc />
        public ValueTask UnbindExchangeAsync(string destination, string source, string routingKey, IDictionary<string, object>? arguments = null, bool waitForConfirmation = true)
        {
            ExchangeUnbind method = new ExchangeUnbind(default, destination, source, routingKey, !waitForConfirmation, arguments);
            if (waitForConfirmation)
            {
                ModelRpc<ExchangeUnbindOk>(method);
            }
            else
            {
                ModelSend(method);
            }

            return default;
        }

        //**************************** Queue methods ****************************
        /// <inheritdoc />
        public ValueTask<(string QueueName, uint MessageCount, uint ConsumerCount)> DeclareQueueAsync(string queue,
            bool durable, bool exclusive, bool autoDelete, bool throwOnMismatch = true, IDictionary<string, object>? arguments = null)
        {
            // TODO 8.0 - Call ModelRpc
            var k = new ModelBase.QueueDeclareRpcContinuation();
            lock (_rpcLock)
            {
                Enqueue(k);
                ModelSend(new QueueDeclare(default, queue, !throwOnMismatch, durable, exclusive, autoDelete, false, arguments));
                k.GetReply(ContinuationTimeout);
            }
            return new ValueTask<(string, uint, uint)>((k.m_result.QueueName, k.m_result.MessageCount, k.m_result.ConsumerCount));
        }

        /// <inheritdoc />
        public ValueTask DeclareQueueWithoutConfirmationAsync(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object>? arguments = null)
        {
            ModelSend(new QueueDeclare(default, queue, false, durable, exclusive, autoDelete, true, arguments));
            return default;
        }

        /// <inheritdoc />
        public ValueTask<uint> DeleteQueueAsync(string queue, bool ifUnused = false, bool ifEmpty = false)
        {
            var result = ModelRpc<QueueDeleteOk>(new QueueDelete(default, queue, ifUnused, ifEmpty, false));
            return new ValueTask<uint>(result._messageCount);
        }

        private void HandleQueueDeclareOk(string queue, uint messageCount, uint consumerCount)
        {
            var k = (ModelBase.QueueDeclareRpcContinuation)GetRpcContinuation();
            k.m_result = new QueueDeclareOk(queue, messageCount, consumerCount);
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        /// <inheritdoc />
        public ValueTask DeleteQueueWithoutConfirmationAsync(string queue, bool ifUnused = false, bool ifEmpty = false)
        {
            QueueDelete method = new QueueDelete(default, queue, ifUnused, ifEmpty, true);
            ModelSend(method);
            return default;
        }

        /// <inheritdoc />
        public ValueTask BindQueueAsync(string queue, string exchange, string routingKey, IDictionary<string, object>? arguments = null, bool waitForConfirmation = true)
        {
            QueueBind method = new QueueBind(default, queue, exchange, routingKey, !waitForConfirmation, arguments);
            if (waitForConfirmation)
            {
                ModelRpc<QueueBindOk>(method);
            }
            else
            {
                ModelSend(method);
            }

            return default;
        }

        /// <inheritdoc />
        public ValueTask UnbindQueueAsync(string queue, string exchange, string routingKey, IDictionary<string, object>? arguments = null)
        {
            ModelRpc<QueueUnbindOk>(new QueueUnbind(default, queue, exchange, routingKey, arguments));
            return default;
        }

        // TODO 8.0 - API extension: waitForConfirmation
        /// <inheritdoc />
        public ValueTask<uint> PurgeQueueAsync(string queue)
        {
            QueuePurgeOk result = ModelRpc<QueuePurgeOk>(new QueuePurge(default, queue, false));
            return new ValueTask<uint>(result._messageCount);
        }

        //**************************** Transaction methods ****************************
        /// <inheritdoc />
        public ValueTask ActivateTransactionsAsync()
        {
            ModelRpc<TxSelectOk>(new TxSelect());
            return default;
        }

        /// <inheritdoc />
        public ValueTask CommitTransactionAsync()
        {
            ModelRpc<TxCommitOk>(new TxCommit());
            return default;
        }

        /// <inheritdoc />
        public ValueTask RollbackTransactionAsync()
        {
            ModelRpc<TxRollbackOk>(new TxRollback());
            return default;
        }

        //**************************** Recover methods ****************************
        /// <inheritdoc />
        public ValueTask ResendUnackedMessages(bool requeue)
        {
            // TODO 8.0 - Call ModelRpc
            var k = new SimpleBlockingRpcContinuation();

            lock (_rpcLock)
            {
                Enqueue(k);
                ModelSend(new BasicRecover(requeue));
                k.GetReply(ContinuationTimeout);
            }

            return default;
        }

        private void HandleBasicRecoverOk()
        {
            var k = (SimpleBlockingRpcContinuation)GetRpcContinuation();
            k.HandleCommand(IncomingCommand.Empty);
        }

        //**************************** Close methods ****************************
        /// <inheritdoc />
        public ValueTask AbortAsync(ushort replyCode, string replyText)
        {
            return CloseAsync(replyCode, replyText, true);
        }

        /// <inheritdoc />
        public ValueTask CloseAsync(ushort replyCode, string replyText)
        {
            return CloseAsync(replyCode, replyText, false);
        }

        private ValueTask CloseAsync(ushort replyCode, string replyText, bool abort)
        {
            var k = new ShutdownContinuation();
            Shutdown += k.OnConnectionShutdown;

            try
            {
                ConsumerDispatcher.Quiesce();
                if (SetCloseReason(new ShutdownEventArgs(ShutdownInitiator.Application, replyCode, replyText)))
                {
                    ModelSend(new ChannelClose(replyCode, replyText, 0, 0));
                }
                k.Wait(TimeSpan.FromMilliseconds(10000));
            }
            catch (Exception)
            {
                if (!abort)
                {
                    throw;
                }
            }

            return new ValueTask(ConsumerDispatcher.Shutdown());
        }

        private void HandleChannelClose(ushort replyCode, string replyText, ushort classId, ushort methodId)
        {
            SetCloseReason(new ShutdownEventArgs(ShutdownInitiator.Peer, replyCode, replyText, classId, methodId));
            Session.Close(CloseReason, false);
            try
            {
                ModelSend(new ChannelCloseOk());
            }
            finally
            {
                Session.Notify();
            }
        }

        private Action<ShutdownEventArgs>? _shutdownHandler;
        /// <inheritdoc />
        public event Action<ShutdownEventArgs>? Shutdown
        {
            add
            {
                if (CloseReason is null)
                {
                    Action<ShutdownEventArgs>? handler2;
                    Action<ShutdownEventArgs>? tmpHandler = _shutdownHandler;
                    do
                    {
                        handler2 = tmpHandler;
                        var combinedHandler = (Action<ShutdownEventArgs>?)Delegate.Combine(handler2, value);
                        tmpHandler = System.Threading.Interlocked.CompareExchange(ref _shutdownHandler, combinedHandler, handler2);
                    }
                    while (tmpHandler != handler2);
                }
                else
                {
                    // Call immediately if it already closed
                    value?.Invoke(CloseReason);
                }
            }
            remove
            {
                Action<ShutdownEventArgs>? handler2;
                Action<ShutdownEventArgs>? tmpHandler = _shutdownHandler;
                do
                {
                    handler2 = tmpHandler;
                    var combinedHandler = (Action<ShutdownEventArgs>?)Delegate.Remove(handler2, value);
                    tmpHandler = System.Threading.Interlocked.CompareExchange(ref _shutdownHandler, combinedHandler, handler2);
                }
                while (tmpHandler != handler2);
            }
        }

        protected override void HandleSessionShutdown(object? sender, ShutdownEventArgs reason)
        {
            ConsumerDispatcher.Quiesce();

            base.HandleSessionShutdown(sender, reason);

            var handler = System.Threading.Interlocked.Exchange(ref _shutdownHandler, null);
            if (handler != null)
            {
                foreach (Action<ShutdownEventArgs> h in handler.GetInvocationList())
                {
                    try
                    {
                        h(reason);
                    }
                    catch (Exception e)
                    {
                        OnUnhandledExceptionOccurred(e, "OnModelShutdown");
                    }
                }
            }

            foreach (KeyValuePair<string, IBasicConsumer> c in _consumers)
            {
                ConsumerDispatcher.HandleModelShutdown(c.Value, reason);
            }

            // TODO 8.0 - Wait for confirms - Remove when moved
            _deliveryTagsCountdown.Reset(0);

            ConsumerDispatcher.Shutdown().GetAwaiter().GetResult();
        }

        private void HandleChannelCloseOk()
        {
            FinishClose();
        }

        internal void FinishClose()
        {
            if (CloseReason != null)
            {
                Session.Close(CloseReason);
            }
        }

        protected void TakeOverChannel(Channel channel)
        {
            base.TakeOverChannel(channel);

            PublishTagAcknowledged = channel.PublishTagAcknowledged;
            NewPublishTagUsed = channel.NewPublishTagUsed;
            MessageDeliveryFailed = channel.MessageDeliveryFailed;
            _shutdownHandler = channel._shutdownHandler;
        }

        //**************************** HandleCommands ****************************
        private protected override bool DispatchAsynchronous(in IncomingCommand cmd)
        {
            switch (cmd.Method.ProtocolCommandId)
            {
                case ProtocolCommandId.BasicDeliver:
                {
                    var __impl = (BasicDeliver)cmd.Method;
                    HandleBasicDeliver(__impl._consumerTag, __impl._deliveryTag, __impl._redelivered, __impl._exchange,
                        __impl._routingKey, (IBasicProperties)cmd.Header, cmd.Body, cmd.TakeoverPayload());
                    return true;
                }
                case ProtocolCommandId.BasicAck:
                {
                    var __impl = (BasicAck)cmd.Method;
                    HandleBasicAck(__impl._deliveryTag, __impl._multiple);
                    return true;
                }
                case ProtocolCommandId.BasicCancel:
                {
                    var __impl = (BasicCancel)cmd.Method;
                    HandleBasicCancel(__impl._consumerTag);
                    return true;
                }
                case ProtocolCommandId.BasicCancelOk:
                {
                    var __impl = (BasicCancelOk)cmd.Method;
                    HandleBasicCancelOk(__impl._consumerTag);
                    return true;
                }
                case ProtocolCommandId.BasicConsumeOk:
                {
                    var __impl = (BasicConsumeOk)cmd.Method;
                    HandleBasicConsumeOk(__impl._consumerTag);
                    return true;
                }
                case ProtocolCommandId.BasicGetEmpty:
                {
                    HandleBasicGetEmpty();
                    return true;
                }
                case ProtocolCommandId.BasicGetOk:
                {
                    var __impl = (BasicGetOk)cmd.Method;
                    HandleBasicGetOk(__impl._deliveryTag, __impl._redelivered, __impl._exchange, __impl._routingKey,
                        __impl._messageCount, (IBasicProperties)cmd.Header, cmd.Body, cmd.TakeoverPayload());
                    return true;
                }
                case ProtocolCommandId.BasicNack:
                {
                    var __impl = (BasicNack)cmd.Method;
                    HandleBasicNack(__impl._deliveryTag, __impl._multiple);
                    return true;
                }
                case ProtocolCommandId.BasicRecoverOk:
                {
                    HandleBasicRecoverOk();
                    return true;
                }
                case ProtocolCommandId.BasicReturn:
                {
                    var __impl = (BasicReturn)cmd.Method;
                    HandleBasicReturn(__impl._replyCode, __impl._replyText, __impl._exchange, __impl._routingKey,
                        (IBasicProperties)cmd.Header, cmd.Body, cmd.TakeoverPayload());
                    return true;
                }
                case ProtocolCommandId.ChannelClose:
                {
                    var __impl = (ChannelClose)cmd.Method;
                    HandleChannelClose(__impl._replyCode, __impl._replyText, __impl._classId, __impl._methodId);
                    return true;
                }
                case ProtocolCommandId.ChannelCloseOk:
                {
                    HandleChannelCloseOk();
                    return true;
                }
                case ProtocolCommandId.ChannelFlow:
                {
                    var __impl = (ChannelFlow)cmd.Method;
                    HandleChannelFlow(__impl._active);
                    return true;
                }
                case ProtocolCommandId.QueueDeclareOk:
                {
                    var __impl = (Framing.Impl.QueueDeclareOk)cmd.Method;
                    HandleQueueDeclareOk(__impl._queue, __impl._messageCount, __impl._consumerCount);
                    return true;
                }
                default: return false;
            }
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            await this.AbortAsync().ConfigureAwait(false);
            await base.DisposeAsyncCore().ConfigureAwait(false);
        }

        //**************************** Wait for confirms **************************
        // TODO 8.0 - Wait for confirms - Extract to it's own class
        private readonly LinkedList<ulong> _pendingDeliveryTags = new LinkedList<ulong>();
        private readonly CountdownEvent _deliveryTagsCountdown = new CountdownEvent(0);
        private readonly object _confirmLock = new object();
        private bool _onlyAcksReceived = true;

        /// <inheritdoc />
        public bool WaitForConfirms()
        {
            return WaitForConfirms(TimeSpan.FromMilliseconds(Timeout.Infinite), out _);
        }

        /// <inheritdoc />
        public bool WaitForConfirms(TimeSpan timeout)
        {
            return WaitForConfirms(timeout, out _);
        }

        /// <inheritdoc />
        public void WaitForConfirmsOrDie()
        {
            WaitForConfirmsOrDie(TimeSpan.FromMilliseconds(Timeout.Infinite));
        }

        /// <inheritdoc />
        public void WaitForConfirmsOrDie(TimeSpan timeout)
        {
            bool onlyAcksReceived = WaitForConfirms(timeout, out bool timedOut);
            if (!onlyAcksReceived)
            {
                CloseAsync(Constants.ReplySuccess, "Nacks Received", false).GetAwaiter().GetResult();
                throw new IOException("Nacks Received");
            }
            if (timedOut)
            {
                CloseAsync(Constants.ReplySuccess, "Timed out waiting for acks", false).GetAwaiter().GetResult();
                throw new IOException("Timed out waiting for acks");
            }
        }

        private bool WaitForConfirms(TimeSpan timeout, out bool timedOut)
        {
            if (!IsPublishAcksEnabled)
            {
                throw new InvalidOperationException("Confirms not selected");
            }
            bool isWaitInfinite = timeout == Timeout.InfiniteTimeSpan;
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (true)
            {
                if (!IsOpen)
                {
                    throw new AlreadyClosedException(CloseReason);
                }

                if (_deliveryTagsCountdown.IsSet)
                {
                    bool aux = _onlyAcksReceived;
                    _onlyAcksReceived = true;
                    timedOut = false;
                    return aux;
                }

                if (isWaitInfinite)
                {
                    _deliveryTagsCountdown.Wait();
                }
                else
                {
                    TimeSpan elapsed = stopwatch.Elapsed;
                    if (elapsed > timeout || !_deliveryTagsCountdown.Wait(timeout - elapsed))
                    {
                        timedOut = true;
                        return _onlyAcksReceived;
                    }
                }
            }
        }
    }
}
