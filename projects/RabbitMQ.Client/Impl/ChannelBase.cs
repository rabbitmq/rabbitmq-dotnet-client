// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.ConsumerDispatching;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Impl
{
    internal abstract class ChannelBase : IChannel, IRecoverable
    {
        ///<summary>Only used to kick-start a connection open
        ///sequence. See <see cref="Connection.OpenAsync"/> </summary>
        internal TaskCompletionSource<ConnectionStartDetails?>? m_connectionStartCell;
        private Exception? m_connectionStartException;

        // AMQP only allows one RPC operation to be active at a time.
        protected readonly SemaphoreSlim _rpcSemaphore = new SemaphoreSlim(1, 1);
        private readonly RpcContinuationQueue _continuationQueue = new RpcContinuationQueue();
        private readonly AsyncManualResetEvent _flowControlBlock = new AsyncManualResetEvent(true);

        private ulong _nextPublishSeqNo;
        private SemaphoreSlim? _confirmSemaphore;
        private bool _trackConfirmations;
        private LinkedList<ulong>? _pendingDeliveryTags;
        private List<TaskCompletionSource<bool>>? _confirmsTaskCompletionSources;

        private bool _onlyAcksReceived = true;

        private ShutdownEventArgs? _closeReason;
        public ShutdownEventArgs? CloseReason => Volatile.Read(ref _closeReason);

        internal readonly IConsumerDispatcher ConsumerDispatcher;

        protected ChannelBase(ConnectionConfig config, ISession session,
            ushort? perChannelConsumerDispatchConcurrency = null)
        {
            ContinuationTimeout = config.ContinuationTimeout;
            ConsumerDispatcher = new AsyncConsumerDispatcher(this,
                perChannelConsumerDispatchConcurrency.GetValueOrDefault(config.ConsumerDispatchConcurrency));
            Func<Exception, string, CancellationToken, Task> onExceptionAsync = (exception, context, cancellationToken) =>
                OnCallbackExceptionAsync(CallbackExceptionEventArgs.Build(exception, context, cancellationToken));
            _basicAcksAsyncWrapper = new AsyncEventingWrapper<BasicAckEventArgs>("OnBasicAck", onExceptionAsync);
            _basicNacksAsyncWrapper = new AsyncEventingWrapper<BasicNackEventArgs>("OnBasicNack", onExceptionAsync);
            _basicReturnAsyncWrapper = new AsyncEventingWrapper<BasicReturnEventArgs>("OnBasicReturn", onExceptionAsync);
            _callbackExceptionAsyncWrapper =
                new AsyncEventingWrapper<CallbackExceptionEventArgs>(string.Empty, (exception, context, cancellationToken) => Task.CompletedTask);
            _flowControlAsyncWrapper = new AsyncEventingWrapper<FlowControlEventArgs>("OnFlowControl", onExceptionAsync);
            _channelShutdownAsyncWrapper = new AsyncEventingWrapper<ShutdownEventArgs>("OnChannelShutdownAsync", onExceptionAsync);
            _recoveryAsyncWrapper = new AsyncEventingWrapper<AsyncEventArgs>("OnChannelRecovery", onExceptionAsync);
            session.CommandReceived = HandleCommandAsync;
            session.SessionShutdownAsync += OnSessionShutdownAsync;
            Session = session;
        }

        internal TimeSpan HandshakeContinuationTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan ContinuationTimeout { get; set; }

        public event AsyncEventHandler<BasicAckEventArgs> BasicAcksAsync
        {
            add => _basicAcksAsyncWrapper.AddHandler(value);
            remove => _basicAcksAsyncWrapper.RemoveHandler(value);
        }

        private AsyncEventingWrapper<BasicAckEventArgs> _basicAcksAsyncWrapper;

        public event AsyncEventHandler<BasicNackEventArgs> BasicNacksAsync
        {
            add => _basicNacksAsyncWrapper.AddHandler(value);
            remove => _basicNacksAsyncWrapper.RemoveHandler(value);
        }

        private AsyncEventingWrapper<BasicNackEventArgs> _basicNacksAsyncWrapper;

        public event AsyncEventHandler<BasicReturnEventArgs> BasicReturnAsync
        {
            add => _basicReturnAsyncWrapper.AddHandler(value);
            remove => _basicReturnAsyncWrapper.RemoveHandler(value);
        }

        private AsyncEventingWrapper<BasicReturnEventArgs> _basicReturnAsyncWrapper;

        public event AsyncEventHandler<CallbackExceptionEventArgs> CallbackExceptionAsync
        {
            add => _callbackExceptionAsyncWrapper.AddHandler(value);
            remove => _callbackExceptionAsyncWrapper.RemoveHandler(value);
        }

        private AsyncEventingWrapper<CallbackExceptionEventArgs> _callbackExceptionAsyncWrapper;

        public event AsyncEventHandler<FlowControlEventArgs> FlowControlAsync
        {
            add => _flowControlAsyncWrapper.AddHandler(value);
            remove => _flowControlAsyncWrapper.RemoveHandler(value);
        }

        private AsyncEventingWrapper<FlowControlEventArgs> _flowControlAsyncWrapper;

        public event AsyncEventHandler<ShutdownEventArgs> ChannelShutdownAsync
        {
            add
            {
                if (IsOpen)
                {
                    _channelShutdownAsyncWrapper.AddHandler(value);
                }
                else
                {
                    value(this, CloseReason);
                }
            }
            remove => _channelShutdownAsyncWrapper.RemoveHandler(value);
        }

        private AsyncEventingWrapper<ShutdownEventArgs> _channelShutdownAsyncWrapper;

        public event AsyncEventHandler<AsyncEventArgs> RecoveryAsync
        {
            add => _recoveryAsyncWrapper.AddHandler(value);
            remove => _recoveryAsyncWrapper.RemoveHandler(value);
        }

        private AsyncEventingWrapper<AsyncEventArgs> _recoveryAsyncWrapper;

        internal Task RunRecoveryEventHandlers(object sender, CancellationToken cancellationToken)
        {
            return _recoveryAsyncWrapper.InvokeAsync(sender, AsyncEventArgs.CreateOrDefault(cancellationToken));
        }

        public int ChannelNumber => ((Session)Session).ChannelNumber;

        public IAsyncBasicConsumer? DefaultConsumer
        {
            get => ConsumerDispatcher.DefaultConsumer;
            set => ConsumerDispatcher.DefaultConsumer = value;
        }

        public bool IsClosed => !IsOpen;

        [MemberNotNullWhen(false, nameof(CloseReason))]
        public bool IsOpen => CloseReason is null;

        public string? CurrentQueue { get; private set; }

        public ISession Session { get; private set; }

        public Exception? ConnectionStartException => m_connectionStartException;

        public void MaybeSetConnectionStartException(Exception ex)
        {
            if (m_connectionStartCell != null)
            {
                m_connectionStartException = ex;
            }
        }

        protected void TakeOver(ChannelBase other)
        {
            _basicAcksAsyncWrapper.Takeover(other._basicAcksAsyncWrapper);
            _basicNacksAsyncWrapper.Takeover(other._basicNacksAsyncWrapper);
            _basicReturnAsyncWrapper.Takeover(other._basicReturnAsyncWrapper);
            _callbackExceptionAsyncWrapper.Takeover(other._callbackExceptionAsyncWrapper);
            _flowControlAsyncWrapper.Takeover(other._flowControlAsyncWrapper);
            _channelShutdownAsyncWrapper.Takeover(other._channelShutdownAsyncWrapper);
            _recoveryAsyncWrapper.Takeover(other._recoveryAsyncWrapper);
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
                ChannelShutdownAsync += k.OnConnectionShutdownAsync;
                enqueued = Enqueue(k);
                ConsumerDispatcher.Quiesce();

                if (SetCloseReason(args))
                {
                    var method = new ChannelClose(
                        args.ReplyCode, args.ReplyText, args.ClassId, args.MethodId);
                    await ModelSendAsync(in method, k.CancellationToken)
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
                if (false == enqueued)
                {
                    k.Dispose();
                }
                _rpcSemaphore.Release();
                ChannelShutdownAsync -= k.OnConnectionShutdownAsync;
            }
        }

        internal async ValueTask ConnectionOpenAsync(string virtualHost, CancellationToken cancellationToken)
        {
            using var timeoutTokenSource = new CancellationTokenSource(HandshakeContinuationTimeout);
            using var lts = CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, cancellationToken);
            var method = new ConnectionOpen(virtualHost);
            // Note: must be awaited or else the timeoutTokenSource instance will be disposed
            await ModelSendAsync(in method, lts.Token).ConfigureAwait(false);
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
                    await ModelSendAsync(in method, k.CancellationToken)
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
            IDictionary<string, object?> clientProperties,
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
                    await ModelSendAsync(in method, k.CancellationToken)
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
                await ModelSendAsync(in method, k.CancellationToken)
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
            ShutdownEventArgs? reason = CloseReason;
            if (reason != null)
            {
                await Session.CloseAsync(reason)
                    .ConfigureAwait(false);
            }

            m_connectionStartCell?.TrySetResult(null);
        }

        [MemberNotNullWhen(true, nameof(_confirmSemaphore))]
        private bool ConfirmsAreEnabled => _confirmSemaphore != null;

        private async Task HandleCommandAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            /*
             * If DispatchCommandAsync returns `true`, it means that the incoming command is server-originated, and has
             * already been handled.
             *
             * Else, the incoming command is the return of an RPC call, and must be handled.
             */
            try
            {
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
            finally
            {
                cmd.ReturnBuffers();
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
            return Session.TransmitAsync(in method, in header, body, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected ValueTask EnforceFlowControlAsync(CancellationToken cancellationToken)
        {
            if (_flowControlBlock.IsSet)
            {
                return default;
            }

            return _flowControlBlock.WaitAsync(cancellationToken);
        }

        internal Task OnCallbackExceptionAsync(CallbackExceptionEventArgs args)
        {
            return _callbackExceptionAsyncWrapper.InvokeAsync(this, args);
        }

        ///<summary>Broadcasts notification of the final shutdown of the channel.</summary>
        ///<remarks>
        ///<para>
        ///Do not call anywhere other than at the end of OnSessionShutdownAsync.
        ///</para>
        ///<para>
        ///Must not be called when m_closeReason is null, because
        ///otherwise there's a window when a new continuation could be
        ///being enqueued at the same time as we're broadcasting the
        ///shutdown event. See the definition of Enqueue() above.
        ///</para>
        ///</remarks>
        private async Task OnChannelShutdownAsync(ShutdownEventArgs reason)
        {
            _continuationQueue.HandleChannelShutdown(reason);
            await _channelShutdownAsyncWrapper.InvokeAsync(this, reason)
                .ConfigureAwait(false);

            if (ConfirmsAreEnabled)
            {
                await _confirmSemaphore.WaitAsync(reason.CancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    if (_confirmsTaskCompletionSources?.Count > 0)
                    {
                        var exception = new AlreadyClosedException(reason);
                        foreach (TaskCompletionSource<bool> confirmsTaskCompletionSource in _confirmsTaskCompletionSources)
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

        /*
         * Note:
         * Attempting to make this method async, with the resulting fallout,
         * resulted in many flaky test results, especially around disposing
         * Channels/Connections
         *
         * Aborted PR: https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1551
         */
        private async Task OnSessionShutdownAsync(object? sender, ShutdownEventArgs reason)
        {
            ConsumerDispatcher.Quiesce();
            SetCloseReason(reason);
            await OnChannelShutdownAsync(reason)
                .ConfigureAwait(false);
            await ConsumerDispatcher.ShutdownAsync(reason)
                .ConfigureAwait(false);
        }

        [MemberNotNull(nameof(_closeReason))]
        internal bool SetCloseReason(ShutdownEventArgs reason)
        {
            if (reason is null)
            {
                throw new ArgumentNullException(nameof(reason));
            }

            // NB: this ensures that CloseAsync is only called once on a channel
            return Interlocked.CompareExchange(ref _closeReason, reason, null) is null;
        }

        public override string ToString()
            => Session.ToString()!;

        void IDisposable.Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (IsOpen)
                {
                    this.AbortAsync().GetAwaiter().GetResult();
                }

                ConsumerDispatcher.Dispose();
                _rpcSemaphore.Dispose();
                _confirmSemaphore?.Dispose();
            }
        }

        public Task ConnectionTuneOkAsync(ushort channelMax, uint frameMax, ushort heartbeat, CancellationToken cancellationToken)
        {
            var method = new ConnectionTuneOk(channelMax, frameMax, heartbeat);
            return ModelSendAsync(in method, cancellationToken).AsTask();
        }

        protected async Task<bool> HandleBasicAck(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            var ack = new BasicAck(cmd.MethodSpan);
            if (!_basicAcksAsyncWrapper.IsEmpty)
            {
                var args = new BasicAckEventArgs(ack._deliveryTag, ack._multiple, cancellationToken);
                await _basicAcksAsyncWrapper.InvokeAsync(this, args)
                    .ConfigureAwait(false);
            }

            await HandleAckNack(ack._deliveryTag, ack._multiple, false, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        protected async Task<bool> HandleBasicNack(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            var nack = new BasicNack(cmd.MethodSpan);
            if (!_basicNacksAsyncWrapper.IsEmpty)
            {
                var args = new BasicNackEventArgs(
                    nack._deliveryTag, nack._multiple, nack._requeue, cancellationToken);
                await _basicNacksAsyncWrapper.InvokeAsync(this, args)
                    .ConfigureAwait(false);
            }

            await HandleAckNack(nack._deliveryTag, nack._multiple, true, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        protected async Task<bool> HandleBasicCancelAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            string consumerTag = new BasicCancel(cmd.MethodSpan)._consumerTag;
            await ConsumerDispatcher.HandleBasicCancelAsync(consumerTag, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        protected async Task<bool> HandleBasicDeliverAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            var method = new BasicDeliver(cmd.MethodSpan);
            var header = new ReadOnlyBasicProperties(cmd.HeaderSpan);
            await ConsumerDispatcher.HandleBasicDeliverAsync(
                    method._consumerTag,
                    AdjustDeliveryTag(method._deliveryTag),
                    method._redelivered,
                    method._exchange,
                    method._routingKey,
                    header,
                    /*
                     * Takeover Body so it doesn't get returned as it is necessary
                     * for handling the Basic.Deliver method by client code.
                     */
                    cmd.TakeoverBody(),
                    cancellationToken).ConfigureAwait(false);
            return true;
        }

        protected virtual ulong AdjustDeliveryTag(ulong deliveryTag)
        {
            return deliveryTag;
        }

        protected async Task<bool> HandleBasicReturn(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            if (!_basicReturnAsyncWrapper.IsEmpty)
            {
                var basicReturn = new BasicReturn(cmd.MethodSpan);
                var e = new BasicReturnEventArgs(basicReturn._replyCode, basicReturn._replyText,
                    basicReturn._exchange, basicReturn._routingKey,
                    new ReadOnlyBasicProperties(cmd.HeaderSpan), cmd.Body.Memory, cancellationToken);
                await _basicReturnAsyncWrapper.InvokeAsync(this, e)
                    .ConfigureAwait(false);
            }
            return true;
        }

        protected async Task<bool> HandleChannelCloseAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            var channelClose = new ChannelClose(cmd.MethodSpan);
            SetCloseReason(new ShutdownEventArgs(ShutdownInitiator.Peer,
                channelClose._replyCode,
                channelClose._replyText,
                channelClose._classId,
                channelClose._methodId));

            await Session.CloseAsync(_closeReason, notify: false)
                .ConfigureAwait(false);

            var method = new ChannelCloseOk();
            await ModelSendAsync(in method, cancellationToken)
                .ConfigureAwait(false);

            await Session.NotifyAsync(cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        protected async Task<bool> HandleChannelCloseOkAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            /*
             * Note:
             * This call _must_ come before completing the async continuation
             */
            await FinishCloseAsync(cancellationToken)
                .ConfigureAwait(false);

            if (_continuationQueue.TryPeek<ChannelCloseAsyncRpcContinuation>(out ChannelCloseAsyncRpcContinuation? k))
            {
                _continuationQueue.Next();
                await k.HandleCommandAsync(cmd)
                    .ConfigureAwait(false);
            }

            return true;
        }

        protected async Task<bool> HandleChannelFlowAsync(IncomingCommand cmd, CancellationToken cancellationToken)
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
            await ModelSendAsync(in method, cancellationToken).
                ConfigureAwait(false);

            if (!_flowControlAsyncWrapper.IsEmpty)
            {
                await _flowControlAsyncWrapper.InvokeAsync(this, new FlowControlEventArgs(active, cancellationToken))
                    .ConfigureAwait(false);
            }

            return true;
        }

        protected async Task<bool> HandleConnectionBlockedAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            string reason = new ConnectionBlocked(cmd.MethodSpan)._reason;
            await Session.Connection.HandleConnectionBlockedAsync(reason, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        protected async Task<bool> HandleConnectionCloseAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            var method = new ConnectionClose(cmd.MethodSpan);
            var reason = new ShutdownEventArgs(ShutdownInitiator.Peer, method._replyCode, method._replyText, method._classId, method._methodId);
            try
            {
                await Session.Connection.ClosedViaPeerAsync(reason, cancellationToken)
                    .ConfigureAwait(false);

                var replyMethod = new ConnectionCloseOk();
                await ModelSendAsync(in replyMethod, cancellationToken)
                    .ConfigureAwait(false);

                SetCloseReason(Session.Connection.CloseReason!);
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

        protected async Task<bool> HandleConnectionSecureAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            var k = (ConnectionSecureOrTuneAsyncRpcContinuation)_continuationQueue.Next();
            await k.HandleCommandAsync(new IncomingCommand())
                .ConfigureAwait(false); // release the continuation.
            return true;
        }

        protected async Task<bool> HandleConnectionStartAsync(IncomingCommand cmd, CancellationToken cancellationToken)
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
                var details = new ConnectionStartDetails(method._locales, method._mechanisms,
                    method._serverProperties, method._versionMajor, method._versionMinor);
                m_connectionStartCell.SetResult(details);
                m_connectionStartCell = null;
            }

            return true;
        }

        protected async Task<bool> HandleConnectionTuneAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            // Note: `using` here to ensure instance is disposed
            using var k = (ConnectionSecureOrTuneAsyncRpcContinuation)_continuationQueue.Next();

            // Note: releases the continuation and returns the buffers
            await k.HandleCommandAsync(cmd)
                .ConfigureAwait(false);

            return true;
        }

        protected async Task<bool> HandleConnectionUnblockedAsync(CancellationToken cancellationToken)
        {
            await Session.Connection.HandleConnectionUnblockedAsync(cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        public async ValueTask<ulong> GetNextPublishSequenceNumberAsync(CancellationToken cancellationToken = default)
        {
            if (ConfirmsAreEnabled)
            {
                await _confirmSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    return _nextPublishSeqNo;
                }
                finally
                {
                    _confirmSemaphore.Release();
                }
            }
            else
            {
                return _nextPublishSeqNo;
            }
        }

        public abstract ValueTask BasicAckAsync(ulong deliveryTag, bool multiple,
            CancellationToken cancellationToken);

        public abstract ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue,
            CancellationToken cancellationToken);

        public abstract ValueTask BasicRejectAsync(ulong deliveryTag, bool requeue,
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
                    await ModelSendAsync(in method, k.CancellationToken)
                        .ConfigureAwait(false);
                    ConsumerDispatcher.GetAndRemoveConsumer(consumerTag);
                }
                else
                {
                    enqueued = Enqueue(k);

                    await ModelSendAsync(in method, k.CancellationToken)
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
            IDictionary<string, object?>? arguments, IAsyncBasicConsumer consumer,
            CancellationToken cancellationToken)
        {
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

                var method = new BasicConsume(queue, consumerTag, noLocal, autoAck, exclusive, false, arguments);
                await ModelSendAsync(in method, k.CancellationToken)
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

        public async Task<BasicGetResult?> BasicGetAsync(string queue, bool autoAck,
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
                await ModelSendAsync(in method, k.CancellationToken)
                    .ConfigureAwait(false);

                BasicGetResult? result = await k;

                using Activity? activity = result != null
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

        public async ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey,
            bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken = default)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            if (ConfirmsAreEnabled)
            {
                await _confirmSemaphore.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    if (_trackConfirmations)
                    {
                        if (_pendingDeliveryTags is null)
                        {
                            throw new InvalidOperationException(InternalConstants.BugFound);
                        }
                        _pendingDeliveryTags.AddLast(_nextPublishSeqNo);
                    }

                    _nextPublishSeqNo++;
                }
                finally
                {
                    _confirmSemaphore.Release();
                }
            }

            try
            {
                var cmd = new BasicPublish(exchange, routingKey, mandatory, default);
                using Activity? sendActivity = RabbitMQActivitySource.PublisherHasListeners
                    ? RabbitMQActivitySource.Send(routingKey, exchange, body.Length)
                    : default;

                if (sendActivity != null)
                {
                    BasicProperties? props = PopulateActivityAndPropagateTraceId(basicProperties, sendActivity);
                    if (props is null)
                    {
                        await EnforceFlowControlAsync(cancellationToken)
                            .ConfigureAwait(false);
                        await ModelSendAsync(in cmd, in basicProperties, body, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await EnforceFlowControlAsync(cancellationToken)
                            .ConfigureAwait(false);
                        await ModelSendAsync(in cmd, in props, body, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                else
                {
                    await EnforceFlowControlAsync(cancellationToken)
                        .ConfigureAwait(false);
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
                        _nextPublishSeqNo--;
                        if (_trackConfirmations && _pendingDeliveryTags is not null)
                        {
                            _pendingDeliveryTags.RemoveLast();
                        }
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
            bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken = default)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            if (ConfirmsAreEnabled)
            {
                await _confirmSemaphore.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    if (_trackConfirmations)
                    {
                        if (_pendingDeliveryTags is null)
                        {
                            throw new InvalidOperationException(InternalConstants.BugFound);
                        }
                        _pendingDeliveryTags.AddLast(_nextPublishSeqNo);
                    }

                    _nextPublishSeqNo++;
                }
                finally
                {
                    _confirmSemaphore.Release();
                }
            }

            try
            {
                var cmd = new BasicPublishMemory(exchange.Bytes, routingKey.Bytes, mandatory, default);
                using Activity? sendActivity = RabbitMQActivitySource.PublisherHasListeners
                    ? RabbitMQActivitySource.Send(routingKey.Value, exchange.Value, body.Length)
                    : default;

                if (sendActivity != null)
                {
                    BasicProperties? props = PopulateActivityAndPropagateTraceId(basicProperties, sendActivity);
                    if (props is null)
                    {
                        await EnforceFlowControlAsync(cancellationToken)
                            .ConfigureAwait(false);
                        await ModelSendAsync(in cmd, in basicProperties, body, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await EnforceFlowControlAsync(cancellationToken)
                            .ConfigureAwait(false);
                        await ModelSendAsync(in cmd, in props, body, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                else
                {
                    await EnforceFlowControlAsync(cancellationToken)
                        .ConfigureAwait(false);
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
                        _nextPublishSeqNo--;
                        if (_trackConfirmations && _pendingDeliveryTags is not null)
                        {
                            _pendingDeliveryTags.RemoveLast();
                        }
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
                await ModelSendAsync(in method, k.CancellationToken)
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
                await ModelSendAsync(in method, k.CancellationToken)
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

        public async Task ConfirmSelectAsync(bool trackConfirmations = true, CancellationToken cancellationToken = default)
        {
            _trackConfirmations = trackConfirmations;
            bool enqueued = false;
            var k = new ConfirmSelectAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

            await _rpcSemaphore.WaitAsync(k.CancellationToken)
                .ConfigureAwait(false);
            try
            {
                if (_nextPublishSeqNo == 0UL)
                {
                    if (_trackConfirmations)
                    {
                        _pendingDeliveryTags = new LinkedList<ulong>();
                        _confirmsTaskCompletionSources = new List<TaskCompletionSource<bool>>();
                    }
                    _nextPublishSeqNo = 1;
                }

                enqueued = Enqueue(k);

                var method = new ConfirmSelect(false);
                await ModelSendAsync(in method, k.CancellationToken)
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
            IDictionary<string, object?>? arguments, bool noWait,
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
                    await ModelSendAsync(in method, k.CancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    enqueued = Enqueue(k);

                    await ModelSendAsync(in method, k.CancellationToken)
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
            IDictionary<string, object?>? arguments, bool passive, bool noWait,
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
                    await ModelSendAsync(in method, k.CancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    enqueued = Enqueue(k);

                    await ModelSendAsync(in method, k.CancellationToken)
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
                    await ModelSendAsync(in method, k.CancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    enqueued = Enqueue(k);

                    await ModelSendAsync(in method, k.CancellationToken)
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
            IDictionary<string, object?>? arguments, bool noWait,
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
                    await ModelSendAsync(in method, k.CancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    enqueued = Enqueue(k);

                    await ModelSendAsync(in method, k.CancellationToken)
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
            IDictionary<string, object?>? arguments, bool passive, bool noWait,
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

                    await ModelSendAsync(in method, k.CancellationToken)
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

                    await ModelSendAsync(in method, k.CancellationToken)
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
            IDictionary<string, object?>? arguments, bool noWait,
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
                    await ModelSendAsync(in method, k.CancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    enqueued = Enqueue(k);

                    await ModelSendAsync(in method, k.CancellationToken)
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
                    await ModelSendAsync(in method, k.CancellationToken)
                        .ConfigureAwait(false);

                    return 0;
                }
                else
                {
                    enqueued = Enqueue(k);

                    await ModelSendAsync(in method, k.CancellationToken)
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
                await ModelSendAsync(in method, k.CancellationToken)
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
            IDictionary<string, object?>? arguments,
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
                await ModelSendAsync(in method, k.CancellationToken)
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
                await ModelSendAsync(in method, k.CancellationToken)
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
                await ModelSendAsync(in method, k.CancellationToken)
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
                await ModelSendAsync(in method, k.CancellationToken)
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

        public async Task<bool> WaitForConfirmsAsync(CancellationToken cancellationToken = default)
        {
            if (false == ConfirmsAreEnabled)
            {
                throw new InvalidOperationException("Confirms not selected");
            }

            if (false == _trackConfirmations)
            {
                throw new InvalidOperationException("Confirmation tracking is not enabled");
            }

            if (_pendingDeliveryTags is null)
            {
                throw new InvalidOperationException(InternalConstants.BugFound);
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
                _confirmsTaskCompletionSources!.Add(tcs);
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
            if (false == ConfirmsAreEnabled)
            {
                throw new InvalidOperationException("Confirms not selected");
            }

            if (false == _trackConfirmations)
            {
                throw new InvalidOperationException("Confirmation tracking is not enabled");
            }

            if (_pendingDeliveryTags is null)
            {
                throw new InvalidOperationException(InternalConstants.BugFound);
            }

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
            if (false == ConfirmsAreEnabled)
            {
                throw new InvalidOperationException("Confirms not selected");
            }

            if (false == _trackConfirmations)
            {
                throw new InvalidOperationException("Confirmation tracking is not enabled");
            }

            if (_pendingDeliveryTags is null)
            {
                throw new InvalidOperationException(InternalConstants.BugFound);
            }

            CancellationTokenRegistration tokenRegistration =
#if NET6_0_OR_GREATER
                cancellationToken.UnsafeRegister(
                    state => ((TaskCompletionSource<bool>)state!).TrySetCanceled(), tcs);
#else
                cancellationToken.Register(
                    state => ((TaskCompletionSource<bool>)state!).TrySetCanceled(),
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

        // NOTE: this method is internal for its use in this test:
        // TestWaitForConfirmsWithTimeoutAsync_MessageNacked_WaitingHasTimedout_ReturnFalse
        internal async Task HandleAckNack(ulong deliveryTag, bool multiple, bool isNack, CancellationToken cancellationToken = default)
        {
            // Only do this if confirms are enabled *and* the library is tracking confirmations
            if (ConfirmsAreEnabled && _trackConfirmations)
            {
                if (_pendingDeliveryTags is null)
                {
                    throw new InvalidOperationException(InternalConstants.BugFound);
                }
                // let's take a lock so we can assume that deliveryTags are unique, never duplicated and always sorted
                await _confirmSemaphore.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    // No need to do anything if there are no delivery tags in the list
                    if (_pendingDeliveryTags.Count > 0)
                    {
                        if (multiple)
                        {
                            while (_pendingDeliveryTags.First!.Value < deliveryTag)
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

                    if (_pendingDeliveryTags.Count == 0 && _confirmsTaskCompletionSources!.Count > 0)
                    {
                        // Done, mark tasks
                        foreach (TaskCompletionSource<bool> confirmsTaskCompletionSource in _confirmsTaskCompletionSources)
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

        private static BasicProperties? PopulateActivityAndPropagateTraceId<TProperties>(TProperties basicProperties,
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

            IDictionary<string, object?>? headers = basicProperties.Headers;
            if (headers is null)
            {
                return AddHeaders(basicProperties, sendActivity);
            }

            // Inject the ActivityContext into the message headers to propagate trace context to the receiving service.
            RabbitMQActivitySource.ContextInjector(sendActivity, headers);
            return null;

            static BasicProperties? AddHeaders(TProperties basicProperties, Activity sendActivity)
            {
                var headers = new Dictionary<string, object?>();

                // Inject the ActivityContext into the message headers to propagate trace context to the receiving service.
                RabbitMQActivitySource.ContextInjector(sendActivity, headers);

                switch (basicProperties)
                {
                    case BasicProperties writableProperties:
                        writableProperties.Headers = headers;
                        return null;
                    case EmptyBasicProperty:
                        return new BasicProperties { Headers = headers };
                    default:
                        return new BasicProperties(basicProperties) { Headers = headers };
                }
            }
        }
    }
}
