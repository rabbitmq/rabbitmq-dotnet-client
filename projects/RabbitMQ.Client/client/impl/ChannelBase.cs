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
        private bool _disposed;

        ///<summary>Only used to kick-start a connection open
        ///sequence. See <see cref="Connection.OpenAsync"/> </summary>
        internal TaskCompletionSource<ConnectionStartDetails> m_connectionStartCell;
        private Exception m_connectionStartException = null;

        // AMQP only allows one RPC operation to be active at a time. 
        protected readonly SemaphoreSlim _rpcSemaphore = new SemaphoreSlim(1, 1);
        private readonly RpcContinuationQueue _continuationQueue = new RpcContinuationQueue();
        private readonly ManualResetEventSlim _flowControlBlock = new ManualResetEventSlim(true);

        private SemaphoreSlim _confirmSemaphore;
        private readonly LinkedList<ulong> _pendingDeliveryTags = new LinkedList<ulong>();

        private bool _onlyAcksReceived = true;

        private ShutdownEventArgs _closeReason = null;
        private readonly object _closeReasonLock = new object();

        internal readonly IConsumerDispatcher ConsumerDispatcher;

        protected ChannelBase(ConnectionConfig config, ISession session)
        {
            ContinuationTimeout = config.ContinuationTimeout;

            ConsumerDispatcher = new AsyncConsumerDispatcher(this, config.DispatchConsumerConcurrency);

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
            session.CommandReceived = HandleCommandAsync;
            session.SessionShutdownAsync += OnSessionShutdownAsync;
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

        public IAsyncBasicConsumer DefaultConsumer
        {
            get => ConsumerDispatcher.DefaultConsumer;
            set => ConsumerDispatcher.DefaultConsumer = value;
        }

        public bool IsClosed
        {
            get
            {
                lock (_closeReasonLock)
                {
                    return _closeReason != null;
                }
            }
        }

        public bool IsOpen
        {
            get
            {
                lock (_closeReasonLock)
                {
                    return _closeReason is null;
                }
            }
        }

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

        public Task CloseAsync(ushort replyCode, string replyText, bool abort,
            CancellationToken cancellationToken)
        {
            var args = new ShutdownEventArgs(ShutdownInitiator.Application, replyCode, replyText);
            return CloseAsync(args, abort, cancellationToken);
        }

        public async Task CloseAsync(ShutdownEventArgs args, bool abort,
            CancellationToken cancellationToken)
        {
            bool enqueued = false;
            var k = new ChannelCloseAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                ChannelShutdown += k.OnConnectionShutdown;
                enqueued = Enqueue(k);
                ConsumerDispatcher.Quiesce();

                if (SetCloseReason(args))
                {
                    var method = new ChannelClose(
                        args.ReplyCode, args.ReplyText, args.ClassId, args.MethodId);
                    await ModelSendAsync(method, k.CancellationToken)
                        .ConfigureAwait(false);
                }

                bool result = await k;
                Debug.Assert(result);

                await ConsumerDispatcher.WaitForShutdownAsync(cancellationToken)
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
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
                ChannelShutdown -= k.OnConnectionShutdown;
            }
        }

        internal async ValueTask ConnectionOpenAsync(string virtualHost, CancellationToken cancellationToken)
        {
            using var timeoutTokenSource = new CancellationTokenSource(HandshakeContinuationTimeout);
            using var lts = CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, cancellationToken);
            var m = new ConnectionOpen(virtualHost);
            // Note: must be awaited or else the timeoutTokenSource instance will be disposed
            await ModelSendAsync(m, lts.Token).ConfigureAwait(false);
        }

        internal async ValueTask<ConnectionSecureOrTune> ConnectionSecureOkAsync(byte[] response,
            CancellationToken cancellationToken)
        {
            bool enqueued = false;
            var k = new ConnectionSecureOrTuneAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                enqueued = Enqueue(k);

                try
                {
                    var method = new ConnectionSecureOk(response);
                    await ModelSendAsync(method, k.CancellationToken)
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
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
            }
        }

        internal async ValueTask<ConnectionSecureOrTune> ConnectionStartOkAsync(
            IDictionary<string, object> clientProperties,
            string mechanism, byte[] response, string locale,
            CancellationToken cancellationToken)
        {
            bool enqueued = false;
            var k = new ConnectionSecureOrTuneAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                enqueued = Enqueue(k);

                try
                {
                    var method = new ConnectionStartOk(clientProperties, mechanism, response, locale);
                    await ModelSendAsync(method, k.CancellationToken)
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
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
            }
        }

        protected abstract Task<bool> DispatchCommandAsync(IncomingCommand cmd, CancellationToken cancellationToken);

        protected bool Enqueue(IRpcContinuation k)
        {
            if (IsOpen)
            {
                _continuationQueue.Enqueue(k);
                return true;
            }
            else
            {
                k.HandleChannelShutdown(CloseReason);
                return false;
            }
        }

        internal async Task<IChannel> OpenAsync(CancellationToken cancellationToken)
        {
            bool enqueued = false;
            var k = new ChannelOpenAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                enqueued = Enqueue(k);

                var method = new ChannelOpen();
                await ModelSendAsync(method, k.CancellationToken)
                    .ConfigureAwait(false);

                bool result = await k;
                Debug.Assert(result);
                return this;
            }
            finally
            {
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
            }
        }

        internal async Task FinishCloseAsync(CancellationToken cancellationToken)
        {
            ShutdownEventArgs reason = CloseReason;
            if (reason != null)
            {
                await Session.CloseAsync(reason, cancellationToken)
                    .ConfigureAwait(false);
            }
            m_connectionStartCell?.TrySetResult(null);
        }

        private bool ConfirmsAreEnabled => _confirmSemaphore != null;

        private async Task HandleCommandAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            /*
             * If DispatchCommandAsync returns `true`, it means that the incoming command is server-originated, and has
             * already been handled.
             * 
             * Else, the incoming command is the return of an RPC call, and must be handled. 
             */
            if (false == await DispatchCommandAsync(cmd, cancellationToken)
                .ConfigureAwait(false))
            {
                using (IRpcContinuation c = _continuationQueue.Next())
                {
                    await c.HandleCommandAsync(cmd)
                        .ConfigureAwait(false);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected ValueTask ModelSendAsync<T>(in T method, CancellationToken cancellationToken) where T : struct, IOutgoingAmqpMethod
        {
            return Session.TransmitAsync(in method, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected ValueTask ModelSendAsync<TMethod, THeader>(in TMethod method, in THeader header, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
            where TMethod : struct, IOutgoingAmqpMethod
            where THeader : IAmqpHeader
        {
            if (!_flowControlBlock.IsSet)
            {
                _flowControlBlock.Wait();
            }

            return Session.TransmitAsync(in method, in header, body, cancellationToken);
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

            if (ConfirmsAreEnabled)
            {
                _confirmSemaphore.Wait();
                try
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
                finally
                {
                    _confirmSemaphore.Release();
                }
            }

            _flowControlBlock.Set();
        }

        private Task OnSessionShutdownAsync(object sender, ShutdownEventArgs reason,
            CancellationToken cancellationToken)
        {
            ConsumerDispatcher.Quiesce();
            SetCloseReason(reason);
            OnChannelShutdown(reason);
            return ConsumerDispatcher.ShutdownAsync(reason, cancellationToken);
        }

        public ShutdownEventArgs CloseReason
        {
            get
            {
                lock (_closeReasonLock)
                {
                    return _closeReason;
                }
            }
        }

        internal bool SetCloseReason(ShutdownEventArgs reason)
        {
            if (reason is null)
            {
                throw new ArgumentNullException(nameof(reason));
            }

            // NB: this ensures that Close is only called once on a channel
            lock (_closeReasonLock)
            {
                bool rv = false;
                if (_closeReason is null)
                {
                    _closeReason = reason;
                    rv = true;
                }
                return rv;
            }
        }

        public override string ToString()
            => Session.ToString();

        void IDisposable.Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                try
                {
                    if (IsOpen)
                    {
                        this.AbortAsync().GetAwaiter().GetResult();
                    }
                    ConsumerDispatcher.Dispose();
                    Session.Dispose();
                    _rpcSemaphore.Dispose();
                    _confirmSemaphore?.Dispose();
                }
                finally
                {
                    _disposed = true;
                }
            }
        }

        public Task ConnectionTuneOkAsync(ushort channelMax, uint frameMax, ushort heartbeat, CancellationToken cancellationToken)
        {
            var method = new ConnectionTuneOk(channelMax, frameMax, heartbeat);
            return ModelSendAsync(method, cancellationToken).AsTask();
        }

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
            if (ConfirmsAreEnabled)
            {
                // let's take a lock so we can assume that deliveryTags are unique, never duplicated and always sorted
                _confirmSemaphore.Wait();
                try
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
                finally
                {
                    _confirmSemaphore.Release();
                }
            }
        }

        protected async Task<bool> HandleBasicCancelAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            try
            {
                string consumerTag = new Client.Framing.Impl.BasicCancel(cmd.MethodSpan)._consumerTag;
                await ConsumerDispatcher.HandleBasicCancelAsync(consumerTag, cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
            finally
            {
                cmd.ReturnBuffers();
            }
        }

        protected async Task<bool> HandleBasicDeliverAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            try
            {
                var method = new Client.Framing.Impl.BasicDeliver(cmd.MethodSpan);
                var header = new ReadOnlyBasicProperties(cmd.HeaderSpan);
                await ConsumerDispatcher.HandleBasicDeliverAsync(
                        method._consumerTag,
                        AdjustDeliveryTag(method._deliveryTag),
                        method._redelivered,
                        method._exchange,
                        method._routingKey,
                        header,
                        cmd.Body,
                        cancellationToken).ConfigureAwait(false);
                return true;
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

        protected virtual ulong AdjustDeliveryTag(ulong deliveryTag)
        {
            return deliveryTag;
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

        protected async Task<bool> HandleChannelCloseAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            try
            {
                var channelClose = new ChannelClose(cmd.MethodSpan);
                SetCloseReason(new ShutdownEventArgs(ShutdownInitiator.Peer,
                    channelClose._replyCode,
                    channelClose._replyText,
                    channelClose._classId,
                    channelClose._methodId));

                await Session.CloseAsync(CloseReason, false, cancellationToken)
                    .ConfigureAwait(false);

                var method = new ChannelCloseOk();
                await ModelSendAsync(method, cancellationToken)
                    .ConfigureAwait(false);

                return true;
            }
            finally
            {
                cmd.ReturnBuffers();
                await Session.NotifyAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        protected async Task<bool> HandleChannelCloseOkAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            try
            {
                /*
                 * Note:
                 * This call _must_ come before completing the async continuation
                 */
                await FinishCloseAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (_continuationQueue.TryPeek<ChannelCloseAsyncRpcContinuation>(out var k))
                {
                    _continuationQueue.Next();
                    await k.HandleCommandAsync(cmd)
                        .ConfigureAwait(false);
                }

                return true;
            }
            finally
            {
                cmd.ReturnBuffers();
            }
        }

        protected async Task<bool> HandleChannelFlowAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            try
            {
                bool active = new ChannelFlow(cmd.MethodSpan)._active;
                if (active)
                {
                    _flowControlBlock.Set();
                }
                else
                {
                    _flowControlBlock.Reset();
                }

                var method = new ChannelFlowOk(active);
                await ModelSendAsync(method, cancellationToken).
                    ConfigureAwait(false);

                if (!_flowControlWrapper.IsEmpty)
                {
                    _flowControlWrapper.Invoke(this, new FlowControlEventArgs(active));
                }

                return true;
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
                string reason = new ConnectionBlocked(cmd.MethodSpan)._reason;
                Session.Connection.HandleConnectionBlocked(reason);
            }
            finally
            {
                cmd.ReturnBuffers();
            }
        }

        protected async Task<bool> HandleConnectionCloseAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            try
            {
                var method = new ConnectionClose(cmd.MethodSpan);
                var reason = new ShutdownEventArgs(ShutdownInitiator.Peer, method._replyCode, method._replyText, method._classId, method._methodId);
                try
                {
                    await Session.Connection.ClosedViaPeerAsync(reason, cancellationToken)
                        .ConfigureAwait(false);

                    var replyMethod = new ConnectionCloseOk();
                    await ModelSendAsync(replyMethod, cancellationToken)
                        .ConfigureAwait(false);

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

                return true;
            }
            finally
            {
                cmd.ReturnBuffers();
            }
        }

        protected async Task<bool> HandleConnectionSecureAsync(IncomingCommand _)
        {
            var k = (ConnectionSecureOrTuneAsyncRpcContinuation)_continuationQueue.Next();
            await k.HandleCommandAsync(IncomingCommand.Empty)
                .ConfigureAwait(false); // release the continuation.
            return true;
        }

        protected async Task<bool> HandleConnectionStartAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            try
            {
                if (m_connectionStartCell is null)
                {
                    var reason = new ShutdownEventArgs(ShutdownInitiator.Library, Constants.CommandInvalid, "Unexpected Connection.Start");
                    await Session.Connection.CloseAsync(reason, false,
                        InternalConstants.DefaultConnectionCloseTimeout,
                        cancellationToken)
                        .ConfigureAwait(false);
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

                return true;
            }
            finally
            {
                cmd.ReturnBuffers();
            }
        }

        protected async Task<bool> HandleConnectionTuneAsync(IncomingCommand cmd)
        {
            // Note: `using` here to ensure instance is disposed
            using var k = (ConnectionSecureOrTuneAsyncRpcContinuation)_continuationQueue.Next();

            // Note: releases the continuation and returns the buffers
            await k.HandleCommandAsync(cmd)
                .ConfigureAwait(false);

            return true;
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

        public abstract ValueTask BasicAckAsync(ulong deliveryTag, bool multiple,
            CancellationToken cancellationToken);

        public abstract ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue,
            CancellationToken cancellationToken);

        public abstract Task BasicRejectAsync(ulong deliveryTag, bool requeue,
            CancellationToken cancellationToken);

        public async Task BasicCancelAsync(string consumerTag, bool noWait,
            CancellationToken cancellationToken)
        {
            bool enqueued = false;
            // NOTE:
            // Maybe don't dispose these instances because the CancellationTokens must remain
            // valid for processing the response.
            var k = new BasicCancelAsyncRpcContinuation(consumerTag, ConsumerDispatcher,
                ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                var method = new BasicCancel(consumerTag, noWait);

                if (noWait)
                {
                    await ModelSendAsync(method, k.CancellationToken)
                        .ConfigureAwait(false);
                    ConsumerDispatcher.GetAndRemoveConsumer(consumerTag);
                }
                else
                {
                    enqueued = Enqueue(k);

                    await ModelSendAsync(method, k.CancellationToken)
                        .ConfigureAwait(false);

                    bool result = await k;
                    Debug.Assert(result);
                }

                return;
            }
            finally
            {
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
            }
        }

        public async Task<string> BasicConsumeAsync(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive,
            IDictionary<string, object> arguments, IAsyncBasicConsumer consumer,
            CancellationToken cancellationToken)
        {
            if (ConsumerDispatcher is AsyncConsumerDispatcher)
            {
                if (!(consumer is IAsyncBasicConsumer))
                {
                    throw new InvalidOperationException("When using an AsyncConsumerDispatcher, the consumer must implement IAsyncBasicConsumer");
                }
            }

            // NOTE:
            // Maybe don't dispose this instance because the CancellationToken must remain
            // valid for processing the response.
            bool enqueued = false;
            var k = new BasicConsumeAsyncRpcContinuation(consumer, ConsumerDispatcher, ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                enqueued = Enqueue(k);

                var method = new Client.Framing.Impl.BasicConsume(queue, consumerTag, noLocal, autoAck, exclusive, false, arguments);
                await ModelSendAsync(method, k.CancellationToken)
                    .ConfigureAwait(false);

                return await k;
            }
            finally
            {
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
            }
        }

        public async ValueTask<BasicGetResult> BasicGetAsync(string queue, bool autoAck,
            CancellationToken cancellationToken)
        {
            bool enqueued = false;

            var k = new BasicGetAsyncRpcContinuation(AdjustDeliveryTag, ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                enqueued = Enqueue(k);

                var method = new BasicGet(queue, autoAck);
                await ModelSendAsync(method, k.CancellationToken)
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
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
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

        public async ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey,
            TProperties basicProperties, ReadOnlyMemory<byte> body, bool mandatory,
            CancellationToken cancellationToken)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            if (ConfirmsAreEnabled)
            {
                await _confirmSemaphore.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    _pendingDeliveryTags.AddLast(NextPublishSeqNo++);
                }
                finally
                {
                    _confirmSemaphore.Release();
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
                    await ModelSendAsync(in cmd, in props, body, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await ModelSendAsync(in cmd, in basicProperties, body, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch
            {
                if (ConfirmsAreEnabled)
                {
                    await _confirmSemaphore.WaitAsync(cancellationToken)
                        .ConfigureAwait(false);
                    try
                    {
                        NextPublishSeqNo--;
                        _pendingDeliveryTags.RemoveLast();
                    }
                    finally
                    {
                        _confirmSemaphore.Release();
                    }
                }

                throw;
            }
        }

        public async ValueTask BasicPublishAsync<TProperties>(CachedString exchange, CachedString routingKey,
            TProperties basicProperties, ReadOnlyMemory<byte> body, bool mandatory,
            CancellationToken cancellationToken)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            if (ConfirmsAreEnabled)
            {
                await _confirmSemaphore.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    _pendingDeliveryTags.AddLast(NextPublishSeqNo++);
                }
                finally
                {
                    _confirmSemaphore.Release();
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
                    await ModelSendAsync(in cmd, in props, body, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await ModelSendAsync(in cmd, in basicProperties, body, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch
            {
                if (ConfirmsAreEnabled)
                {
                    await _confirmSemaphore.WaitAsync(cancellationToken)
                        .ConfigureAwait(false);
                    try
                    {
                        NextPublishSeqNo--;
                        _pendingDeliveryTags.RemoveLast();
                    }
                    finally
                    {
                        _confirmSemaphore.Release();
                    }
                }

                throw;
            }
        }

        public async Task UpdateSecretAsync(string newSecret, string reason,
            CancellationToken cancellationToken)
        {
            if (newSecret is null)
            {
                throw new ArgumentNullException(nameof(newSecret));
            }

            if (reason is null)
            {
                throw new ArgumentNullException(nameof(reason));
            }

            bool enqueued = false;
            var k = new SimpleAsyncRpcContinuation(ProtocolCommandId.ConnectionUpdateSecretOk,
                ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                enqueued = Enqueue(k);

                byte[] newSecretBytes = Encoding.UTF8.GetBytes(newSecret);
                var method = new ConnectionUpdateSecret(newSecretBytes, reason);
                await ModelSendAsync(method, k.CancellationToken)
                    .ConfigureAwait(false);

                bool result = await k;
                Debug.Assert(result);
                return;
            }
            finally
            {
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
            }
        }

        public async Task BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global,
            CancellationToken cancellationToken)
        {
            bool enqueued = false;
            var k = new BasicQosAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                enqueued = Enqueue(k);

                var method = new BasicQos(prefetchSize, prefetchCount, global);
                await ModelSendAsync(method, k.CancellationToken)
                    .ConfigureAwait(false);

                bool result = await k;
                Debug.Assert(result);
                return;
            }
            finally
            {
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
            }
        }

        public async Task ConfirmSelectAsync(CancellationToken cancellationToken)
        {
            bool enqueued = false;
            var k = new ConfirmSelectAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                if (NextPublishSeqNo == 0UL)
                {
                    _confirmsTaskCompletionSources = new List<TaskCompletionSource<bool>>();
                    NextPublishSeqNo = 1;
                }

                enqueued = Enqueue(k);

                var method = new ConfirmSelect(false);
                await ModelSendAsync(method, k.CancellationToken)
                    .ConfigureAwait(false);

                bool result = await k;
                Debug.Assert(result);

                // Note:
                // Non-null means confirms are enabled
                _confirmSemaphore = new SemaphoreSlim(1, 1);

                return;
            }
            finally
            {
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
            }
        }

        public async Task ExchangeBindAsync(string destination, string source, string routingKey,
            IDictionary<string, object> arguments, bool noWait,
            CancellationToken cancellationToken)
        {
            bool enqueued = false;
            var k = new ExchangeBindAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                var method = new ExchangeBind(destination, source, routingKey, noWait, arguments);

                if (noWait)
                {
                    await ModelSendAsync(method, k.CancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    enqueued = Enqueue(k);

                    await ModelSendAsync(method, k.CancellationToken)
                        .ConfigureAwait(false);

                    bool result = await k;
                    Debug.Assert(result);
                }

                return;
            }
            finally
            {
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
            }
        }

        public Task ExchangeDeclarePassiveAsync(string exchange, CancellationToken cancellationToken)
        {
            return ExchangeDeclareAsync(exchange: exchange, type: string.Empty, passive: true,
                durable: false, autoDelete: false, arguments: null, noWait: false,
                cancellationToken: cancellationToken);
        }

        public async Task ExchangeDeclareAsync(string exchange, string type, bool durable, bool autoDelete,
            IDictionary<string, object> arguments, bool passive, bool noWait,
            CancellationToken cancellationToken)
        {
            bool enqueued = false;
            var k = new ExchangeDeclareAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                var method = new ExchangeDeclare(exchange, type, passive, durable, autoDelete, false, noWait, arguments);
                if (noWait)
                {
                    await ModelSendAsync(method, k.CancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    enqueued = Enqueue(k);

                    await ModelSendAsync(method, k.CancellationToken)
                        .ConfigureAwait(false);

                    bool result = await k;
                    Debug.Assert(result);
                }

                return;
            }
            finally
            {
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
            }
        }

        public async Task ExchangeDeleteAsync(string exchange, bool ifUnused, bool noWait,
            CancellationToken cancellationToken)
        {
            bool enqueued = false;
            var k = new ExchangeDeleteAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                var method = new ExchangeDelete(exchange, ifUnused, Nowait: noWait);

                if (noWait)
                {
                    await ModelSendAsync(method, k.CancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    enqueued = Enqueue(k);

                    await ModelSendAsync(method, k.CancellationToken)
                        .ConfigureAwait(false);

                    bool result = await k;
                    Debug.Assert(result);
                }

                return;
            }
            finally
            {
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
            }
        }

        public async Task ExchangeUnbindAsync(string destination, string source, string routingKey,
            IDictionary<string, object> arguments, bool noWait,
            CancellationToken cancellationToken)
        {
            bool enqueued = false;
            var k = new ExchangeUnbindAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                var method = new ExchangeUnbind(destination, source, routingKey, noWait, arguments);

                if (noWait)
                {
                    await ModelSendAsync(method, k.CancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    enqueued = Enqueue(k);

                    await ModelSendAsync(method, k.CancellationToken)
                        .ConfigureAwait(false);

                    bool result = await k;
                    Debug.Assert(result);
                }

                return;
            }
            finally
            {
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
            }
        }

        public Task<QueueDeclareOk> QueueDeclarePassiveAsync(string queue,
            CancellationToken cancellationToken)
        {
            return QueueDeclareAsync(queue: queue, passive: true,
                durable: false, exclusive: false, autoDelete: false,
                noWait: false, arguments: null, cancellationToken: cancellationToken);
        }

        public async Task<QueueDeclareOk> QueueDeclareAsync(string queue, bool durable, bool exclusive, bool autoDelete,
            IDictionary<string, object> arguments, bool passive, bool noWait,
            CancellationToken cancellationToken)
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

            bool enqueued = false;
            var k = new QueueDeclareAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                var method = new QueueDeclare(queue, passive, durable, exclusive, autoDelete, noWait, arguments);

                if (noWait)
                {

                    await ModelSendAsync(method, k.CancellationToken)
                        .ConfigureAwait(false);

                    if (false == passive)
                    {
                        CurrentQueue = queue;
                    }

                    return new QueueDeclareOk(queue, 0, 0);
                }
                else
                {
                    enqueued = Enqueue(k);

                    await ModelSendAsync(method, k.CancellationToken)
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
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
            }
        }

        public async Task QueueBindAsync(string queue, string exchange, string routingKey,
            IDictionary<string, object> arguments, bool noWait,
            CancellationToken cancellationToken)
        {
            bool enqueued = false;
            var k = new QueueBindAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                var method = new QueueBind(queue, exchange, routingKey, noWait, arguments);

                if (noWait)
                {
                    await ModelSendAsync(method, k.CancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    enqueued = Enqueue(k);

                    await ModelSendAsync(method, k.CancellationToken)
                        .ConfigureAwait(false);

                    bool result = await k;
                    Debug.Assert(result);
                }

                return;
            }
            finally
            {
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
            }
        }

        public async Task<uint> MessageCountAsync(string queue,
            CancellationToken cancellationToken)
        {
            QueueDeclareOk ok = await QueueDeclarePassiveAsync(queue, cancellationToken)
                .ConfigureAwait(false);
            return ok.MessageCount;
        }

        public async Task<uint> ConsumerCountAsync(string queue,
            CancellationToken cancellationToken)
        {
            QueueDeclareOk ok = await QueueDeclarePassiveAsync(queue, cancellationToken)
                .ConfigureAwait(false);
            return ok.ConsumerCount;
        }

        public async Task<uint> QueueDeleteAsync(string queue, bool ifUnused, bool ifEmpty, bool noWait,
            CancellationToken cancellationToken)
        {
            bool enqueued = false;
            var k = new QueueDeleteAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                var method = new QueueDelete(queue, ifUnused, ifEmpty, noWait);

                if (noWait)
                {
                    await ModelSendAsync(method, k.CancellationToken)
                        .ConfigureAwait(false);

                    return 0;
                }
                else
                {
                    enqueued = Enqueue(k);

                    await ModelSendAsync(method, k.CancellationToken)
                        .ConfigureAwait(false);

                    return await k;
                }
            }
            finally
            {
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
            }
        }

        public async Task<uint> QueuePurgeAsync(string queue, CancellationToken cancellationToken)
        {
            bool enqueued = false;

            var k = new QueuePurgeAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                enqueued = Enqueue(k);

                var method = new QueuePurge(queue, false);
                await ModelSendAsync(method, k.CancellationToken)
                    .ConfigureAwait(false);

                return await k;
            }
            finally
            {
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
            }
        }

        public async Task QueueUnbindAsync(string queue, string exchange, string routingKey,
            IDictionary<string, object> arguments,
            CancellationToken cancellationToken)
        {
            bool enqueued = false;
            var k = new QueueUnbindAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                Enqueue(k);

                var method = new QueueUnbind(queue, exchange, routingKey, arguments);
                await ModelSendAsync(method, k.CancellationToken)
                    .ConfigureAwait(false);

                bool result = await k;
                Debug.Assert(result);
                return;
            }
            finally
            {
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
            }
        }

        public async Task TxCommitAsync(CancellationToken cancellationToken)
        {
            bool enqueued = false;
            var k = new TxCommitAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                enqueued = Enqueue(k);

                var method = new TxCommit();
                await ModelSendAsync(method, k.CancellationToken)
                    .ConfigureAwait(false);

                bool result = await k;
                Debug.Assert(result);
                return;
            }
            finally
            {
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
            }
        }

        public async Task TxRollbackAsync(CancellationToken cancellationToken)
        {
            bool enqueued = false;
            var k = new TxRollbackAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                enqueued = Enqueue(k);

                var method = new TxRollback();
                await ModelSendAsync(method, k.CancellationToken)
                    .ConfigureAwait(false);

                bool result = await k;
                Debug.Assert(result);
                return;
            }
            finally
            {
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
            }
        }

        public async Task TxSelectAsync(CancellationToken cancellationToken)
        {
            bool enqueued = false;
            var k = new TxSelectAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                Enqueue(k);

                var method = new TxSelect();
                await ModelSendAsync(method, k.CancellationToken)
                    .ConfigureAwait(false);

                bool result = await k;
                Debug.Assert(result);
                return;
            }
            finally
            {
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
            }
        }

        private List<TaskCompletionSource<bool>> _confirmsTaskCompletionSources;

        public async Task<bool> WaitForConfirmsAsync(CancellationToken cancellationToken = default)
        {
            if (false == ConfirmsAreEnabled)
            {
                throw new InvalidOperationException("Confirms not selected");
            }

            TaskCompletionSource<bool> tcs;
            await _confirmSemaphore.WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            try
            {
                if (_pendingDeliveryTags.Count == 0)
                {
                    if (_onlyAcksReceived == false)
                    {
                        _onlyAcksReceived = true;
                        return false;
                    }

                    return true;
                }

                tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _confirmsTaskCompletionSources.Add(tcs);
            }
            finally
            {
                _confirmSemaphore.Release();
            }

            bool rv;

            if (false == cancellationToken.CanBeCanceled)
            {
                rv = await tcs.Task.ConfigureAwait(false);
            }
            else
            {
                rv = await WaitForConfirmsWithTokenAsync(tcs, cancellationToken)
                    .ConfigureAwait(false);
            }

            return rv;
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

                await CloseAsync(ea, false, token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                const string msg = "timed out waiting for acks";
                var ea = new ShutdownEventArgs(ShutdownInitiator.Library, Constants.ReplySuccess, msg, ex);

                await CloseAsync(ea, false, token)
                    .ConfigureAwait(false);

                throw;
            }
        }

        private async Task<bool> WaitForConfirmsWithTokenAsync(TaskCompletionSource<bool> tcs,
            CancellationToken cancellationToken)
        {
            CancellationTokenRegistration tokenRegistration =
#if NET6_0_OR_GREATER
                cancellationToken.UnsafeRegister(
                    state => ((TaskCompletionSource<bool>)state).TrySetCanceled(), tcs);
#else
                cancellationToken.Register(
                    state => ((TaskCompletionSource<bool>)state).TrySetCanceled(),
                    state: tcs, useSynchronizationContext: false);
#endif
            try
            {
                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
#if NET6_0_OR_GREATER
                await tokenRegistration.DisposeAsync()
                    .ConfigureAwait(false);
#else
                tokenRegistration.Dispose();
#endif
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
