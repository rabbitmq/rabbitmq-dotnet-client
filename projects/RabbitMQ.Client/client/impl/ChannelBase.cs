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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.client.impl;
using RabbitMQ.Client.ConsumerDispatching;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    internal abstract class ChannelBase : IChannel, IRecoverable
    {
        ///<summary>Only used to kick-start a connection open
        ///sequence. See <see cref="Connection.OpenAsync"/> </summary>
        internal TaskCompletionSource<ConnectionStartDetails> m_connectionStartCell;
        private Exception m_connectionStartException = null;

        // AMQP only allows one RPC operation to be active at a time. 
        protected readonly SemaphoreSlim _rpcSemaphore = new SemaphoreSlim(1, 1);
        private readonly RpcContinuationQueue _continuationQueue = new RpcContinuationQueue();
        private readonly ManualResetEventSlim _flowControlBlock = new ManualResetEventSlim(true);

        private readonly object _confirmLock = new object();
        private readonly LinkedList<ulong> _pendingDeliveryTags = new LinkedList<ulong>();

        private bool _onlyAcksReceived = true;

        private ShutdownEventArgs _closeReason;
        public ShutdownEventArgs CloseReason => Volatile.Read(ref _closeReason);

        internal readonly IConsumerDispatcher ConsumerDispatcher;

        protected ChannelBase(ConnectionConfig config, ISession session)
        {
            ContinuationTimeout = config.ContinuationTimeout;

            if (config.DispatchConsumersAsync)
            {
                ConsumerDispatcher = new AsyncConsumerDispatcher(this, config.DispatchConsumerConcurrency);
            }
            else
            {
                ConsumerDispatcher = new ConsumerDispatcher(this, config.DispatchConsumerConcurrency);
            }

            Action<Exception, string> onException = (exception, context) =>
                OnCallbackException(CallbackExceptionEventArgs.Build(exception, context));
            _basicAcksWrapper = new EventingWrapper<BasicAckEventArgs>("OnBasicAck", onException);
            _basicNacksWrapper = new EventingWrapper<BasicNackEventArgs>("OnBasicNack", onException);
            _basicReturnWrapper = new EventingWrapper<BasicReturnEventArgs>("OnBasicReturn", onException);
            _callbackExceptionWrapper =
                new EventingWrapper<CallbackExceptionEventArgs>(string.Empty, (exception, context) => { });
            _flowControlWrapper = new EventingWrapper<FlowControlEventArgs>("OnFlowControl", onException);
            _channelShutdownWrapper = new EventingWrapper<ShutdownEventArgs>("OnChannelShutdown", onException);
            _recoveryWrapper = new EventingWrapper<EventArgs>("OnChannelRecovery", onException);
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

        public event EventHandler<ShutdownEventArgs> ChannelShutdown
        {
            add
            {
                if (IsOpen)
                {
                    _channelShutdownWrapper.AddHandler(value);
                }
                else
                {
                    value(this, CloseReason);
                }
            }
            remove => _channelShutdownWrapper.RemoveHandler(value);
        }

        private EventingWrapper<ShutdownEventArgs> _channelShutdownWrapper;

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

        // TODO add private bool for Confirm mode
        public ulong NextPublishSeqNo { get; private set; }

        public string CurrentQueue { get; private set; }

        public ISession Session { get; private set; }

        public Exception ConnectionStartException => m_connectionStartException;

        public void MaybeSetConnectionStartException(Exception ex)
        {
            if (m_connectionStartCell != null)
            {
                m_connectionStartException = ex;
            }
        }

        protected void TakeOver(ChannelBase other)
        {
            _basicAcksWrapper.Takeover(other._basicAcksWrapper);
            _basicNacksWrapper.Takeover(other._basicNacksWrapper);
            _basicReturnWrapper.Takeover(other._basicReturnWrapper);
            _callbackExceptionWrapper.Takeover(other._callbackExceptionWrapper);
            _flowControlWrapper.Takeover(other._flowControlWrapper);
            _channelShutdownWrapper.Takeover(other._channelShutdownWrapper);
            _recoveryWrapper.Takeover(other._recoveryWrapper);
        }

        public void Close(ushort replyCode, string replyText, bool abort)
        {
            var reason = new ShutdownEventArgs(ShutdownInitiator.Application, replyCode, replyText);
            var k = new ShutdownContinuation();
            ChannelShutdown += k.OnConnectionShutdown;

            try
            {
                ConsumerDispatcher.Quiesce();

                if (SetCloseReason(reason))
                {
                    _Private_ChannelClose(reason.ReplyCode, reason.ReplyText, reason.ClassId, reason.MethodId);
                }

                k.Wait(TimeSpan.FromMilliseconds(10000));

                ConsumerDispatcher.WaitForShutdown();
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
                ChannelShutdown -= k.OnConnectionShutdown;
            }
        }

        public Task CloseAsync(ushort replyCode, string replyText, bool abort)
        {
            var args = new ShutdownEventArgs(ShutdownInitiator.Application, replyCode, replyText);
            return CloseAsync(args, abort);
        }

        public async Task CloseAsync(ShutdownEventArgs args, bool abort)
        {
            using var k = new ChannelCloseAsyncRpcContinuation(ContinuationTimeout);
            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                ChannelShutdown += k.OnConnectionShutdown;
                Enqueue(k);
                ConsumerDispatcher.Quiesce();

                if (SetCloseReason(args))
                {
                    var method = new ChannelClose(
                        args.ReplyCode, args.ReplyText, args.ClassId, args.MethodId);
                    await ModelSendAsync(method)
                        .ConfigureAwait(false);
                }

                bool result = await k;
                Debug.Assert(result);

                await ConsumerDispatcher.WaitForShutdownAsync()
                    .ConfigureAwait(false);
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
                _rpcSemaphore.Release();
                ChannelShutdown -= k.OnConnectionShutdown;
            }
        }

        internal async ValueTask ConnectionOpenAsync(string virtualHost, CancellationToken _)
        {
            var m = new ConnectionOpen(virtualHost);
            // TODO linked cancellation token
            await ModelSendAsync(m)
                .TimeoutAfter(HandshakeContinuationTimeout)
                .ConfigureAwait(false);
        }

        internal async ValueTask<ConnectionSecureOrTune> ConnectionSecureOkAsync(byte[] response)
        {
            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                using var k = new ConnectionSecureOrTuneAsyncRpcContinuation(ContinuationTimeout);
                Enqueue(k);

                try
                {
                    var method = new ConnectionSecureOk(response);
                    await ModelSendAsync(method)
                        .ConfigureAwait(false);
                }
                catch (AlreadyClosedException)
                {
                    // let continuation throw OperationInterruptedException,
                    // which is a much more suitable exception before connection
                    // negotiation finishes
                }

                return await k;
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

        internal async ValueTask<ConnectionSecureOrTune> ConnectionStartOkAsync(
            IDictionary<string, object> clientProperties, string mechanism, byte[] response,
            string locale)
        {
            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                using var k = new ConnectionSecureOrTuneAsyncRpcContinuation(ContinuationTimeout);
                Enqueue(k);

                try
                {
                    var method = new ConnectionStartOk(clientProperties, mechanism, response, locale);
                    await ModelSendAsync(method)
                        .ConfigureAwait(false);
                }
                catch (AlreadyClosedException)
                {
                    // let continuation throw OperationInterruptedException,
                    // which is a much more suitable exception before connection
                    // negotiation finishes
                }

                return await k;
            }
            finally
            {
                _rpcSemaphore.Release();
            }
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
                k.HandleChannelShutdown(CloseReason);
            }
        }

        internal async Task<IChannel> OpenAsync()
        {
            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            using var k = new ChannelOpenAsyncRpcContinuation(ContinuationTimeout);
            try
            {
                Enqueue(k);

                var method = new ChannelOpen();
                await ModelSendAsync(method)
                    .ConfigureAwait(false);

                bool result = await k;
                Debug.Assert(result);
                return this;
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

        internal void FinishClose()
        {
            var reason = CloseReason;
            if (reason != null)
            {
                Session.Close(reason);
            }

            m_connectionStartCell?.TrySetResult(null);
        }

        private void HandleCommand(in IncomingCommand cmd)
        {
            if (!DispatchAsynchronous(in cmd)) // Was asynchronous. Already processed. No need to process further.
            {
                IRpcContinuation c = _continuationQueue.Next();
                c.HandleCommand(in cmd);
            }
        }

        protected void ChannelRpc<TMethod>(in TMethod method, ProtocolCommandId returnCommandId)
            where TMethod : struct, IOutgoingAmqpMethod
        {
            var k = new SimpleBlockingRpcContinuation();
            IncomingCommand reply = default;
            _rpcSemaphore.Wait();
            try
            {
                Enqueue(k);
                Session.Transmit(in method);
                k.GetReply(ContinuationTimeout, out reply);

                if (reply.CommandId != returnCommandId)
                {
                    throw new UnexpectedMethodException(reply.CommandId, returnCommandId);
                }
            }
            finally
            {
                reply.ReturnBuffers();
                _rpcSemaphore.Release();
            }
        }

        protected TReturn ChannelRpc<TMethod, TReturn>(in TMethod method, ProtocolCommandId returnCommandId, Func<RentedMemory, TReturn> createFunc)
            where TMethod : struct, IOutgoingAmqpMethod
        {
            IncomingCommand reply = default;
            try
            {
                var k = new SimpleBlockingRpcContinuation();

                _rpcSemaphore.Wait();
                try
                {
                    Enqueue(k);
                    Session.Transmit(in method);
                    k.GetReply(ContinuationTimeout, out reply);
                }
                finally
                {
                    _rpcSemaphore.Release();
                }

                if (reply.CommandId != returnCommandId)
                {
                    throw new UnexpectedMethodException(reply.CommandId, returnCommandId);
                }

                return createFunc(reply.Method);
            }
            finally
            {
                reply.ReturnBuffers();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ChannelSend<T>(in T method) where T : struct, IOutgoingAmqpMethod
        {
            Session.Transmit(in method);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected ValueTask ModelSendAsync<T>(in T method) where T : struct, IOutgoingAmqpMethod
        {
            return Session.TransmitAsync(in method);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ChannelSend<TMethod, THeader>(in TMethod method, in THeader header, ReadOnlyMemory<byte> body)
            where TMethod : struct, IOutgoingAmqpMethod
            where THeader : IAmqpHeader
        {
            if (!_flowControlBlock.IsSet)
            {
                _flowControlBlock.Wait();
            }

            Session.Transmit(in method, in header, body);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected ValueTask ModelSendAsync<TMethod, THeader>(in TMethod method, in THeader header,
            ReadOnlyMemory<byte> body)
            where TMethod : struct, IOutgoingAmqpMethod
            where THeader : IAmqpHeader
        {
            if (!_flowControlBlock.IsSet)
            {
                _flowControlBlock.Wait();
            }

            return Session.TransmitAsync(in method, in header, body);
        }

        internal void OnCallbackException(CallbackExceptionEventArgs args)
        {
            _callbackExceptionWrapper.Invoke(this, args);
        }

        ///<summary>Broadcasts notification of the final shutdown of the channel.</summary>
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
        private void OnChannelShutdown(ShutdownEventArgs reason)
        {
            _continuationQueue.HandleChannelShutdown(reason);
            _channelShutdownWrapper.Invoke(this, reason);
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
            OnChannelShutdown(reason);
            ConsumerDispatcher.Shutdown(reason);
        }

        internal bool SetCloseReason(ShutdownEventArgs reason)
        {
            // NB: this ensures that Close is only called once on a channel
            return Interlocked.CompareExchange(ref _closeReason, reason, null) is null;
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
                // TODO exception?
                if (IsOpen)
                {
                    throw new InvalidOperationException("Channel must be closed before calling Dispose!");
                }
                ConsumerDispatcher.Dispose();
                _rpcSemaphore.Dispose();
            }

            // dispose unmanaged resources
        }

        public abstract void ConnectionTuneOk(ushort channelMax, uint frameMax, ushort heartbeat);

        protected void HandleBasicAck(in IncomingCommand cmd)
        {
            try
            {
                var ack = new BasicAck(cmd.MethodSpan);
                if (!_basicAcksWrapper.IsEmpty)
                {
                    var args = new BasicAckEventArgs(ack._deliveryTag, ack._multiple);
                    _basicAcksWrapper.Invoke(this, args);
                }

                HandleAckNack(ack._deliveryTag, ack._multiple, false);
            }
            finally
            {
                cmd.ReturnBuffers();
            }
        }

        protected void HandleBasicNack(in IncomingCommand cmd)
        {
            try
            {
                var nack = new BasicNack(cmd.MethodSpan);
                if (!_basicNacksWrapper.IsEmpty)
                {
                    var args = new BasicNackEventArgs(
                        nack._deliveryTag, nack._multiple, nack._requeue);
                    _basicNacksWrapper.Invoke(this, args);
                }

                HandleAckNack(nack._deliveryTag, nack._multiple, true);
            }
            finally
            {
                cmd.ReturnBuffers();
            }
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
            try
            {
                var consumerTag = new Client.Framing.Impl.BasicCancel(cmd.MethodSpan)._consumerTag;
                ConsumerDispatcher.HandleBasicCancel(consumerTag);
            }
            finally
            {
                cmd.ReturnBuffers();
            }
        }

        protected bool HandleBasicCancelOk(in IncomingCommand cmd)
        {
            if (_continuationQueue.TryPeek<BasicConsumeRpcContinuation>(out var k))
            {
                try
                {
                    _continuationQueue.Next();
                    string consumerTag = new Client.Framing.Impl.BasicCancelOk(cmd.MethodSpan)._consumerTag;
                    ConsumerDispatcher.HandleBasicCancelOk(consumerTag);
                    k.HandleCommand(IncomingCommand.Empty); // release the continuation.
                    return true;
                }
                finally
                {
                    cmd.ReturnBuffers();
                }
            }
            else
            {
                return false;
            }
        }

        protected bool HandleBasicConsumeOk(in IncomingCommand cmd)
        {
            if (_continuationQueue.TryPeek<BasicConsumeRpcContinuation>(out var k))
            {
                try
                {
                    _continuationQueue.Next();
                    var consumerTag = new Client.Framing.Impl.BasicConsumeOk(cmd.MethodSpan)._consumerTag;
                    k.m_consumerTag = consumerTag;
                    ConsumerDispatcher.HandleBasicConsumeOk(k.m_consumer, consumerTag);
                    k.HandleCommand(IncomingCommand.Empty); // release the continuation.
                    return true;
                }
                finally
                {
                    cmd.ReturnBuffers();
                }
            }
            else
            {
                return false;
            }
        }

        protected void HandleBasicDeliver(in IncomingCommand cmd)
        {
            try
            {
                var method = new Client.Framing.Impl.BasicDeliver(cmd.MethodSpan);
                var header = new ReadOnlyBasicProperties(cmd.HeaderSpan);
                ConsumerDispatcher.HandleBasicDeliver(
                        method._consumerTag,
                        AdjustDeliveryTag(method._deliveryTag),
                        method._redelivered,
                        method._exchange,
                        method._routingKey,
                        header,
                        cmd.Body);
            }
            finally
            {
                /*
                 * Note: do not return the Body as it is necessary for handling
                 * the Basic.Deliver method by client code
                 */
                cmd.ReturnMethodAndHeaderBuffers();
            }
        }

        protected bool HandleBasicGetOk(in IncomingCommand cmd)
        {
            if (_continuationQueue.TryPeek<BasicGetRpcContinuation>(out var k))
            {
                try
                {
                    var method = new BasicGetOk(cmd.MethodSpan);
                    var header = new ReadOnlyBasicProperties(cmd.HeaderSpan);
                    _continuationQueue.Next();
                    k.m_result = new BasicGetResult(
                        AdjustDeliveryTag(method._deliveryTag),
                        method._redelivered,
                        method._exchange,
                        method._routingKey,
                        method._messageCount,
                        header,
                        cmd.Body.ToArray());
                    k.HandleCommand(IncomingCommand.Empty); // release the continuation.
                    return true;
                }
                finally
                {
                    // Note: since we copy the body buffer above, we want to return all buffers here
                    cmd.ReturnBuffers();
                }
            }
            else
            {
                return false;
            }
        }

        protected virtual ulong AdjustDeliveryTag(ulong deliveryTag)
        {
            return deliveryTag;
        }

        protected bool HandleBasicGetEmpty(in IncomingCommand cmd)
        {
            if (_continuationQueue.TryPeek<BasicGetRpcContinuation>(out var k))
            {
                try
                {
                    _continuationQueue.Next();
                    k.m_result = null;
                    k.HandleCommand(IncomingCommand.Empty); // release the continuation.
                    return true;
                }
                finally
                {
                    cmd.ReturnBuffers();
                }
            }
            else
            {
                return false;
            }
        }

        protected void HandleBasicReturn(in IncomingCommand cmd)
        {
            try
            {
                if (!_basicReturnWrapper.IsEmpty)
                {
                    var basicReturn = new BasicReturn(cmd.MethodSpan);
                    var e = new BasicReturnEventArgs(basicReturn._replyCode, basicReturn._replyText,
                        basicReturn._exchange, basicReturn._routingKey,
                        new ReadOnlyBasicProperties(cmd.HeaderSpan), cmd.Body.Memory);
                    _basicReturnWrapper.Invoke(this, e);
                }
            }
            finally
            {
                // Note: we can return all the buffers here since the event has been invoked and has returned
                cmd.ReturnBuffers();
            }
        }

        protected void HandleChannelClose(in IncomingCommand cmd)
        {
            try
            {
                var channelClose = new ChannelClose(cmd.MethodSpan);
                SetCloseReason(new ShutdownEventArgs(ShutdownInitiator.Peer,
                    channelClose._replyCode,
                    channelClose._replyText,
                    channelClose._classId,
                    channelClose._methodId));

                Session.Close(CloseReason, false);

                _Private_ChannelCloseOk();
            }
            finally
            {
                cmd.ReturnBuffers();
                Session.Notify();
            }
        }

        protected void HandleChannelCloseOk(in IncomingCommand cmd)
        {
            try
            {
                /*
                 * Note:
                 * This call _must_ come before completing the async continuation
                 */
                FinishClose();

                if (_continuationQueue.TryPeek<ChannelCloseAsyncRpcContinuation>(out var k))
                {
                    _continuationQueue.Next();
                    k.HandleCommand(cmd);
                }
            }
            finally
            {
                cmd.ReturnBuffers();
            }
        }

        protected void HandleChannelFlow(in IncomingCommand cmd)
        {
            try
            {
                var active = new ChannelFlow(cmd.MethodSpan)._active;
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
            finally
            {
                cmd.ReturnBuffers();
            }
        }

        protected void HandleConnectionBlocked(in IncomingCommand cmd)
        {
            try
            {
                var reason = new ConnectionBlocked(cmd.MethodSpan)._reason;
                Session.Connection.HandleConnectionBlocked(reason);
            }
            finally
            {
                cmd.ReturnBuffers();
            }
        }

        protected void HandleConnectionClose(in IncomingCommand cmd)
        {
            try
            {
                var method = new ConnectionClose(cmd.MethodSpan);
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
            finally
            {
                cmd.ReturnBuffers();
            }
        }

        protected void HandleConnectionSecure(in IncomingCommand cmd)
        {
            using var k = (ConnectionSecureOrTuneAsyncRpcContinuation)_continuationQueue.Next();
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        protected void HandleConnectionStart(in IncomingCommand cmd)
        {
            try
            {
                if (m_connectionStartCell is null)
                {
                    var reason = new ShutdownEventArgs(ShutdownInitiator.Library, Constants.CommandInvalid, "Unexpected Connection.Start");
                    // TODO async!
                    Session.Connection.CloseAsync(reason, false, InternalConstants.DefaultConnectionCloseTimeout).EnsureCompleted();
                }
                else
                {
                    var method = new ConnectionStart(cmd.MethodSpan);
                    var details = new ConnectionStartDetails
                    {
                        m_versionMajor = method._versionMajor,
                        m_versionMinor = method._versionMinor,
                        m_serverProperties = method._serverProperties,
                        m_mechanisms = method._mechanisms,
                        m_locales = method._locales
                    };
                    m_connectionStartCell.SetResult(details);
                    m_connectionStartCell = null;
                }
            }
            finally
            {
                cmd.ReturnBuffers();
            }
        }

        protected void HandleConnectionTune(in IncomingCommand cmd)
        {
            using var k = (ConnectionSecureOrTuneAsyncRpcContinuation)_continuationQueue.Next();
            /*
             * Note: releases the continuation and returns the buffers
             */
            k.HandleCommand(cmd);
        }

        protected void HandleConnectionUnblocked(in IncomingCommand cmd)
        {
            try
            {
                Session.Connection.HandleConnectionUnblocked();
            }
            finally
            {
                cmd.ReturnBuffers();
            }
        }

        protected bool HandleQueueDeclareOk(in IncomingCommand cmd)
        {
            if (_continuationQueue.TryPeek<QueueDeclareRpcContinuation>(out var k))
            {
                try
                {
                    _continuationQueue.Next();
                    var method = new Client.Framing.Impl.QueueDeclareOk(cmd.MethodSpan);
                    k.m_result = new QueueDeclareOk(method._queue, method._messageCount, method._consumerCount);
                    k.HandleCommand(IncomingCommand.Empty); // release the continuation.
                    return true;
                }
                finally
                {
                    cmd.ReturnBuffers();
                }
            }
            else
            {
                return false;
            }
        }

        public abstract void _Private_ChannelClose(ushort replyCode, string replyText, ushort classId, ushort methodId);

        public abstract void _Private_ChannelCloseOk();

        public abstract void _Private_ChannelFlowOk(bool active);

        public abstract void _Private_ConnectionCloseOk();

        public abstract ValueTask BasicAckAsync(ulong deliveryTag, bool multiple);

        public async Task BasicCancelAsync(string consumerTag, bool noWait)
        {
            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                var method = new Client.Framing.Impl.BasicCancel(consumerTag, noWait);

                if (noWait)
                {
                    await ModelSendAsync(method)
                        .ConfigureAwait(false);
                    ConsumerDispatcher.GetAndRemoveConsumer(consumerTag);
                }
                else
                {
                    using var k = new BasicCancelAsyncRpcContinuation(consumerTag, ConsumerDispatcher, ContinuationTimeout);
                    Enqueue(k);

                    await ModelSendAsync(method)
                        .ConfigureAwait(false);

                    bool result = await k;
                    Debug.Assert(result);
                }

                return;
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

        public async Task<string> BasicConsumeAsync(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive,
            IDictionary<string, object> arguments, IBasicConsumer consumer)
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

            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                using var k = new BasicConsumeAsyncRpcContinuation(consumer, ConsumerDispatcher, ContinuationTimeout);
                Enqueue(k);

                var method = new Client.Framing.Impl.BasicConsume(queue, consumerTag, noLocal, autoAck, exclusive, false, arguments);
                await ModelSendAsync(method)
                    .ConfigureAwait(false);

                return await k;
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

        public async ValueTask<BasicGetResult> BasicGetAsync(string queue, bool autoAck)
        {
            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                using var k = new BasicGetAsyncRpcContinuation(AdjustDeliveryTag, ContinuationTimeout);
                Enqueue(k);

                var method = new BasicGet(queue, autoAck);
                await ModelSendAsync(method)
                    .ConfigureAwait(false);

                BasicGetResult result = await k;

                using Activity activity = result != null
                    ? RabbitMQActivitySource.Receive(result.RoutingKey,
                        result.Exchange,
                        result.DeliveryTag, result.BasicProperties, result.Body.Length)
                    : RabbitMQActivitySource.ReceiveEmpty(queue);

                activity?.SetStartTime(k.StartTime);

                return result;
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

        public abstract ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue);

        public ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey, in TProperties basicProperties, ReadOnlyMemory<byte> body, bool mandatory)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            if (NextPublishSeqNo > 0)
            {
                lock (_confirmLock)
                {
                    _pendingDeliveryTags.AddLast(NextPublishSeqNo++);
                }
            }

            try
            {
                var cmd = new BasicPublish(exchange, routingKey, mandatory, default);
                RabbitMQActivitySource.TryGetExistingContext(basicProperties, out ActivityContext existingContext);
                using Activity sendActivity = RabbitMQActivitySource.PublisherHasListeners
                    ? RabbitMQActivitySource.Send(routingKey, exchange, body.Length, existingContext)
                    : default;

                if (sendActivity != null)
                {
                    BasicProperties props = PopulateActivityAndPropagateTraceId(basicProperties, sendActivity);
                    return ModelSendAsync(in cmd, in props, body);
                }

                return ModelSendAsync(in cmd, in basicProperties, body);
            }
            catch
            {
                if (NextPublishSeqNo > 0)
                {
                    lock (_confirmLock)
                    {
                        NextPublishSeqNo--;
                        _pendingDeliveryTags.RemoveLast();
                    }
                }

                throw;
            }
        }

        private static void InjectTraceContextIntoBasicProperties(object propsObj, string key, string value)
        {
            if (!(propsObj is Dictionary<string, object> headers))
            {
                return;
            }

            // Only propagate headers if they haven't already been set
            if (!headers.ContainsKey(key))
            {
                headers[key] = value;
            }
        }

        public void BasicPublish<TProperties>(CachedString exchange, CachedString routingKey,
            in TProperties basicProperties, ReadOnlyMemory<byte> body, bool mandatory)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            if (NextPublishSeqNo > 0)
            {
                lock (_confirmLock)
                {
                    _pendingDeliveryTags.AddLast(NextPublishSeqNo++);
                }
            }

            try
            {
                var cmd = new BasicPublishMemory(exchange.Bytes, routingKey.Bytes, mandatory, default);
                RabbitMQActivitySource.TryGetExistingContext(basicProperties, out ActivityContext existingContext);
                using Activity sendActivity = RabbitMQActivitySource.PublisherHasListeners
                    ? RabbitMQActivitySource.Send(routingKey.Value, exchange.Value, body.Length, existingContext)
                    : default;

                if (sendActivity != null)
                {
                    BasicProperties props = PopulateActivityAndPropagateTraceId(basicProperties, sendActivity);
                    ChannelSend(in cmd, in props, body);
                    return;
                }

                ChannelSend(in cmd, in basicProperties, body);
            }
            catch
            {
                if (NextPublishSeqNo > 0)
                {
                    lock (_confirmLock)
                    {
                        NextPublishSeqNo--;
                        _pendingDeliveryTags.RemoveLast();
                    }
                }

                throw;
            }
        }

        public async ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey,
            TProperties basicProperties, ReadOnlyMemory<byte> body, bool mandatory)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            if (NextPublishSeqNo > 0)
            {
                lock (_confirmLock)
                {
                    _pendingDeliveryTags.AddLast(NextPublishSeqNo++);
                }
            }

            try
            {
                var cmd = new BasicPublish(exchange, routingKey, mandatory, default);
                RabbitMQActivitySource.TryGetExistingContext(basicProperties, out ActivityContext existingContext);
                using Activity sendActivity = RabbitMQActivitySource.PublisherHasListeners
                    ? RabbitMQActivitySource.Send(routingKey, exchange, body.Length, existingContext)
                    : default;

                if (sendActivity != null)
                {
                    BasicProperties props = PopulateActivityAndPropagateTraceId(basicProperties, sendActivity);
                    await ModelSendAsync(in cmd, in props, body);
                    return;
                }

                await ModelSendAsync(in cmd, in basicProperties, body);
            }
            catch
            {
                if (NextPublishSeqNo > 0)
                {
                    lock (_confirmLock)
                    {
                        NextPublishSeqNo--;
                        _pendingDeliveryTags.RemoveLast();
                    }
                }

                throw;
            }
        }

        public async ValueTask BasicPublishAsync<TProperties>(CachedString exchange, CachedString routingKey,
            TProperties basicProperties, ReadOnlyMemory<byte> body, bool mandatory)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            if (NextPublishSeqNo > 0)
            {
                lock (_confirmLock)
                {
                    _pendingDeliveryTags.AddLast(NextPublishSeqNo++);
                }
            }

            try
            {
                var cmd = new BasicPublishMemory(exchange.Bytes, routingKey.Bytes, mandatory, default);
                RabbitMQActivitySource.TryGetExistingContext(basicProperties, out ActivityContext existingContext);
                using Activity sendActivity = RabbitMQActivitySource.PublisherHasListeners
                    ? RabbitMQActivitySource.Send(routingKey.Value, exchange.Value, body.Length, existingContext)
                    : default;

                if (sendActivity != null)
                {
                    BasicProperties props = PopulateActivityAndPropagateTraceId(basicProperties, sendActivity);
                    await ModelSendAsync(in cmd, in props, body);
                    return;
                }

                await ModelSendAsync(in cmd, in basicProperties, body);
            }
            catch
            {
                if (NextPublishSeqNo > 0)
                {
                    lock (_confirmLock)
                    {
                        NextPublishSeqNo--;
                        _pendingDeliveryTags.RemoveLast();
                    }
                }

                throw;
            }
        }

#if FOO
        BasicProperties props = default;
            if (basicProperties is BasicProperties properties)
            {
                props = properties;
            }
            else if (basicProperties is EmptyBasicProperty)
            {
                props = new BasicProperties();
            }

            var headers = props.Headers ?? new Dictionary<string, object>();

            // Inject the ActivityContext into the message headers to propagate trace context to the receiving service.
            DistributedContextPropagator.Current.Inject(sendActivity, headers, InjectTraceContextIntoBasicProperties);
            props.Headers = headers;
            return props;
        }
#endif 

        public async Task UpdateSecretAsync(string newSecret, string reason)
        {
            if (newSecret is null)
            {
                throw new ArgumentNullException(nameof(newSecret));
            }

            if (reason is null)
            {
                throw new ArgumentNullException(nameof(reason));
            }

            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                using var k = new SimpleAsyncRpcContinuation(ProtocolCommandId.ConnectionUpdateSecretOk, ContinuationTimeout);
                Enqueue(k);

                var newSecretBytes = Encoding.UTF8.GetBytes(newSecret);
                var method = new ConnectionUpdateSecret(newSecretBytes, reason);
                await ModelSendAsync(method)
                    .ConfigureAwait(false);

                bool result = await k;
                Debug.Assert(result);
                return;
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

        public async Task BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global)
        {
            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                using var k = new BasicQosAsyncRpcContinuation(ContinuationTimeout);
                Enqueue(k);

                var method = new BasicQos(prefetchSize, prefetchCount, global);
                await ModelSendAsync(method)
                    .ConfigureAwait(false);

                bool result = await k;
                Debug.Assert(result);
                return;
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

        public abstract Task BasicRejectAsync(ulong deliveryTag, bool requeue);

        public async Task ConfirmSelectAsync()
        {
            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                if (NextPublishSeqNo == 0UL)
                {
                    _confirmsTaskCompletionSources = new List<TaskCompletionSource<bool>>();
                    NextPublishSeqNo = 1;
                }

                using var k = new ConfirmSelectAsyncRpcContinuation(ContinuationTimeout);
                Enqueue(k);

                var method = new ConfirmSelect(false);
                await ModelSendAsync(method)
                    .ConfigureAwait(false);

                bool result = await k;
                Debug.Assert(result);

                return;
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

        public async Task ExchangeBindAsync(string destination, string source, string routingKey,
            IDictionary<string, object> arguments, bool noWait)
        {
            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                var method = new ExchangeBind(destination, source, routingKey, noWait, arguments);

                if (noWait)
                {
                    await ModelSendAsync(method)
                        .ConfigureAwait(false);
                }
                else
                {
                    using var k = new ExchangeBindAsyncRpcContinuation(ContinuationTimeout);
                    Enqueue(k);

                    await ModelSendAsync(method)
                        .ConfigureAwait(false);

                    bool result = await k;
                    Debug.Assert(result);
                }

                return;
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

        public Task ExchangeDeclarePassiveAsync(string exchange)
        {
            return ExchangeDeclareAsync(exchange: exchange, type: string.Empty, passive: true,
                durable: false, autoDelete: false, arguments: null, noWait: false);
        }

        public async Task ExchangeDeclareAsync(string exchange, string type, bool durable, bool autoDelete,
            IDictionary<string, object> arguments, bool passive, bool noWait)
        {
            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                var method = new ExchangeDeclare(exchange, type, passive, durable, autoDelete, false, noWait, arguments);

                if (noWait)
                {
                    await ModelSendAsync(method)
                        .ConfigureAwait(false);
                }
                else
                {
                    using var k = new ExchangeDeclareAsyncRpcContinuation(ContinuationTimeout);
                    Enqueue(k);

                    await ModelSendAsync(method)
                        .ConfigureAwait(false);

                    bool result = await k;
                    Debug.Assert(result);
                }

                return;
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

        public async Task ExchangeDeleteAsync(string exchange, bool ifUnused, bool noWait)
        {
            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                var method = new ExchangeDelete(exchange, ifUnused, Nowait: noWait);

                if (noWait)
                {
                    await ModelSendAsync(method)
                        .ConfigureAwait(false);
                }
                else
                {
                    using var k = new ExchangeDeleteAsyncRpcContinuation(ContinuationTimeout);
                    Enqueue(k);

                    await ModelSendAsync(method)
                        .ConfigureAwait(false);

                    bool result = await k;
                    Debug.Assert(result);
                }

                return;
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

        public async Task ExchangeUnbindAsync(string destination, string source, string routingKey,
            IDictionary<string, object> arguments, bool noWait)
        {
            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                var method = new ExchangeUnbind(destination, source, routingKey, noWait, arguments);

                if (noWait)
                {
                    await ModelSendAsync(method)
                        .ConfigureAwait(false);
                }
                else
                {
                    using var k = new ExchangeUnbindAsyncRpcContinuation(ContinuationTimeout);
                    Enqueue(k);

                    await ModelSendAsync(method)
                        .ConfigureAwait(false);

                    bool result = await k;
                    Debug.Assert(result);
                }

                return;
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

        public Task<QueueDeclareOk> QueueDeclarePassiveAsync(string queue)
        {
            return QueueDeclareAsync(queue: queue, passive: true,
                durable: false, exclusive: false, autoDelete: false,
                noWait: false, arguments: null);
        }

        public async Task<QueueDeclareOk> QueueDeclareAsync(string queue, bool durable, bool exclusive, bool autoDelete,
            IDictionary<string, object> arguments, bool passive, bool noWait)
        {
            if (true == noWait)
            {
                if (queue == string.Empty)
                {
                    throw new InvalidOperationException("noWait must not be used with a server-named queue.");
                }

                if (true == passive)
                {
                    throw new InvalidOperationException("It does not make sense to use noWait: true and passive: true");
                }
            }

            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                var method = new QueueDeclare(queue, passive, durable, exclusive, autoDelete, noWait, arguments);

                if (noWait)
                {
                    await ModelSendAsync(method)
                        .ConfigureAwait(false);

                    if (false == passive)
                    {
                        CurrentQueue = queue;
                    }

                    return new QueueDeclareOk(queue, 0, 0);
                }
                else
                {
                    using var k = new QueueDeclareAsyncRpcContinuation(ContinuationTimeout);
                    Enqueue(k);

                    await ModelSendAsync(method)
                        .ConfigureAwait(false);

                    QueueDeclareOk result = await k;
                    if (false == passive)
                    {
                        CurrentQueue = result.QueueName;
                    }

                    return result;
                }
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

        public async Task QueueBindAsync(string queue, string exchange, string routingKey,
            IDictionary<string, object> arguments, bool noWait)
        {
            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                var method = new QueueBind(queue, exchange, routingKey, noWait, arguments);

                if (noWait)
                {
                    await ModelSendAsync(method)
                        .ConfigureAwait(false);
                }
                else
                {
                    using var k = new QueueBindAsyncRpcContinuation(ContinuationTimeout);
                    Enqueue(k);

                    await ModelSendAsync(method)
                        .ConfigureAwait(false);

                    bool result = await k;
                    Debug.Assert(result);
                }

                return;
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

        public async Task<uint> MessageCountAsync(string queue)
        {
            QueueDeclareOk ok = await QueueDeclarePassiveAsync(queue);
            return ok.MessageCount;
        }

        public async Task<uint> ConsumerCountAsync(string queue)
        {
            QueueDeclareOk ok = await QueueDeclarePassiveAsync(queue);
            return ok.ConsumerCount;
        }

        public async Task<uint> QueueDeleteAsync(string queue, bool ifUnused, bool ifEmpty, bool noWait)
        {
            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                var method = new QueueDelete(queue, ifUnused, ifEmpty, noWait);

                if (noWait)
                {
                    await ModelSendAsync(method)
                        .ConfigureAwait(false);

                    return 0;
                }
                else
                {
                    var k = new QueueDeleteAsyncRpcContinuation(ContinuationTimeout);
                    Enqueue(k);

                    await ModelSendAsync(method)
                        .ConfigureAwait(false);

                    return await k;
                }
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

        public async Task<uint> QueuePurgeAsync(string queue)
        {
            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                var k = new QueuePurgeAsyncRpcContinuation(ContinuationTimeout);
                Enqueue(k);

                var method = new QueuePurge(queue, false);
                await ModelSendAsync(method)
                    .ConfigureAwait(false);

                return await k;
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

        public async Task QueueUnbindAsync(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                using var k = new QueueUnbindAsyncRpcContinuation(ContinuationTimeout);
                Enqueue(k);

                var method = new QueueUnbind(queue, exchange, routingKey, arguments);
                await ModelSendAsync(method)
                    .ConfigureAwait(false);

                bool result = await k;
                Debug.Assert(result);
                return;
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

        public async Task TxCommitAsync()
        {
            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                using var k = new TxCommitAsyncRpcContinuation(ContinuationTimeout);
                Enqueue(k);

                var method = new TxCommit();
                await ModelSendAsync(method)
                    .ConfigureAwait(false);

                bool result = await k;
                Debug.Assert(result);
                return;
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

        public async Task TxRollbackAsync()
        {
            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                using var k = new TxRollbackAsyncRpcContinuation(ContinuationTimeout);
                Enqueue(k);

                var method = new TxRollback();
                await ModelSendAsync(method)
                    .ConfigureAwait(false);

                bool result = await k;
                Debug.Assert(result);
                return;
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

        public async Task TxSelectAsync()
        {
            await _rpcSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                using var k = new TxSelectAsyncRpcContinuation(ContinuationTimeout);
                Enqueue(k);

                var method = new TxSelect();
                await ModelSendAsync(method)
                    .ConfigureAwait(false);

                bool result = await k;
                Debug.Assert(result);
                return;
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

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
                await tokenRegistration.DisposeAsync()
                    .ConfigureAwait(false);
#endif
            }
        }

        public async Task WaitForConfirmsOrDieAsync(CancellationToken token = default)
        {
            try
            {
                bool onlyAcksReceived = await WaitForConfirmsAsync(token)
                    .ConfigureAwait(false);

                if (onlyAcksReceived)
                {
                    return;
                }

                var ea = new ShutdownEventArgs(ShutdownInitiator.Library, Constants.ReplySuccess, "Nacks Received", new IOException("nack received"));

                await CloseAsync(ea, false)
                    .ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                const string msg = "timed out waiting for acks";

                var ex = new IOException(msg);
                var ea = new ShutdownEventArgs(ShutdownInitiator.Library, Constants.ReplySuccess, msg, ex);

                await CloseAsync(ea, false)
                    .ConfigureAwait(false);

                throw ex;
            }
        }

        private static BasicProperties PopulateActivityAndPropagateTraceId<TProperties>(TProperties basicProperties,
            Activity sendActivity) where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            // This activity is marked as recorded, so let's propagate the trace and span ids.
            if (sendActivity.IsAllDataRequested)
            {
                if (!string.IsNullOrEmpty(basicProperties.CorrelationId))
                {
                    sendActivity.SetTag(RabbitMQActivitySource.MessageConversationId, basicProperties.CorrelationId);
                }
            }

            BasicProperties props = default;
            if (basicProperties is BasicProperties properties)
            {
                props = properties;
            }
            else if (basicProperties is EmptyBasicProperty)
            {
                props = new BasicProperties();
            }

            var headers = props.Headers ?? new Dictionary<string, object>();

            // Inject the ActivityContext into the message headers to propagate trace context to the receiving service.
            DistributedContextPropagator.Current.Inject(sendActivity, headers, InjectTraceContextIntoBasicProperties);
            props.Headers = headers;
            return props;
        }
    }
}
