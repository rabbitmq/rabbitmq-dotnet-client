// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
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
// The MPL v2.0:
//
//---------------------------------------------------------------------------
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.ConsumerDispatching;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    internal abstract class ModelBase : IModel, IRecoverable
    {
        ///<summary>Only used to kick-start a connection open
        ///sequence. See <see cref="Connection.Open"/> </summary>
        internal BlockingCell<ConnectionStartDetails> m_connectionStartCell;
        internal readonly IBasicProperties _emptyBasicProperties;

        private readonly RpcContinuationQueue _continuationQueue = new RpcContinuationQueue();
        private readonly ManualResetEventSlim _flowControlBlock = new ManualResetEventSlim(true);

        private readonly object _rpcLock = new object();
        private readonly object _confirmLock = new object();
        private readonly LinkedList<ulong> _pendingDeliveryTags = new LinkedList<ulong>();

        private bool _onlyAcksReceived = true;

        private ShutdownEventArgs _closeReason;
        public ShutdownEventArgs CloseReason => Volatile.Read(ref _closeReason);

        internal IConsumerDispatcher ConsumerDispatcher { get; }

        protected ModelBase(bool dispatchAsync, int concurrency, ISession session)
        {
            ConsumerDispatcher = dispatchAsync ?
                (IConsumerDispatcher)new AsyncConsumerDispatcher(this, concurrency) :
                new ConsumerDispatcher(this, concurrency);

            _emptyBasicProperties = CreateBasicProperties();
            Action<Exception, string> onException = (exception, context) => OnCallbackException(CallbackExceptionEventArgs.Build(exception, context));
            _basicAcksWrapper = new EventingWrapper<BasicAckEventArgs>("OnBasicAck", onException);
            _basicNacksWrapper = new EventingWrapper<BasicNackEventArgs>("OnBasicNack", onException);
            _basicRecoverOkWrapper = new EventingWrapper<EventArgs>("OnBasicRecover", onException);
            _basicReturnWrapper = new EventingWrapper<BasicReturnEventArgs>("OnBasicReturn", onException);
            _callbackExceptionWrapper = new EventingWrapper<CallbackExceptionEventArgs>(string.Empty, (exception, context) => { });
            _flowControlWrapper = new EventingWrapper<FlowControlEventArgs>("OnFlowControl", onException);
            _modelShutdownWrapper = new EventingWrapper<ShutdownEventArgs>("OnModelShutdown", onException);
            _recoveryWrapper = new EventingWrapper<EventArgs>("OnModelRecovery", onException);
            session.CommandReceived = HandleCommand;
            session.SessionShutdown += OnSessionShutdown;
            Session = session;
        }

        internal TimeSpan HandshakeContinuationTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan ContinuationTimeout { get; set; } = TimeSpan.FromSeconds(20);

        public event EventHandler<BasicAckEventArgs> BasicAcks
        {
            add => _basicAcksWrapper.AddHandler(value);
            remove => _basicAcksWrapper.RemoveHandler(value);
        }
        private EventingWrapper<BasicAckEventArgs> _basicAcksWrapper;

        public event EventHandler<BasicNackEventArgs> BasicNacks
        {
            add => _basicNacksWrapper.AddHandler(value);
            remove => _basicNacksWrapper.RemoveHandler(value);
        }
        private EventingWrapper<BasicNackEventArgs> _basicNacksWrapper;

        public event EventHandler<EventArgs> BasicRecoverOk
        {
            add => _basicRecoverOkWrapper.AddHandler(value);
            remove => _basicRecoverOkWrapper.RemoveHandler(value);
        }
        private EventingWrapper<EventArgs> _basicRecoverOkWrapper;

        public event EventHandler<BasicReturnEventArgs> BasicReturn
        {
            add => _basicReturnWrapper.AddHandler(value);
            remove => _basicReturnWrapper.RemoveHandler(value);
        }
        private EventingWrapper<BasicReturnEventArgs> _basicReturnWrapper;

        public event EventHandler<CallbackExceptionEventArgs> CallbackException
        {
            add => _callbackExceptionWrapper.AddHandler(value);
            remove => _callbackExceptionWrapper.RemoveHandler(value);
        }
        private EventingWrapper<CallbackExceptionEventArgs> _callbackExceptionWrapper;

        public event EventHandler<FlowControlEventArgs> FlowControl
        {
            add => _flowControlWrapper.AddHandler(value);
            remove => _flowControlWrapper.RemoveHandler(value);
        }
        private EventingWrapper<FlowControlEventArgs> _flowControlWrapper;

        public event EventHandler<ShutdownEventArgs> ModelShutdown
        {
            add
            {
                if (IsOpen)
                {
                    _modelShutdownWrapper.AddHandler(value);
                }
                else
                {
                    value(this, CloseReason);
                }
            }
            remove => _modelShutdownWrapper.RemoveHandler(value);
        }
        private EventingWrapper<ShutdownEventArgs> _modelShutdownWrapper;

        public event EventHandler<EventArgs> Recovery
        {
            add => _recoveryWrapper.AddHandler(value);
            remove => _recoveryWrapper.RemoveHandler(value);
        }
        private EventingWrapper<EventArgs> _recoveryWrapper;

        internal void RunRecoveryEventHandlers(object sender)
        {
            _recoveryWrapper.Invoke(sender, EventArgs.Empty);
        }

        public int ChannelNumber => ((Session)Session).ChannelNumber;

        public IBasicConsumer DefaultConsumer
        {
            get => ConsumerDispatcher.DefaultConsumer;
            set => ConsumerDispatcher.DefaultConsumer = value;
        }

        public bool IsClosed => !IsOpen;

        public bool IsOpen => CloseReason is null;

        public ulong NextPublishSeqNo { get; private set; }

        public ISession Session { get; private set; }

        protected void TakeOver(ModelBase other)
        {
            _basicAcksWrapper.Takeover(other._basicAcksWrapper);
            _basicNacksWrapper.Takeover(other._basicNacksWrapper);
            _basicRecoverOkWrapper.Takeover(other._basicRecoverOkWrapper);
            _basicReturnWrapper.Takeover(other._basicReturnWrapper);
            _callbackExceptionWrapper.Takeover(other._callbackExceptionWrapper);
            _flowControlWrapper.Takeover(other._flowControlWrapper);
            _modelShutdownWrapper.Takeover(other._modelShutdownWrapper);
            _recoveryWrapper.Takeover(other._recoveryWrapper);
        }

        public void Close(ushort replyCode, string replyText, bool abort)
        {
            _ = CloseAsync(new ShutdownEventArgs(ShutdownInitiator.Application, replyCode, replyText), abort);
        }

        private async Task CloseAsync(ShutdownEventArgs reason, bool abort)
        {
            var k = new ShutdownContinuation();
            ModelShutdown += k.OnConnectionShutdown;

            try
            {
                ConsumerDispatcher.Quiesce();
                if (SetCloseReason(reason))
                {
                    _Private_ChannelClose(reason.ReplyCode, reason.ReplyText, 0, 0);
                }

                k.Wait(TimeSpan.FromMilliseconds(10000));
                await ConsumerDispatcher.WaitForShutdownAsync().ConfigureAwait(false);
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
            finally
            {
                ModelShutdown -= k.OnConnectionShutdown;
            }
        }

        internal void ConnectionOpen(string virtualHost)
        {
            var k = new SimpleBlockingRpcContinuation();
            lock (_rpcLock)
            {
                Enqueue(k);
                try
                {
                    _Private_ConnectionOpen(virtualHost);
                }
                catch (AlreadyClosedException)
                {
                    // let continuation throw OperationInterruptedException,
                    // which is a much more suitable exception before connection
                    // negotiation finishes
                }
                k.GetReply(HandshakeContinuationTimeout);
            }
        }

        internal ConnectionSecureOrTune ConnectionSecureOk(byte[] response)
        {
            var k = new ConnectionStartRpcContinuation();
            lock (_rpcLock)
            {
                Enqueue(k);
                try
                {
                    _Private_ConnectionSecureOk(response);
                }
                catch (AlreadyClosedException)
                {
                    // let continuation throw OperationInterruptedException,
                    // which is a much more suitable exception before connection
                    // negotiation finishes
                }
                k.GetReply(HandshakeContinuationTimeout);
            }
            return k.m_result;
        }

        internal ConnectionSecureOrTune ConnectionStartOk(IDictionary<string, object> clientProperties, string mechanism, byte[] response, string locale)
        {
            var k = new ConnectionStartRpcContinuation();
            lock (_rpcLock)
            {
                Enqueue(k);
                try
                {
                    _Private_ConnectionStartOk(clientProperties, mechanism,
                        response, locale);
                }
                catch (AlreadyClosedException)
                {
                    // let continuation throw OperationInterruptedException,
                    // which is a much more suitable exception before connection
                    // negotiation finishes
                }
                k.GetReply(HandshakeContinuationTimeout);
            }
            return k.m_result;
        }

        protected abstract bool DispatchAsynchronous(in IncomingCommand cmd);

        protected void Enqueue(IRpcContinuation k)
        {
            if (IsOpen)
            {
                _continuationQueue.Enqueue(k);
            }
            else
            {
                k.HandleModelShutdown(CloseReason);
            }
        }

        internal void FinishClose()
        {
            var reason = CloseReason;
            if (reason != null)
            {
                Session.Close(reason);
            }

            m_connectionStartCell?.ContinueWithValue(null);
        }

        private void HandleCommand(in IncomingCommand cmd)
        {
            if (!DispatchAsynchronous(in cmd)) // Was asynchronous. Already processed. No need to process further.
            {
                _continuationQueue.Next().HandleCommand(in cmd);
            }
        }

        protected T ModelRpc<T>(MethodBase method) where T : MethodBase
        {
            var k = new SimpleBlockingRpcContinuation();
            var outgoingCommand = new OutgoingCommand(method);
            MethodBase baseResult;
            lock (_rpcLock)
            {
                TransmitAndEnqueue(outgoingCommand, k);
                baseResult = k.GetReply(ContinuationTimeout).Method;
            }

            if (baseResult is T result)
            {
                return result;
            }

            throw new UnexpectedMethodException(baseResult.ProtocolClassId, baseResult.ProtocolMethodId, baseResult.ProtocolMethodName);
        }

        protected void ModelSend(MethodBase method)
        {
            Session.Transmit(new OutgoingCommand(method));
        }

        protected void ModelSend(MethodBase method, ContentHeaderBase header, ReadOnlyMemory<byte> body)
        {
            if (!_flowControlBlock.IsSet)
            {
                _flowControlBlock.Wait();
            }

            Session.Transmit(new OutgoingContentCommand(method, header, body));
        }

        internal void OnCallbackException(CallbackExceptionEventArgs args)
        {
            _callbackExceptionWrapper.Invoke(this, args);
        }

        ///<summary>Broadcasts notification of the final shutdown of the model.</summary>
        ///<remarks>
        ///<para>
        ///Do not call anywhere other than at the end of OnSessionShutdown.
        ///</para>
        ///<para>
        ///Must not be called when m_closeReason is null, because
        ///otherwise there's a window when a new continuation could be
        ///being enqueued at the same time as we're broadcasting the
        ///shutdown event. See the definition of Enqueue() above.
        ///</para>
        ///</remarks>
        private void OnModelShutdown(ShutdownEventArgs reason)
        {
            _continuationQueue.HandleModelShutdown(reason);
            _modelShutdownWrapper.Invoke(this, reason);
            lock (_confirmLock)
            {
                if (_confirmsTaskCompletionSources?.Count > 0)
                {
                    var exception = new AlreadyClosedException(reason);
                    foreach (var confirmsTaskCompletionSource in _confirmsTaskCompletionSources)
                    {
                        confirmsTaskCompletionSource.TrySetException(exception);
                    }
                    _confirmsTaskCompletionSources.Clear();
                }
            }
            _flowControlBlock.Set();
        }

        private void OnSessionShutdown(object sender, ShutdownEventArgs reason)
        {
            ConsumerDispatcher.Quiesce();
            SetCloseReason(reason);
            OnModelShutdown(reason);
            ConsumerDispatcher.ShutdownAsync(reason).GetAwaiter().GetResult();
        }

        internal bool SetCloseReason(ShutdownEventArgs reason)
        {
            return System.Threading.Interlocked.CompareExchange(ref _closeReason, reason, null) is null;
        }

        public override string ToString()
            => Session.ToString();

        private void TransmitAndEnqueue(in OutgoingCommand cmd, IRpcContinuation k)
        {
            Enqueue(k);
            Session.Transmit(cmd);
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // dispose managed resources
                this.Abort();
            }

            // dispose unmanaged resources
        }

        public abstract void ConnectionTuneOk(ushort channelMax, uint frameMax, ushort heartbeat);

        public void HandleBasicAck(ulong deliveryTag, bool multiple)
        {
            if (!_basicAcksWrapper.IsEmpty)
            {
                var args = new BasicAckEventArgs
                {
                    DeliveryTag = deliveryTag,
                    Multiple = multiple
                };
                _basicAcksWrapper.Invoke(this, args);
            }

            HandleAckNack(deliveryTag, multiple, false);
        }

        public void HandleBasicNack(ulong deliveryTag, bool multiple, bool requeue)
        {
            if (!_basicNacksWrapper.IsEmpty)
            {
                var args = new BasicNackEventArgs
                {
                    DeliveryTag = deliveryTag,
                    Multiple = multiple,
                    Requeue = requeue
                };
                _basicNacksWrapper.Invoke(this, args);
            }

            HandleAckNack(deliveryTag, multiple, true);
        }

        protected void HandleAckNack(ulong deliveryTag, bool multiple, bool isNack)
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

                            if (_pendingDeliveryTags.First.Value == deliveryTag)
                            {
                                _pendingDeliveryTags.RemoveFirst();
                            }
                        }
                        else
                        {
                            _pendingDeliveryTags.Remove(deliveryTag);
                        }
                    }

                    _onlyAcksReceived = _onlyAcksReceived && !isNack;

                    if (_pendingDeliveryTags.Count == 0 && _confirmsTaskCompletionSources.Count > 0)
                    {
                        // Done, mark tasks
                        foreach (var confirmsTaskCompletionSource in _confirmsTaskCompletionSources)
                        {
                            confirmsTaskCompletionSource.TrySetResult(_onlyAcksReceived);
                        }
                        _confirmsTaskCompletionSources.Clear();
                        _onlyAcksReceived = true;
                    }
                }
            }
        }

        public void HandleBasicCancel(string consumerTag)
        {
            ConsumerDispatcher.HandleBasicCancel(consumerTag);
        }

        public void HandleBasicCancelOk(string consumerTag)
        {
            var k = (BasicConsumerRpcContinuation)_continuationQueue.Next();
            ConsumerDispatcher.HandleBasicCancelOk(consumerTag);
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        public void HandleBasicConsumeOk(string consumerTag)
        {
            var k = (BasicConsumerRpcContinuation)_continuationQueue.Next();
            k.m_consumerTag = consumerTag;
            ConsumerDispatcher.HandleBasicConsumeOk(k.m_consumer, consumerTag);
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        public virtual void HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body,
            byte[] rentedArray)
        {
            ConsumerDispatcher.HandleBasicDeliver(
                    consumerTag,
                    deliveryTag,
                    redelivered,
                    exchange,
                    routingKey,
                    basicProperties,
                    body,
                    rentedArray);
        }

        public void HandleBasicGetEmpty()
        {
            var k = (BasicGetRpcContinuation)_continuationQueue.Next();
            k.m_result = null;
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        public virtual void HandleBasicGetOk(ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            uint messageCount,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body,
            byte[] rentedArray)
        {
            var k = (BasicGetRpcContinuation)_continuationQueue.Next();
            k.m_result = new BasicGetResult(deliveryTag, redelivered, exchange, routingKey, messageCount, basicProperties, body, rentedArray);
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        public void HandleBasicRecoverOk()
        {
            var k = (SimpleBlockingRpcContinuation)_continuationQueue.Next();
            _basicRecoverOkWrapper.Invoke(this, EventArgs.Empty);
            k.HandleCommand(IncomingCommand.Empty);
        }

        public void HandleBasicReturn(ushort replyCode,
            string replyText,
            string exchange,
            string routingKey,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body,
            byte[] rentedArray)
        {
            if (!_basicReturnWrapper.IsEmpty)
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
                _basicReturnWrapper.Invoke(this, e);
            }
            ArrayPool<byte>.Shared.Return(rentedArray);
        }

        public void HandleChannelClose(ushort replyCode,
            string replyText,
            ushort classId,
            ushort methodId)
        {
            SetCloseReason(new ShutdownEventArgs(ShutdownInitiator.Peer,
                replyCode,
                replyText,
                classId,
                methodId));

            Session.Close(CloseReason, false);
            try
            {
                _Private_ChannelCloseOk();
            }
            finally
            {
                Session.Notify();
            }
        }

        public void HandleChannelCloseOk()
        {
            FinishClose();
        }

        public void HandleChannelFlow(bool active)
        {
            if (active)
            {
                _flowControlBlock.Set();
                _Private_ChannelFlowOk(active);
            }
            else
            {
                _flowControlBlock.Reset();
                _Private_ChannelFlowOk(active);
            }

            if (!_flowControlWrapper.IsEmpty)
            {
                _flowControlWrapper.Invoke(this, new FlowControlEventArgs(active));
            }
        }

        internal void HandleConnectionBlocked(string reason)
        {
            Session.Connection.HandleConnectionBlocked(reason);
        }

        public void HandleConnectionClose(ushort replyCode, string replyText, ushort classId, ushort methodId)
        {
            var reason = new ShutdownEventArgs(ShutdownInitiator.Peer, replyCode, replyText, classId, methodId);
            try
            {
                Session.Connection.InternalClose(reason);
                _Private_ConnectionCloseOk();
                SetCloseReason(Session.Connection.CloseReason);
            }
            catch (IOException)
            {
                // Ignored. We're only trying to be polite by sending
                // the close-ok, after all.
            }
            catch (AlreadyClosedException)
            {
                // Ignored. We're only trying to be polite by sending
                // the close-ok, after all.
            }
        }

        public void HandleConnectionSecure(byte[] challenge)
        {
            var k = (ConnectionStartRpcContinuation)_continuationQueue.Next();
            k.m_result = new ConnectionSecureOrTune
            {
                m_challenge = challenge
            };
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        public void HandleConnectionStart(byte versionMajor, byte versionMinor, IDictionary<string, object> serverProperties, byte[] mechanisms, byte[] locales)
        {
            if (m_connectionStartCell is null)
            {
                var reason = new ShutdownEventArgs(ShutdownInitiator.Library, Constants.CommandInvalid, "Unexpected Connection.Start");
                Session.Connection.Close(reason, false, Timeout.InfiniteTimeSpan);
            }
            var details = new ConnectionStartDetails
            {
                m_versionMajor = versionMajor,
                m_versionMinor = versionMinor,
                m_serverProperties = serverProperties,
                m_mechanisms = mechanisms,
                m_locales = locales
            };
            m_connectionStartCell.ContinueWithValue(details);
            m_connectionStartCell = null;
        }

        ///<summary>Handle incoming Connection.Tune
        ///methods.</summary>
        public void HandleConnectionTune(ushort channelMax, uint frameMax, ushort heartbeatInSeconds)
        {
            var k = (ConnectionStartRpcContinuation)_continuationQueue.Next();
            k.m_result = new ConnectionSecureOrTune
            {
                m_tuneDetails =
                {
                    m_channelMax = channelMax,
                    m_frameMax = frameMax,
                    m_heartbeatInSeconds = heartbeatInSeconds
                }
            };
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        public void HandleConnectionUnblocked()
        {
            Session.Connection.HandleConnectionUnblocked();
        }

        public void HandleQueueDeclareOk(string queue, uint messageCount, uint consumerCount)
        {
            var k = (QueueDeclareRpcContinuation)_continuationQueue.Next();
            k.m_result = new QueueDeclareOk(queue, messageCount, consumerCount);
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        public abstract void _Private_BasicCancel(string consumerTag, bool nowait);

        public abstract void _Private_BasicConsume(string queue, string consumerTag, bool noLocal, bool autoAck, bool exclusive, bool nowait, IDictionary<string, object> arguments);

        public abstract void _Private_BasicGet(string queue, bool autoAck);

        public abstract void _Private_BasicPublish(string exchange, string routingKey, bool mandatory, IBasicProperties basicProperties, ReadOnlyMemory<byte> body);

        public abstract void _Private_BasicPublishMemory(ReadOnlyMemory<byte> exchange, ReadOnlyMemory<byte> routingKey, bool mandatory, IBasicProperties basicProperties, ReadOnlyMemory<byte> body);

        public abstract void _Private_BasicRecover(bool requeue);

        public abstract void _Private_ChannelClose(ushort replyCode, string replyText, ushort classId, ushort methodId);

        public abstract void _Private_ChannelCloseOk();

        public abstract void _Private_ChannelFlowOk(bool active);

        public abstract void _Private_ChannelOpen();

        public abstract void _Private_ConfirmSelect(bool nowait);

        public abstract void _Private_ConnectionCloseOk();

        public abstract void _Private_ConnectionOpen(string virtualHost);

        public abstract void _Private_ConnectionSecureOk(byte[] response);

        public abstract void _Private_ConnectionStartOk(IDictionary<string, object> clientProperties, string mechanism, byte[] response, string locale);

        public abstract void _Private_UpdateSecret(byte[] @newSecret, string @reason);

        public abstract void _Private_ExchangeBind(string destination, string source, string routingKey, bool nowait, IDictionary<string, object> arguments);

        public abstract void _Private_ExchangeDeclare(string exchange, string type, bool passive, bool durable, bool autoDelete, bool @internal, bool nowait, IDictionary<string, object> arguments);

        public abstract void _Private_ExchangeDelete(string exchange, bool ifUnused, bool nowait);

        public abstract void _Private_ExchangeUnbind(string destination, string source, string routingKey, bool nowait, IDictionary<string, object> arguments);

        public abstract void _Private_QueueBind(string queue, string exchange, string routingKey, bool nowait, IDictionary<string, object> arguments);

        public abstract void _Private_QueueDeclare(string queue, bool passive, bool durable, bool exclusive, bool autoDelete, bool nowait, IDictionary<string, object> arguments);

        public abstract uint _Private_QueueDelete(string queue, bool ifUnused, bool ifEmpty, bool nowait);

        public abstract uint _Private_QueuePurge(string queue, bool nowait);

        public abstract void BasicAck(ulong deliveryTag, bool multiple);

        public void BasicCancel(string consumerTag)
        {
            var k = new BasicConsumerRpcContinuation { m_consumerTag = consumerTag };

            lock (_rpcLock)
            {
                Enqueue(k);
                _Private_BasicCancel(consumerTag, false);
                k.GetReply(ContinuationTimeout);
            }
        }

        public void BasicCancelNoWait(string consumerTag)
        {
            _Private_BasicCancel(consumerTag, true);
            ConsumerDispatcher.GetAndRemoveConsumer(consumerTag);
        }

        public string BasicConsume(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive, IDictionary<string, object> arguments, IBasicConsumer consumer)
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

            var k = new BasicConsumerRpcContinuation { m_consumer = consumer };

            lock (_rpcLock)
            {
                Enqueue(k);
                // Non-nowait. We have an unconventional means of getting
                // the RPC response, but a response is still expected.
                _Private_BasicConsume(queue, consumerTag, noLocal, autoAck, exclusive,
                    /*nowait:*/ false, arguments);
                k.GetReply(ContinuationTimeout);
            }
            string actualConsumerTag = k.m_consumerTag;

            return actualConsumerTag;
        }

        public BasicGetResult BasicGet(string queue, bool autoAck)
        {
            var k = new BasicGetRpcContinuation();
            lock (_rpcLock)
            {
                Enqueue(k);
                _Private_BasicGet(queue, autoAck);
                k.GetReply(ContinuationTimeout);
            }

            return k.m_result;
        }

        public abstract void BasicNack(ulong deliveryTag, bool multiple, bool requeue);

        private void AllocatePublishSeqNos(int count)
        {
            lock (_confirmLock)
            {
                for (int i = 0; i < count; i++)
                {
                    _pendingDeliveryTags.AddLast(NextPublishSeqNo++);
                }
            }
        }

        public void BasicPublish(string exchange, string routingKey, bool mandatory, IBasicProperties basicProperties, ReadOnlyMemory<byte> body)
        {
            if (routingKey is null)
            {
                throw new ArgumentNullException(nameof(routingKey));
            }

            if (NextPublishSeqNo > 0)
            {
                lock (_confirmLock)
                {
                    _pendingDeliveryTags.AddLast(NextPublishSeqNo++);
                }
            }

            _Private_BasicPublish(exchange,
                routingKey,
                mandatory,
                basicProperties ?? _emptyBasicProperties,
                body);
        }

        public void BasicPublish(CachedString exchange, CachedString routingKey, bool mandatory, IBasicProperties basicProperties, ReadOnlyMemory<byte> body)
        {
            if (NextPublishSeqNo > 0)
            {
                lock (_confirmLock)
                {
                    _pendingDeliveryTags.AddLast(NextPublishSeqNo++);
                }
            }

            _Private_BasicPublishMemory(exchange.Bytes, routingKey.Bytes, mandatory, basicProperties ?? _emptyBasicProperties, body);
        }

        public void UpdateSecret(string newSecret, string reason)
        {
            if (newSecret is null)
            {
                throw new ArgumentNullException(nameof(newSecret));
            }

            if (reason is null)
            {
                throw new ArgumentNullException(nameof(reason));
            }

            _Private_UpdateSecret(Encoding.UTF8.GetBytes(newSecret), reason);
        }

        public abstract void BasicQos(uint prefetchSize, ushort prefetchCount, bool global);

        public void BasicRecover(bool requeue)
        {
            var k = new SimpleBlockingRpcContinuation();

            lock (_rpcLock)
            {
                Enqueue(k);
                _Private_BasicRecover(requeue);
                k.GetReply(ContinuationTimeout);
            }
        }

        public abstract void BasicRecoverAsync(bool requeue);

        public abstract void BasicReject(ulong deliveryTag, bool requeue);

        public void ConfirmSelect()
        {
            if (NextPublishSeqNo == 0UL)
            {
                _confirmsTaskCompletionSources = new List<TaskCompletionSource<bool>>();
                NextPublishSeqNo = 1;
            }

            _Private_ConfirmSelect(false);
        }

        ///////////////////////////////////////////////////////////////////////////

        public abstract IBasicProperties CreateBasicProperties();
        public IBasicPublishBatch CreateBasicPublishBatch()
        {
            return new BasicPublishBatch(this);
        }

        public IBasicPublishBatch CreateBasicPublishBatch(int sizeHint)
        {
            return new BasicPublishBatch(this, sizeHint);
        }


        public void ExchangeBind(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            _Private_ExchangeBind(destination, source, routingKey, false, arguments);
        }

        public void ExchangeBindNoWait(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            _Private_ExchangeBind(destination, source, routingKey, true, arguments);
        }

        public void ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments)
        {
            _Private_ExchangeDeclare(exchange, type, false, durable, autoDelete, false, false, arguments);
        }

        public void ExchangeDeclareNoWait(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments)
        {
            _Private_ExchangeDeclare(exchange, type, false, durable, autoDelete, false, true, arguments);
        }

        public void ExchangeDeclarePassive(string exchange)
        {
            _Private_ExchangeDeclare(exchange, "", true, false, false, false, false, null);
        }

        public void ExchangeDelete(string exchange, bool ifUnused)
        {
            _Private_ExchangeDelete(exchange, ifUnused, false);
        }

        public void ExchangeDeleteNoWait(string exchange, bool ifUnused)
        {
            _Private_ExchangeDelete(exchange, ifUnused, true);
        }

        public void ExchangeUnbind(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            _Private_ExchangeUnbind(destination, source, routingKey, false, arguments);
        }

        public void ExchangeUnbindNoWait(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            _Private_ExchangeUnbind(destination, source, routingKey, true, arguments);
        }

        public void QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            _Private_QueueBind(queue, exchange, routingKey, false, arguments);
        }

        public void QueueBindNoWait(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            _Private_QueueBind(queue, exchange, routingKey, true, arguments);
        }

        public QueueDeclareOk QueueDeclare(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments)
        {
            return QueueDeclare(queue, false, durable, exclusive, autoDelete, arguments);
        }

        public void QueueDeclareNoWait(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments)
        {
            _Private_QueueDeclare(queue, false, durable, exclusive, autoDelete, true, arguments);
        }

        public QueueDeclareOk QueueDeclarePassive(string queue)
        {
            return QueueDeclare(queue, true, false, false, false, null);
        }

        public uint MessageCount(string queue)
        {
            QueueDeclareOk ok = QueueDeclarePassive(queue);
            return ok.MessageCount;
        }

        public uint ConsumerCount(string queue)
        {
            QueueDeclareOk ok = QueueDeclarePassive(queue);
            return ok.ConsumerCount;
        }

        public uint QueueDelete(string queue, bool ifUnused, bool ifEmpty)
        {
            return _Private_QueueDelete(queue, ifUnused, ifEmpty, false);
        }

        public void QueueDeleteNoWait(string queue, bool ifUnused, bool ifEmpty)
        {
            _Private_QueueDelete(queue, ifUnused, ifEmpty, true);
        }

        public uint QueuePurge(string queue)
        {
            return _Private_QueuePurge(queue, false);
        }

        public abstract void QueueUnbind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments);

        public abstract void TxCommit();

        public abstract void TxRollback();

        public abstract void TxSelect();

        private List<TaskCompletionSource<bool>> _confirmsTaskCompletionSources;

        public Task<bool> WaitForConfirmsAsync(CancellationToken token = default)
        {
            if (NextPublishSeqNo == 0UL)
            {
                throw new InvalidOperationException("Confirms not selected");
            }

            TaskCompletionSource<bool> tcs;
            lock (_confirmLock)
            {
                if (_pendingDeliveryTags.Count == 0)
                {
                    if (_onlyAcksReceived == false)
                    {
                        _onlyAcksReceived = true;
                        return Task.FromResult(false);
                    }
                    return Task.FromResult(true);
                }

                tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _confirmsTaskCompletionSources.Add(tcs);
            }

            if (!token.CanBeCanceled)
            {
                return tcs.Task;
            }

            return WaitForConfirmsWithTokenAsync(tcs, token);
        }

        private async Task<bool> WaitForConfirmsWithTokenAsync(TaskCompletionSource<bool> tcs, CancellationToken token)
        {
            CancellationTokenRegistration tokenRegistration =
#if NETSTANDARD
                token.Register(
#else
                token.UnsafeRegister(
#endif
                    state => ((TaskCompletionSource<bool>)state).TrySetCanceled(), tcs);

            try
            {
                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
#if NETSTANDARD
                tokenRegistration.Dispose();
#else
                await tokenRegistration.DisposeAsync().ConfigureAwait(false);
#endif
            }
        }

        public async Task WaitForConfirmsOrDieAsync(CancellationToken token = default)
        {
            try
            {
                bool onlyAcksReceived = await WaitForConfirmsAsync(token).ConfigureAwait(false);
                if (onlyAcksReceived)
                {
                    return;
                }

                await CloseAsync(
                    new ShutdownEventArgs(ShutdownInitiator.Application, Constants.ReplySuccess, "Nacks Received",
                        new IOException("nack received")),
                    false).ConfigureAwait(false);
            }
            catch (TaskCanceledException exception)
            {
                await CloseAsync(new ShutdownEventArgs(ShutdownInitiator.Application,
                        Constants.ReplySuccess,
                        "Timed out waiting for acks",
                        exception),
                    false).ConfigureAwait(false);
            }
        }

        internal void SendCommands(List<OutgoingContentCommand> commands)
        {
            _flowControlBlock.Wait();
            if (NextPublishSeqNo > 0)
            {
                AllocatePublishSeqNos(commands.Count);
            }
            Session.Transmit(commands);
        }

        private QueueDeclareOk QueueDeclare(string queue, bool passive, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments)
        {
            var k = new QueueDeclareRpcContinuation();
            lock (_rpcLock)
            {
                Enqueue(k);
                _Private_QueueDeclare(queue, passive, durable, exclusive, autoDelete, false, arguments);
                k.GetReply(ContinuationTimeout);
            }
            return k.m_result;
        }


        public class BasicConsumerRpcContinuation : SimpleBlockingRpcContinuation
        {
            public IBasicConsumer m_consumer;
            public string m_consumerTag;
        }

        public class BasicGetRpcContinuation : SimpleBlockingRpcContinuation
        {
            public BasicGetResult m_result;
        }

        public class ConnectionStartRpcContinuation : SimpleBlockingRpcContinuation
        {
            public ConnectionSecureOrTune m_result;
        }

        public class QueueDeclareRpcContinuation : SimpleBlockingRpcContinuation
        {
            public QueueDeclareOk m_result;
        }
    }
}
