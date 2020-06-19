// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2020 VMware, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       https://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at https://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    internal class Model : IFullModel, IRecoverable
    {
        ///<summary>Only used to kick-start a connection open
        ///sequence. See <see cref="Connection.Open"/> </summary>
        protected readonly Channel<Command> _consumerCommandQueue = Channel.CreateUnbounded<Command>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        private readonly Dictionary<string, IAsyncBasicConsumer> _consumers = new Dictionary<string, IAsyncBasicConsumer>();
        protected RpcContinuationQueue _continuationQueue;
        private readonly SemaphoreSlim _flowControlSemaphore = new SemaphoreSlim(1, 1);
        private Task _consumerTask;

        private readonly object _shutdownLock = new object();
        private readonly object _confirmLock = new object();
        private readonly LinkedList<ulong> _pendingDeliveryTags = new LinkedList<ulong>();
        private readonly SemaphoreSlim _consumerSemaphore = new SemaphoreSlim(1, 1);
        private AsyncEventHandler<ShutdownEventArgs> _modelShutdown;
        private bool _onlyAcksReceived = true;

        internal Model(ISession session) => Initialise(session);

        protected void Initialise(ISession session)
        {
            CloseReason = null;
            NextPublishSeqNo = 0;
            Session = session;
            Session.CommandReceived = HandleCommand;
            Session.SessionShutdown += OnSessionShutdown;
            _consumerTask = Task.Run(ConsumerDispatch);
            _continuationQueue = new RpcContinuationQueue(session);
        }

        private async ValueTask ConsumerDispatch()
        {
            while (await _consumerCommandQueue.Reader.WaitToReadAsync())
            {
                while (_consumerCommandQueue.Reader.TryRead(out Command item))
                {
                    try
                    {
                        ValueTask task = default;
                        switch (item.Method.ProtocolCommandId)
                        {
                            case MethodConstants.BasicDeliver:
                                var basicDeliver = item.Method as BasicDeliver;
                                task = HandleBasicDeliver(basicDeliver._consumerTag, basicDeliver._deliveryTag, basicDeliver._redelivered, basicDeliver._exchange, basicDeliver._routingKey, (IBasicProperties)item.Header, item.Body);
                                break;
                            case MethodConstants.BasicAck:
                                var basicAck = item.Method as BasicAck;
                                task = HandleBasicAck(basicAck._deliveryTag, basicAck._multiple);
                                break;
                            case MethodConstants.BasicCancel:
                                var basicCancel = item.Method as BasicCancel;
                                task = HandleBasicCancel(basicCancel._consumerTag, basicCancel._nowait);
                                break;
                            case MethodConstants.BasicCancelOk:
                                var basicCancelOk = item.Method as BasicCancelOk;
                                task = HandleBasicCancelOk(basicCancelOk._consumerTag);
                                break;
                            case MethodConstants.BasicNack:
                                var basicNack = item.Method as BasicNack;
                                task = HandleBasicNack(basicNack._deliveryTag, basicNack._multiple, basicNack._requeue);
                                break;
                            case MethodConstants.BasicReturn:
                                var basicReturn = item.Method as BasicReturn;
                                task = HandleBasicReturn(basicReturn._replyCode, basicReturn._replyText, basicReturn._exchange, basicReturn._routingKey, (IBasicProperties)item.Header, item.Body);
                                break;
                            case MethodConstants.ChannelFlow:
                                var channelFlow = item.Method as ChannelFlow;
                                task = HandleChannelFlow(channelFlow._active);
                                break;
                            case MethodConstants.ChannelClose:
                                var channelClose = item.Method as ChannelClose;
                                task = HandleChannelClose(channelClose._replyCode, channelClose._replyText, channelClose._classId, channelClose._methodId);
                                break;
                            default:
                                break;
                        }

                        if (!task.IsCompletedSuccessfully)
                        {
                            await task.ConfigureAwait(false);
                        }
                    }
                    catch { }
                    finally
                    {
                        item.Dispose();
                    }
                }
            }
        }

        public TimeSpan ContinuationTimeout { get; set; } = TimeSpan.FromSeconds(20);

        public event EventHandler<BasicAckEventArgs> BasicAcks;
        public event EventHandler<BasicNackEventArgs> BasicNacks;
        public event EventHandler<EventArgs> BasicRecoverOk;
        public event EventHandler<BasicReturnEventArgs> BasicReturn;
        public event EventHandler<CallbackExceptionEventArgs> CallbackException;
        public event EventHandler<FlowControlEventArgs> FlowControl;
        public event AsyncEventHandler<ShutdownEventArgs> ModelShutdown
        {
            add
            {
                bool ok = false;
                if (CloseReason == null)
                {
                    lock (_shutdownLock)
                    {
                        if (CloseReason == null)
                        {
                            _modelShutdown += value;
                            ok = true;
                        }
                    }
                }
                if (!ok)
                {
                    value(this, CloseReason);
                }
            }
            remove
            {
                lock (_shutdownLock)
                {
                    _modelShutdown -= value;
                }
            }
        }

#pragma warning disable 67
        public event EventHandler<EventArgs> Recovery;
#pragma warning restore 67

        public int ChannelNumber => ((Session)Session).ChannelNumber;

        public ShutdownEventArgs CloseReason { get; private set; }

        public IAsyncBasicConsumer DefaultConsumer { get; set; }

        public bool IsClosed => !IsOpen;

        public bool IsOpen => CloseReason == null;

        public ulong NextPublishSeqNo { get; private set; }

        public ISession Session { get; private set; }

        public ValueTask Close(ushort replyCode, string replyText, bool abort) => Close(new ShutdownEventArgs(ShutdownInitiator.Application,
                replyCode, replyText),
                abort);

        public async ValueTask Close(ShutdownEventArgs reason, bool abort)
        {
            try
            {
                if (SetCloseReason(reason))
                {
                    await TransmitAndEnqueueAsync<ChannelCloseOk>(new ChannelClose(reason.ReplyCode, reason.ReplyText, reason.ClassId, reason.MethodId), TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                    await FinishClose().ConfigureAwait(false);
                }
            }
            catch (AlreadyClosedException)
            {
                if (!abort)
                {
                    throw;
                }
            }
            catch (IOException)
            {
                if (!abort)
                {
                    throw;
                }
            }
            catch (Exception)
            {
                if (!abort)
                {
                    throw;
                }
            }
        }

        public bool DispatchAsynchronous(Command cmd)
        {
            switch (cmd.Method.ProtocolCommandId)
            {
                case MethodConstants.BasicAck:
                case MethodConstants.BasicCancel:
                case MethodConstants.BasicCancelOk:
                case MethodConstants.BasicDeliver:
                case MethodConstants.BasicNack:
                case MethodConstants.BasicReturn:
                case MethodConstants.ChannelClose:
                case MethodConstants.ChannelFlow:
                    {
                        _consumerCommandQueue.Writer.TryWrite(cmd);
                        return true;
                    }
                default: return false;
            }
        }

        private ValueTask<T> EnqueueAsync<T>(Command cmd, TimeSpan timeout = default) where T : MethodBase => _continuationQueue.SendAndReceiveAsync<T>(cmd, timeout);

        private ValueTask<Command> EnqueueAsync(Command cmd, TimeSpan timeout = default) => _continuationQueue.SendAndReceiveAsync(cmd, timeout);

        public async ValueTask FinishClose()
        {
            if (CloseReason != null)
            {
                await Session.Close(CloseReason).ConfigureAwait(false);
            }
        }

        public ValueTask HandleCommand(ISession session, Command cmd)
        {
#if DEBUG
            Debug.WriteLine("Received command: " + cmd.Method.ProtocolMethodName);
#endif
            // If this is a Connection method, let's send it to the Connection handler
            if ((cmd.Method.ProtocolCommandId >> 16) == ClassConstants.Connection)
            {
                return Session.Connection.HandleCommand(cmd);
            }

            if (!DispatchAsynchronous(cmd))// Was asynchronous. Already processed. No need to process further.
            {
                _continuationQueue.HandleCommand(cmd);
            }

            return default;
        }

        public virtual void OnBasicAck(BasicAckEventArgs args)
        {
            foreach (EventHandler<BasicAckEventArgs> h in BasicAcks?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try
                {
                    h(this, args);
                }
                catch (Exception e)
                {
                    OnCallbackException(CallbackExceptionEventArgs.Build(e));
                }
            }

            handleAckNack(args.DeliveryTag, args.Multiple, false);
        }

        public virtual void OnBasicNack(BasicNackEventArgs args)
        {
            foreach (EventHandler<BasicNackEventArgs> h in BasicNacks?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try
                {
                    h(this, args);
                }
                catch (Exception e)
                {
                    OnCallbackException(CallbackExceptionEventArgs.Build(e));
                }
            }

            handleAckNack(args.DeliveryTag, args.Multiple, true);
        }

        public virtual void OnBasicRecoverOk(EventArgs args)
        {
            foreach (EventHandler<EventArgs> h in BasicRecoverOk?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try
                {
                    h(this, args);
                }
                catch (Exception e)
                {
                    OnCallbackException(CallbackExceptionEventArgs.Build(e));
                }
            }
        }

        public virtual void OnBasicReturn(BasicReturnEventArgs args)
        {
            foreach (EventHandler<BasicReturnEventArgs> h in BasicReturn?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try
                {
                    h(this, args);
                }
                catch (Exception e)
                {
                    OnCallbackException(CallbackExceptionEventArgs.Build(e));
                }
            }
        }

        public virtual void OnCallbackException(CallbackExceptionEventArgs args)
        {
            foreach (EventHandler<CallbackExceptionEventArgs> h in CallbackException?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try
                {
                    h(this, args);
                }
                catch
                {
                    // Exception in
                    // Callback-exception-handler. That was the
                    // app's last chance. Swallow the exception.
                    // FIXME: proper logging
                }
            }
        }

        public virtual void OnFlowControl(FlowControlEventArgs args)
        {
            foreach (EventHandler<FlowControlEventArgs> h in FlowControl?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try
                {
                    h(this, args);
                }
                catch (Exception e)
                {
                    OnCallbackException(CallbackExceptionEventArgs.Build(e));
                }
            }
        }

        ///<summary>Broadcasts notification of the final shutdown of the model.</summary>
        ///<remarks>
        ///<para>
        ///Do not call anywhere other than at the end of OnSessionShutdown.
        ///</para>
        ///<para>
        ///Must not be called when m_closeReason == null, because
        ///otherwise there's a window when a new continuation could be
        ///being enqueued at the same time as we're broadcasting the
        ///shutdown event. See the definition of Enqueue() above.
        ///</para>
        ///</remarks>
        public virtual async ValueTask OnModelShutdown(ShutdownEventArgs reason)
        {
            _continuationQueue.HandleModelShutdown(reason);
            AsyncEventHandler<ShutdownEventArgs> handler;
            lock (_shutdownLock)
            {
                handler = _modelShutdown;
                _modelShutdown = null;
            }

            if (handler != null)
            {
                foreach (AsyncEventHandler<ShutdownEventArgs> h in handler.GetInvocationList())
                {
                    try
                    {
                        await h(this, reason).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        OnCallbackException(CallbackExceptionEventArgs.Build(e));
                    }
                }
            }

            lock (_confirmLock)
            {
                if (_consumerSemaphore.CurrentCount == 0)
                {
                    _consumerSemaphore.Release();
                }
            }

            if (_flowControlSemaphore.CurrentCount == 0)
            {
                _flowControlSemaphore.Release();
            }
        }

        public async ValueTask OnSessionShutdown(object sender, ShutdownEventArgs reason)
        {
            SetCloseReason(reason);
            await OnModelShutdown(reason).ConfigureAwait(false);
            await BroadcastShutdownToConsumers(_consumers, reason).ConfigureAwait(false);
        }

        protected async ValueTask BroadcastShutdownToConsumers(Dictionary<string, IAsyncBasicConsumer> cs, ShutdownEventArgs reason)
        {
            foreach (KeyValuePair<string, IAsyncBasicConsumer> c in cs)
            {
                try
                {
                    await c.Value.HandleModelShutdown(this, reason).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    OnCallbackException(CallbackExceptionEventArgs.Build(e));
                }
            }
        }

        public bool SetCloseReason(ShutdownEventArgs reason)
        {
            if (CloseReason == null)
            {
                lock (_shutdownLock)
                {
                    if (CloseReason == null)
                    {
                        CloseReason = reason;
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else
            {
                return false;
            }
        }

        public override string ToString() => Session.ToString();

        public ValueTask<T> TransmitAndEnqueueAsync<T>(MethodBase method, TimeSpan timeout = default) where T : MethodBase => TransmitAndEnqueueAsync<T>(new Command(method), timeout);

        public ValueTask<T> TransmitAndEnqueueAsync<T>(Command cmd, TimeSpan timeout = default) where T : MethodBase
        {
            if (CloseReason != null && cmd.Method.ProtocolCommandId != MethodConstants.ChannelClose)
            {
                throw new AlreadyClosedException(CloseReason);
            }

            return EnqueueAsync<T>(cmd, timeout);
        }

        public ValueTask<Command> TransmitAndEnqueueAsync(Command cmd, TimeSpan timeout = default)
        {
            if (CloseReason != null && cmd.Method.ProtocolCommandId != MethodConstants.ChannelClose)
            {
                throw new AlreadyClosedException(CloseReason);
            }

            return EnqueueAsync(cmd, timeout);
        }

        void IDisposable.Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // dispose managed resources
                Abort();
            }

            // dispose unmanaged resources
        }

        public ValueTask HandleBasicAck(ulong deliveryTag, bool multiple)
        {
            var e = new BasicAckEventArgs
            {
                DeliveryTag = deliveryTag,
                Multiple = multiple
            };
            OnBasicAck(e);
            return default;
        }

        public async ValueTask HandleBasicCancel(string consumerTag, bool nowait)
        {
            if (!_consumers.TryGetValue(consumerTag, out IAsyncBasicConsumer consumer))
            {
                if (DefaultConsumer == null)
                {
                    throw new InvalidOperationException("Unsolicited delivery - see IModel.DefaultConsumer to handle this case.");
                }
                else
                {
                    consumer = DefaultConsumer;
                }
            }

            try
            {
                ValueTask cancel = consumer.HandleBasicCancel(consumerTag);
                if (!cancel.IsCompletedSuccessfully)
                {
                    await cancel.ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                OnCallbackException(new CallbackExceptionEventArgs(e));
            }

            lock (_consumers)
            {
                _consumers.Remove(consumerTag);
            }
        }

        public async ValueTask HandleBasicCancelOk(string consumerTag)
        {
            IAsyncBasicConsumer consumer = _consumers[consumerTag];
            ModelShutdown -= _consumers[consumerTag].HandleModelShutdown;
            lock (_consumers)
            {
                _consumers.Remove(consumerTag);
            }

            try
            {
                ValueTask cancel = consumer.HandleBasicCancelOk(consumerTag);
                if (!cancel.IsCompletedSuccessfully)
                {
                    await cancel.ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                OnCallbackException(new CallbackExceptionEventArgs(e));
            }
        }

        public async ValueTask HandleBasicConsumeOk(string consumerTag)
        {
            try
            {
                ValueTask cancel = _consumers[consumerTag].HandleBasicConsumeOk(consumerTag);
                if (!cancel.IsCompletedSuccessfully)
                {
                    await cancel.ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                OnCallbackException(new CallbackExceptionEventArgs(e));
            }
        }

        public virtual async ValueTask HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body)
        {
            if (!_consumers.TryGetValue(consumerTag, out IAsyncBasicConsumer consumer))
            {
                if (DefaultConsumer == null)
                {
                    throw new InvalidOperationException("Unsolicited delivery - see IModel.DefaultConsumer to handle this case.");
                }
                else
                {
                    consumer = DefaultConsumer;
                }
            }

            try
            {
                ValueTask cancel = consumer.HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange, routingKey, basicProperties, body);
                if (!cancel.IsCompletedSuccessfully)
                {
                    await cancel.ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                OnCallbackException(new CallbackExceptionEventArgs(e));
            }
        }

        public ValueTask HandleBasicNack(ulong deliveryTag,
            bool multiple,
            bool requeue)
        {
            var e = new BasicNackEventArgs
            {
                DeliveryTag = deliveryTag,
                Multiple = multiple,
                Requeue = requeue
            };
            OnBasicNack(e);
            return default;
        }

        public ValueTask HandleBasicRecoverOk()
        {
            OnBasicRecoverOk(new EventArgs());
            return default;
        }

        public ValueTask HandleBasicReturn(ushort replyCode,
            string replyText,
            string exchange,
            string routingKey,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body)
        {
            var e = new BasicReturnEventArgs
            {
                ReplyCode = replyCode,
                ReplyText = replyText,
                Exchange = exchange,
                RoutingKey = routingKey,
                BasicProperties = basicProperties,
                Body = body
            };
            OnBasicReturn(e);
            return default;
        }

        public async ValueTask HandleChannelClose(ushort replyCode,
            string replyText,
            ushort classId,
            ushort methodId)
        {
            SetCloseReason(new ShutdownEventArgs(ShutdownInitiator.Peer,
                replyCode,
                replyText,
                classId,
                methodId));

            await Session.Close(CloseReason, false).ConfigureAwait(false);
            try
            {
                await Session.Transmit(new Command(new ChannelCloseOk())).ConfigureAwait(false);
            }
            finally
            {
                Session.Notify();
            }
        }

        public ValueTask HandleChannelCloseOk() => FinishClose();

        public async ValueTask HandleChannelFlow(bool active)
        {
            if (active)
            {
                if (_flowControlSemaphore.CurrentCount == 0)
                {
                    _flowControlSemaphore.Release();
                }
            }
            else
            {
                _flowControlSemaphore.Wait(0);
            }

            await Session.Transmit(new Command(new ChannelFlowOk(active))).ConfigureAwait(false);
            OnFlowControl(new FlowControlEventArgs(active));
        }

        public ValueTask HandleQueueDeclareOk(string queue, uint messageCount, uint consumerCount) => default;

        public void Abort() => Abort(Constants.ReplySuccess, "Goodbye");

        public void Abort(ushort replyCode, string replyText) => Close(replyCode, replyText, true);

        public virtual ValueTask BasicAck(ulong deliveryTag, bool multiple) => Session.Transmit(new Command(new BasicAck(deliveryTag, multiple)));

        public ValueTask BasicCancel(string consumerTag) => Session.Transmit(new Command(new Framing.Impl.BasicCancel(consumerTag, false)));

        public async ValueTask BasicCancelNoWait(string consumerTag)
        {
            await Session.Transmit(new Command(new Framing.Impl.BasicCancel(consumerTag, true))).ConfigureAwait(false);
            lock (_consumers)
            {
                _consumers.Remove(consumerTag);
            }
        }

        public async ValueTask<string> BasicConsume(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive, IDictionary<string, object> arguments, IAsyncBasicConsumer consumer)
        {
            Command result = await TransmitAndEnqueueAsync(new Command(new BasicConsume() { _queue = queue, _noAck = autoAck, _consumerTag = consumerTag, _noLocal = noLocal, _exclusive = exclusive, _arguments = arguments })).ConfigureAwait(false);
            if (result.Method is Framing.Impl.BasicConsumeOk resultMethod)
            {
                consumerTag = resultMethod._consumerTag;
                lock (_consumers)
                {
                    _consumers[consumerTag] = consumer;
                }

                await HandleBasicConsumeOk(consumerTag).ConfigureAwait(false);
                return consumerTag;
            }

            throw new UnexpectedMethodException(result.Method);
        }

        public virtual async ValueTask<BasicGetResult> BasicGet(string queue, bool autoAck)
        {
            using (Command result = await TransmitAndEnqueueAsync(new Command(new BasicGet() { _queue = queue, _noAck = autoAck })).ConfigureAwait(false))
            {
                switch (result.Method)
                {
                    case BasicGetOk resultMethod:
                        var memory = new Memory<byte>(ArrayPool<byte>.Shared.Rent(result.Body.Length), 0, result.Body.Length);
                        result.Body.CopyTo(memory);
                        return new BasicGetResult(resultMethod._deliveryTag, resultMethod._redelivered, resultMethod._exchange, resultMethod._routingKey, resultMethod._messageCount, result.Header as IBasicProperties, memory);
                    case BasicGetEmpty _:
                        return null;
                    default:
                        throw new UnexpectedMethodException(result.Method);
                }
            }
        }

        public virtual ValueTask BasicNack(ulong deliveryTag, bool multiple, bool requeue) => Session.Transmit(new Command(new BasicNack(deliveryTag, multiple, requeue)));

        internal void AllocatePublishSeqNos(int count)
        {
            if (NextPublishSeqNo > 0)
            {
                lock (_confirmLock)
                {
                    _consumerSemaphore.Wait(0);
                    for (int i = 0; i < count; i++)
                    {
                        _pendingDeliveryTags.AddLast(NextPublishSeqNo++);
                    }
                }
            }
        }

        public ValueTask BasicPublish(string exchange,
            string routingKey,
            bool mandatory,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body)
        {
            if (routingKey == null)
            {
                throw new ArgumentNullException(nameof(routingKey));
            }

            if (basicProperties == null)
            {
                basicProperties = CreateBasicProperties();
            }

            if (NextPublishSeqNo > 0)
            {
                lock (_confirmLock)
                {
                    _consumerSemaphore.Wait(0);
                    _pendingDeliveryTags.AddLast(NextPublishSeqNo++);
                }
            }

            return Session.Transmit(new Command(new BasicPublish { _exchange = exchange, _routingKey = routingKey, _mandatory = mandatory }, (BasicProperties)basicProperties, body, false));
        }

        public async ValueTask BasicQos(uint prefetchSize, ushort prefetchCount, bool global) => await TransmitAndEnqueueAsync<BasicQosOk>(new BasicQos(prefetchSize, prefetchCount, global)).ConfigureAwait(false);

        public async ValueTask BasicRecover(bool requeue)
        {
            var recover = await TransmitAndEnqueueAsync<BasicRecoverOk>(new Command(new BasicRecover(requeue))).ConfigureAwait(false);
            await HandleBasicRecoverOk().ConfigureAwait(false);
        }

        public async ValueTask BasicRecoverAsync(bool requeue) => await TransmitAndEnqueueAsync<BasicRecoverOk>(new BasicRecover(requeue)).ConfigureAwait(false);

        public virtual ValueTask BasicReject(ulong deliveryTag, bool requeue) => Session.Transmit(new Command(new BasicReject(deliveryTag, requeue)));

        public ValueTask Close() => Close(Constants.ReplySuccess, "Goodbye");

        public ValueTask Close(ushort replyCode, string replyText) => Close(replyCode, replyText, false);

        public async ValueTask ConfirmSelect()
        {
            if (NextPublishSeqNo == 0UL)
            {
                NextPublishSeqNo = 1;
            }

            await TransmitAndEnqueueAsync<ConfirmSelectOk>(new ConfirmSelect()).ConfigureAwait(false);
        }

        ///////////////////////////////////////////////////////////////////////////

        public virtual IBasicProperties CreateBasicProperties() => new Framing.BasicProperties();

        public IBasicPublishBatch CreateBasicPublishBatch() => new BasicPublishBatch(this);

        public async ValueTask ExchangeBind(string destination, string source, string routingKey, IDictionary<string, object> arguments) => await TransmitAndEnqueueAsync<ExchangeBindOk>(new ExchangeBind { _destination = destination, _source = source, _routingKey = routingKey, _arguments = arguments }).ConfigureAwait(false);

        public ValueTask ExchangeBindNoWait(string destination, string source, string routingKey, IDictionary<string, object> arguments) => Session.Transmit(new Command(new ExchangeBind { _destination = destination, _source = source, _routingKey = routingKey, _arguments = arguments, _nowait = true }));

        public async ValueTask ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments) => await TransmitAndEnqueueAsync<ExchangeDeclareOk>(new ExchangeDeclare { _exchange = exchange, _type = type, _durable = durable, _autoDelete = autoDelete, _arguments = arguments }).ConfigureAwait(false);

        public ValueTask ExchangeDeclareNoWait(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments) => Session.Transmit(new Command(new ExchangeDeclare { _exchange = exchange, _type = type, _durable = durable, _autoDelete = autoDelete, _arguments = arguments, _nowait = true }));

        public async ValueTask ExchangeDeclarePassive(string exchange) => await TransmitAndEnqueueAsync<ExchangeDeclareOk>(new ExchangeDeclare { _exchange = exchange, _type = string.Empty, _passive = true }).ConfigureAwait(false);

        public async ValueTask ExchangeDelete(string exchange, bool ifUnused) => await TransmitAndEnqueueAsync<ExchangeDeleteOk>(new ExchangeDelete { _exchange = exchange, _ifUnused = ifUnused }).ConfigureAwait(false);

        public ValueTask ExchangeDeleteNoWait(string exchange, bool ifUnused) => Session.Transmit(new Command(new ExchangeDelete { _exchange = exchange, _ifUnused = ifUnused, _nowait = true }));

        public async ValueTask ExchangeUnbind(string destination, string source, string routingKey, IDictionary<string, object> arguments) => await TransmitAndEnqueueAsync<ExchangeUnbindOk>(new ExchangeUnbind { _destination = destination, _source = source, _routingKey = routingKey, _arguments = arguments }).ConfigureAwait(false);

        public ValueTask ExchangeUnbindNoWait(string destination, string source, string routingKey, IDictionary<string, object> arguments) => Session.Transmit(new Command(new ExchangeUnbind { _destination = destination, _source = source, _routingKey = routingKey, _arguments = arguments, _nowait = true }));

        public async ValueTask QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments) => await TransmitAndEnqueueAsync<QueueBindOk>(new QueueBind { _queue = queue, _exchange = exchange, _routingKey = routingKey, _arguments = arguments }).ConfigureAwait(false);

        public ValueTask QueueBindNoWait(string queue, string exchange, string routingKey, IDictionary<string, object> arguments) => Session.Transmit(new Command(new QueueBind { _queue = queue, _exchange = exchange, _routingKey = routingKey, _arguments = arguments, _nowait = true }));

        public ValueTask<QueueDeclareOk> QueueDeclare(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments) => QueueDeclare(queue, false, durable, exclusive, autoDelete, arguments);

        public ValueTask QueueDeclareNoWait(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments) => Session.Transmit(new Command(new QueueDeclare { _queue = queue, _durable = durable, _exclusive = exclusive, _autoDelete = autoDelete, _arguments = arguments, _nowait = true }));

        public ValueTask<QueueDeclareOk> QueueDeclarePassive(string queue) => QueueDeclare(queue, true, false, false, false, null);

        public async ValueTask<uint> MessageCount(string queue) => (await QueueDeclarePassive(queue).ConfigureAwait(false)).MessageCount;

        public async ValueTask<uint> ConsumerCount(string queue) => (await QueueDeclarePassive(queue).ConfigureAwait(false)).ConsumerCount;

        public async ValueTask<uint> QueueDelete(string queue, bool ifUnused, bool ifEmpty)
        {
            QueueDeleteOk result = await TransmitAndEnqueueAsync<QueueDeleteOk>(new QueueDelete { _queue = queue, _ifUnused = ifUnused, _ifEmpty = ifEmpty }).ConfigureAwait(false);
            return result._messageCount;
        }

        public ValueTask QueueDeleteNoWait(string queue, bool ifUnused, bool ifEmpty) => Session.Transmit(new Command(new QueueDelete { _queue = queue, _ifUnused = ifUnused, _ifEmpty = ifEmpty, _nowait = true }));

        public async ValueTask<uint> QueuePurge(string queue)
        {
            QueuePurgeOk result = await TransmitAndEnqueueAsync<QueuePurgeOk>(new QueuePurge { _queue = queue }).ConfigureAwait(false);
            return result._messageCount;
        }

        public async ValueTask QueueUnbind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments) => await TransmitAndEnqueueAsync<QueueUnbindOk>(new QueueUnbind { _queue = queue, _exchange = exchange, _routingKey = routingKey, _arguments = arguments }).ConfigureAwait(false);

        public async ValueTask TxCommit() => await TransmitAndEnqueueAsync<TxCommitOk>(new TxCommit()).ConfigureAwait(false);

        public async ValueTask TxRollback() => await TransmitAndEnqueueAsync<TxRollbackOk>(new TxRollback()).ConfigureAwait(false);

        public async ValueTask TxSelect() => await TransmitAndEnqueueAsync<TxSelectOk>(new TxSelect()).ConfigureAwait(false);

        public async ValueTask<bool> WaitForConfirms(TimeSpan timeout)
        {
            if (NextPublishSeqNo == 0UL)
            {
                throw new InvalidOperationException("Confirms not selected");
            }

            bool isWaitInfinite = timeout.TotalMilliseconds == Timeout.Infinite;
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (true)
            {
                if (!IsOpen)
                {
                    throw new AlreadyClosedException(CloseReason);
                }

                lock (_confirmLock)
                {
                    if (_consumerSemaphore.Wait(0))
                    {
                        try
                        {
                            bool aux = _onlyAcksReceived;
                            _onlyAcksReceived = true;
                            return aux;
                        }
                        finally
                        {
                            _consumerSemaphore.Release();
                        }
                    }
                }

                if (isWaitInfinite)
                {
                    try
                    {
                        if (!_consumerSemaphore.Wait(0))
                        {
                            await _consumerSemaphore.WaitAsync().ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        _consumerSemaphore.Release();
                    }
                }
                else
                {
                    TimeSpan elapsed = stopwatch.Elapsed;
                    try
                    {
                        if (elapsed > timeout || !await _consumerSemaphore.WaitAsync(timeout - elapsed).ConfigureAwait(false))
                        {
                            throw new TimeoutException("Timed out waiting for confirms.");
                        }
                    }
                    finally
                    {
                        if (_consumerSemaphore.CurrentCount == 0)
                        {
                            _consumerSemaphore.Release();
                        }
                    }
                }
            }
        }

        public ValueTask<bool> WaitForConfirms() => WaitForConfirms(TimeSpan.FromMilliseconds(Timeout.Infinite));

        public ValueTask WaitForConfirmsOrDie() => WaitForConfirmsOrDie(TimeSpan.FromMilliseconds(Timeout.Infinite));

        public async ValueTask WaitForConfirmsOrDie(TimeSpan timeout)
        {
            try
            {
                bool onlyAcksReceived = await WaitForConfirms(timeout).ConfigureAwait(false);
                if (!onlyAcksReceived)
                {
                    await Close(new ShutdownEventArgs(ShutdownInitiator.Application, Constants.ReplySuccess, "Nacks Received", new IOException("nack received")), false).ConfigureAwait(false);
                    throw new IOException("Nacks Received");
                }
            }
            catch (TimeoutException)
            {
                await Close(new ShutdownEventArgs(ShutdownInitiator.Application, Constants.ReplySuccess, "Timed out waiting for acks", new IOException("timed out waiting for acks")), false).ConfigureAwait(false);
                throw new IOException("Timed out waiting for acks");
            }
        }

        internal async ValueTask SendCommands(IList<Command> commands)
        {
            if (!_flowControlSemaphore.Wait(0))
            {
                await _flowControlSemaphore.WaitAsync().ConfigureAwait(false);
            }
            AllocatePublishSeqNos(commands.Count);
            await Session.Transmit(commands).ConfigureAwait(false);
        }

        protected virtual void handleAckNack(ulong deliveryTag, bool multiple, bool isNack)
        {
            // No need to do this if publisher confirms have never been enabled.
            if (NextPublishSeqNo > 0)
            {
                // let's take a lock so we can assume that deliveryTags are unique, never duplicated and always sorted
                lock (_confirmLock)
                {
                    // No need to do anything if there are no delivery tags in the list
                    if (_pendingDeliveryTags.Count > 0)
                    {
                        if (multiple)
                        {
                            while (_pendingDeliveryTags.First.Value < deliveryTag)
                            {
                                _pendingDeliveryTags.RemoveFirst();
                            }
                        }

                        _pendingDeliveryTags.Remove(deliveryTag);
                    }

                    if (_pendingDeliveryTags.Count == 0 && _consumerSemaphore.CurrentCount == 0)
                    {
                        _consumerSemaphore.Release();
                    }

                    _onlyAcksReceived = _onlyAcksReceived && !isNack;
                }
            }
        }

        private async ValueTask<QueueDeclareOk> QueueDeclare(string queue, bool passive, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments)
        {
            Command result = await TransmitAndEnqueueAsync(new Command(new QueueDeclare() { _queue = queue, _passive = passive, _durable = durable, _exclusive = exclusive, _autoDelete = autoDelete, _arguments = arguments, _nowait = false })).ConfigureAwait(false);
            if (result.Method is Framing.Impl.QueueDeclareOk resultMethod)
            {
                return new QueueDeclareOk(resultMethod._queue, resultMethod._messageCount, resultMethod._consumerCount);
            }

            throw new UnexpectedMethodException(result.Method);
        }
    }
}
