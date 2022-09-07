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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.client.framing;
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

        private readonly RpcContinuationQueue _continuationQueue = new RpcContinuationQueue();
        private readonly ManualResetEventSlim _flowControlBlock = new ManualResetEventSlim(true);

        private readonly object _rpcLock = new object();
        private readonly object _confirmLock = new object();
        private readonly LinkedList<ulong> _pendingDeliveryTags = new LinkedList<ulong>();

        private bool _onlyAcksReceived = true;

        private ShutdownEventArgs _closeReason;
        public ShutdownEventArgs CloseReason => Volatile.Read(ref _closeReason);

        internal IConsumerDispatcher ConsumerDispatcher { get; }

        protected ModelBase(ConnectionConfig config, ISession session)
        {
            ContinuationTimeout = config.ContinuationTimeout;
            ConsumerDispatcher = config.DispatchConsumersAsync ?
                (IConsumerDispatcher)new AsyncConsumerDispatcher(this, config.DispatchConsumerConcurrency) :
                new ConsumerDispatcher(this, config.DispatchConsumerConcurrency);

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
        public TimeSpan ContinuationTimeout { get; set; }

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

        protected void ModelRpc<TMethod>(in TMethod method, ProtocolCommandId returnCommandId)
            where TMethod : struct, IOutgoingAmqpMethod
        {
            var k = new SimpleBlockingRpcContinuation();
            IncomingCommand reply;
            lock (_rpcLock)
            {
                Enqueue(k);
                Session.Transmit(in method);
                k.GetReply(ContinuationTimeout, out reply);
            }

            reply.ReturnMethodBuffer();

            if (reply.CommandId != returnCommandId)
            {
                throw new UnexpectedMethodException(reply.CommandId, returnCommandId);
            }
        }

        protected TReturn ModelRpc<TMethod, TReturn>(in TMethod method, ProtocolCommandId returnCommandId, Func<ReadOnlyMemory<byte>, TReturn> createFunc)
            where TMethod : struct, IOutgoingAmqpMethod
        {
            var k = new SimpleBlockingRpcContinuation();
            IncomingCommand reply;

            lock (_rpcLock)
            {
                Enqueue(k);
                Session.Transmit(in method);
                k.GetReply(ContinuationTimeout, out reply);
            }

            if (reply.CommandId != returnCommandId)
            {
                reply.ReturnMethodBuffer();
                throw new UnexpectedMethodException(reply.CommandId, returnCommandId);
            }

            var returnValue = createFunc(reply.MethodBytes);
            reply.ReturnMethodBuffer();
            return returnValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ModelSend<T>(in T method) where T : struct, IOutgoingAmqpMethod
        {
            Session.Transmit(in method);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ModelSend<TMethod, THeader>(in TMethod method, in THeader header, ReadOnlyMemory<byte> body)
            where TMethod : struct, IOutgoingAmqpMethod
            where THeader : IAmqpHeader
        {
            if (!_flowControlBlock.IsSet)
            {
                _flowControlBlock.Wait();
            }
            Session.Transmit(in method, in header, body);
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

        protected void HandleBasicAck(in IncomingCommand cmd)
        {
            var ack = new BasicAck(cmd.MethodBytes.Span);
            cmd.ReturnMethodBuffer();
            if (!_basicAcksWrapper.IsEmpty)
            {
                var args = new BasicAckEventArgs
                {
                    DeliveryTag = ack._deliveryTag,
                    Multiple = ack._multiple
                };
                _basicAcksWrapper.Invoke(this, args);
            }

            HandleAckNack(ack._deliveryTag, ack._multiple, false);
        }

        protected void HandleBasicNack(in IncomingCommand cmd)
        {
            var nack = new BasicNack(cmd.MethodBytes.Span);
            cmd.ReturnMethodBuffer();
            if (!_basicNacksWrapper.IsEmpty)
            {
                var args = new BasicNackEventArgs
                {
                    DeliveryTag = nack._deliveryTag,
                    Multiple = nack._multiple,
                    Requeue = nack._requeue
                };
                _basicNacksWrapper.Invoke(this, args);
            }

            HandleAckNack(nack._deliveryTag, nack._multiple, true);
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

        protected void HandleBasicCancel(in IncomingCommand cmd)
        {
            var consumerTag = new Client.Framing.Impl.BasicCancel(cmd.MethodBytes.Span)._consumerTag;
            cmd.ReturnMethodBuffer();
            ConsumerDispatcher.HandleBasicCancel(consumerTag);
        }

        protected void HandleBasicCancelOk(in IncomingCommand cmd)
        {
            var k = (BasicConsumerRpcContinuation)_continuationQueue.Next();
            var consumerTag = new Client.Framing.Impl.BasicCancelOk(cmd.MethodBytes.Span)._consumerTag;
            cmd.ReturnMethodBuffer();
            ConsumerDispatcher.HandleBasicCancelOk(consumerTag);
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        protected void HandleBasicConsumeOk(in IncomingCommand cmd)
        {
            var consumerTag = new Client.Framing.Impl.BasicConsumeOk(cmd.MethodBytes.Span)._consumerTag;
            cmd.ReturnMethodBuffer();
            var k = (BasicConsumerRpcContinuation)_continuationQueue.Next();
            k.m_consumerTag = consumerTag;
            ConsumerDispatcher.HandleBasicConsumeOk(k.m_consumer, consumerTag);
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        protected void HandleBasicDeliver(in IncomingCommand cmd)
        {
            var method = new Client.Framing.Impl.BasicDeliver(cmd.MethodBytes.Span);
            cmd.ReturnMethodBuffer();
            var header = new ReadOnlyBasicProperties(cmd.HeaderBytes.Span);
            cmd.ReturnHeaderBuffer();

            ConsumerDispatcher.HandleBasicDeliver(
                    method._consumerTag,
                    AdjustDeliveryTag(method._deliveryTag),
                    method._redelivered,
                    method._exchange,
                    method._routingKey,
                    header,
                    cmd.Body,
                    cmd.TakeoverBody());
        }

        protected void HandleBasicGetOk(in IncomingCommand cmd)
        {
            var method = new BasicGetOk(cmd.MethodBytes.Span);
            cmd.ReturnMethodBuffer();
            var header = new ReadOnlyBasicProperties(cmd.HeaderBytes.Span);
            cmd.ReturnHeaderBuffer();

            var k = (BasicGetRpcContinuation)_continuationQueue.Next();
            k.m_result = new BasicGetResult(
                AdjustDeliveryTag(method._deliveryTag),
                method._redelivered,
                method._exchange,
                method._routingKey,
                method._messageCount,
                header,
                cmd.Body,
                cmd.TakeoverBody());
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        protected virtual ulong AdjustDeliveryTag(ulong deliveryTag)
        {
            return deliveryTag;
        }

        protected void HandleBasicGetEmpty()
        {
            var k = (BasicGetRpcContinuation)_continuationQueue.Next();
            k.m_result = null;
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        protected void HandleBasicRecoverOk()
        {
            var k = (SimpleBlockingRpcContinuation)_continuationQueue.Next();
            _basicRecoverOkWrapper.Invoke(this, EventArgs.Empty);
            k.HandleCommand(IncomingCommand.Empty);
        }

        protected void HandleBasicReturn(in IncomingCommand cmd)
        {
            if (!_basicReturnWrapper.IsEmpty)
            {
                var basicReturn = new BasicReturn(cmd.MethodBytes.Span);
                var e = new BasicReturnEventArgs
                {
                    ReplyCode = basicReturn._replyCode,
                    ReplyText = basicReturn._replyText,
                    Exchange = basicReturn._exchange,
                    RoutingKey = basicReturn._routingKey,
                    BasicProperties = new ReadOnlyBasicProperties(cmd.HeaderBytes.Span),
                    Body = cmd.Body
                };
                _basicReturnWrapper.Invoke(this, e);
            }
            cmd.ReturnMethodBuffer();
            cmd.ReturnHeaderBuffer();
            ArrayPool<byte>.Shared.Return(cmd.TakeoverBody());
        }

        protected void HandleChannelClose(in IncomingCommand cmd)
        {
            var channelClose = new ChannelClose(cmd.MethodBytes.Span);
            cmd.ReturnMethodBuffer();
            SetCloseReason(new ShutdownEventArgs(ShutdownInitiator.Peer,
                channelClose._replyCode,
                channelClose._replyText,
                channelClose._classId,
                channelClose._methodId));

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

        protected void HandleChannelCloseOk()
        {
            FinishClose();
        }

        protected void HandleChannelFlow(in IncomingCommand cmd)
        {
            var active = new ChannelFlow(cmd.MethodBytes.Span)._active;
            cmd.ReturnMethodBuffer();
            if (active)
            {
                _flowControlBlock.Set();
            }
            else
            {
                _flowControlBlock.Reset();
            }

            _Private_ChannelFlowOk(active);

            if (!_flowControlWrapper.IsEmpty)
            {
                _flowControlWrapper.Invoke(this, new FlowControlEventArgs(active));
            }
        }

        protected void HandleConnectionBlocked(in IncomingCommand cmd)
        {
            var reason = new ConnectionBlocked(cmd.MethodBytes.Span)._reason;
            cmd.ReturnMethodBuffer();
            Session.Connection.HandleConnectionBlocked(reason);
        }

        protected void HandleConnectionClose(in IncomingCommand cmd)
        {
            var method = new ConnectionClose(cmd.MethodBytes.Span);
            cmd.ReturnMethodBuffer();
            var reason = new ShutdownEventArgs(ShutdownInitiator.Peer, method._replyCode, method._replyText, method._classId, method._methodId);
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

        protected void HandleConnectionSecure(in IncomingCommand cmd)
        {
            var challenge = new ConnectionSecure(cmd.MethodBytes.Span)._challenge;
            cmd.ReturnMethodBuffer();
            var k = (ConnectionStartRpcContinuation)_continuationQueue.Next();
            k.m_result = new ConnectionSecureOrTune
            {
                m_challenge = challenge
            };
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        protected void HandleConnectionStart(in IncomingCommand cmd)
        {
            if (m_connectionStartCell is null)
            {
                var reason = new ShutdownEventArgs(ShutdownInitiator.Library, Constants.CommandInvalid, "Unexpected Connection.Start");
                Session.Connection.Close(reason, false, InternalConstants.DefaultConnectionCloseTimeout);
            }

            var method = new ConnectionStart(cmd.MethodBytes.Span);
            cmd.ReturnMethodBuffer();
            var details = new ConnectionStartDetails
            {
                m_versionMajor = method._versionMajor,
                m_versionMinor = method._versionMinor,
                m_serverProperties = method._serverProperties,
                m_mechanisms = method._mechanisms,
                m_locales = method._locales
            };
            m_connectionStartCell.ContinueWithValue(details);
            m_connectionStartCell = null;
        }

        protected void HandleConnectionTune(in IncomingCommand cmd)
        {
            var connectionTune = new ConnectionTune(cmd.MethodBytes.Span);
            cmd.ReturnMethodBuffer();
            var k = (ConnectionStartRpcContinuation)_continuationQueue.Next();
            k.m_result = new ConnectionSecureOrTune
            {
                m_tuneDetails =
                {
                    m_channelMax = connectionTune._channelMax,
                    m_frameMax = connectionTune._frameMax,
                    m_heartbeatInSeconds = connectionTune._heartbeat
                }
            };
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        protected void HandleConnectionUnblocked()
        {
            Session.Connection.HandleConnectionUnblocked();
        }

        protected void HandleQueueDeclareOk(in IncomingCommand cmd)
        {
            var method = new Client.Framing.Impl.QueueDeclareOk(cmd.MethodBytes.Span);
            cmd.ReturnMethodBuffer();
            var k = (QueueDeclareRpcContinuation)_continuationQueue.Next();
            k.m_result = new QueueDeclareOk(method._queue, method._messageCount, method._consumerCount);
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        public abstract void _Private_BasicCancel(string consumerTag, bool nowait);

        public abstract void _Private_BasicConsume(string queue, string consumerTag, bool noLocal, bool autoAck, bool exclusive, bool nowait, IDictionary<string, object> arguments);

        public abstract void _Private_BasicGet(string queue, bool autoAck);

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

        public void BasicPublish<TProperties>(string exchange, string routingKey, in TProperties basicProperties, ReadOnlyMemory<byte> body, bool mandatory)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            if (NextPublishSeqNo > 0)
            {
                lock (_confirmLock)
                {
                    _pendingDeliveryTags.AddLast(NextPublishSeqNo++);
                }
            }

            var cmd = new BasicPublish(exchange, routingKey, mandatory, default);
            ModelSend(in cmd, in basicProperties, body);
        }

        public void BasicPublish<TProperties>(CachedString exchange, CachedString routingKey, in TProperties basicProperties, ReadOnlyMemory<byte> body, bool mandatory)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            if (NextPublishSeqNo > 0)
            {
                lock (_confirmLock)
                {
                    _pendingDeliveryTags.AddLast(NextPublishSeqNo++);
                }
            }

            var cmd = new BasicPublishMemory(exchange.Bytes, routingKey.Bytes, mandatory, default);
            ModelSend(in cmd, in basicProperties, body);
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
                    new ShutdownEventArgs(ShutdownInitiator.Library, Constants.ReplySuccess, "Nacks Received",
                        new IOException("nack received")),
                    false).ConfigureAwait(false);
            }
            catch (TaskCanceledException exception)
            {
                await CloseAsync(new ShutdownEventArgs(ShutdownInitiator.Library,
                        Constants.ReplySuccess,
                        "Timed out waiting for acks",
                        exception),
                    false).ConfigureAwait(false);
            }
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
