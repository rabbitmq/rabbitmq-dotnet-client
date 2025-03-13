// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
    internal partial class Channel : IChannel, IRecoverable
    {
        ///<summary>Only used to kick-start a connection open
        ///sequence. See <see cref="Connection.OpenAsync"/> </summary>
        internal TaskCompletionSource<ConnectionStartDetails?>? m_connectionStartCell;
        private Exception? m_connectionStartException;

        // AMQP only allows one RPC operation to be active at a time.
        protected readonly SemaphoreSlim _rpcSemaphore = new SemaphoreSlim(1, 1);
        private readonly RpcContinuationQueue _continuationQueue = new RpcContinuationQueue();

        private ShutdownEventArgs? _closeReason;
        public ShutdownEventArgs? CloseReason => Volatile.Read(ref _closeReason);

        private TaskCompletionSource<bool>? _serverOriginatedChannelCloseTcs;

        internal readonly IConsumerDispatcher ConsumerDispatcher;

        private bool _disposed;
        private int _isDisposing;

        private CancellationTokenSource _shutdownCts = new CancellationTokenSource();
        public CancellationTokenSource ShutdownCts => _shutdownCts;
        
        public Channel(ISession session, CreateChannelOptions createChannelOptions)
        {
            ContinuationTimeout = createChannelOptions.ContinuationTimeout;
            ConsumerDispatcher = new AsyncConsumerDispatcher(this, createChannelOptions.InternalConsumerDispatchConcurrency);
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

        protected void TakeOver(Channel other)
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
            _shutdownCts.Cancel();
            
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
                MaybeDisposeContinuation(enqueued, k);
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
                MaybeDisposeContinuation(enqueued, k);
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
                MaybeDisposeContinuation(enqueued, k);
                _rpcSemaphore.Release();
            }
        }

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

        internal async Task<IChannel> OpenAsync(CreateChannelOptions createChannelOptions,
            CancellationToken cancellationToken)
        {
            ConfigurePublisherConfirmations(createChannelOptions.PublisherConfirmationsEnabled,
                createChannelOptions.PublisherConfirmationTrackingEnabled,
                createChannelOptions.OutstandingPublisherConfirmationsRateLimiter);

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

                await MaybeConfirmSelect(cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                MaybeDisposeContinuation(enqueued, k);
                _rpcSemaphore.Release();
            }

            return this;
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

            await MaybeHandlePublisherConfirmationTcsOnChannelShutdownAsync(reason)
                .ConfigureAwait(false);

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
            if (_disposed)
            {
                return;
            }

            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (IsDisposing)
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

                    _serverOriginatedChannelCloseTcs?.Task.Wait(TimeSpan.FromSeconds(5));

                    ConsumerDispatcher.Dispose();
                    _rpcSemaphore.Dispose();
                    _confirmSemaphore.Dispose();
                    _outstandingPublisherConfirmationsRateLimiter?.Dispose();
                }
                finally
                {
                    _disposed = true;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            await DisposeAsyncCore()
                .ConfigureAwait(false);

            Dispose(false);
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (_disposed)
            {
                return;
            }

            if (IsDisposing)
            {
                return;
            }

            try
            {
                if (IsOpen)
                {
                    await this.AbortAsync()
                        .ConfigureAwait(false);
                }

                if (_serverOriginatedChannelCloseTcs is not null)
                {
                    await _serverOriginatedChannelCloseTcs.Task.WaitAsync(TimeSpan.FromSeconds(5))
                        .ConfigureAwait(false);
                }

                ConsumerDispatcher.Dispose();
                _rpcSemaphore.Dispose();
                _confirmSemaphore.Dispose();

                if (_outstandingPublisherConfirmationsRateLimiter is not null)
                {
                    await _outstandingPublisherConfirmationsRateLimiter.DisposeAsync()
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                _disposed = true;
            }
        }

        public Task ConnectionTuneOkAsync(ushort channelMax, uint frameMax, ushort heartbeat, CancellationToken cancellationToken)
        {
            var method = new ConnectionTuneOk(channelMax, frameMax, heartbeat);
            return ModelSendAsync(in method, cancellationToken).AsTask();
        }

        protected async Task<bool> HandleBasicAck(IncomingCommand cmd,
            CancellationToken cancellationToken = default)
        {
            var ack = new BasicAck(cmd.MethodSpan);
            if (!_basicAcksAsyncWrapper.IsEmpty)
            {
                var args = new BasicAckEventArgs(ack._deliveryTag, ack._multiple, cancellationToken);
                await _basicAcksAsyncWrapper.InvokeAsync(this, args)
                    .ConfigureAwait(false);
            }

            HandleAck(ack._deliveryTag, ack._multiple);

            return true;
        }

        protected async Task<bool> HandleBasicNack(IncomingCommand cmd,
            CancellationToken cancellationToken = default)
        {
            var nack = new BasicNack(cmd.MethodSpan);
            if (!_basicNacksAsyncWrapper.IsEmpty)
            {
                var args = new BasicNackEventArgs(
                    nack._deliveryTag, nack._multiple, nack._requeue, cancellationToken);
                await _basicNacksAsyncWrapper.InvokeAsync(this, args)
                    .ConfigureAwait(false);
            }

            HandleNack(nack._deliveryTag, nack._multiple, false);

            return true;
        }

        protected async Task<bool> HandleBasicReturn(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            var basicReturn = new BasicReturn(cmd.MethodSpan);

            var e = new BasicReturnEventArgs(basicReturn._replyCode, basicReturn._replyText,
                basicReturn._exchange, basicReturn._routingKey,
                new ReadOnlyBasicProperties(cmd.HeaderSpan), cmd.Body.Memory, cancellationToken);

            if (!_basicReturnAsyncWrapper.IsEmpty)
            {
                await _basicReturnAsyncWrapper.InvokeAsync(this, e)
                    .ConfigureAwait(false);
            }

            HandleReturn(e);

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

        protected async Task<bool> HandleChannelCloseAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool>? serverOriginatedChannelCloseTcs = _serverOriginatedChannelCloseTcs;
            if (serverOriginatedChannelCloseTcs is null)
            {
                // Attempt to assign the new TCS only if _tcs is still null
                _ = Interlocked.CompareExchange(ref _serverOriginatedChannelCloseTcs,
                    new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously), null);
            }

            try
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

                _serverOriginatedChannelCloseTcs?.TrySetResult(true);
                return true;
            }
            catch (Exception ex)
            {
                _serverOriginatedChannelCloseTcs?.TrySetException(ex);
                throw;
            }
        }

        protected async Task<bool> HandleChannelCloseOkAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            /*
             * Note:
             * This call _must_ come before completing the async continuation
             */
            await FinishCloseAsync(cancellationToken)
                .ConfigureAwait(false);

            if (_continuationQueue.TryPeek(out ChannelCloseAsyncRpcContinuation? k))
            {
                using (IRpcContinuation c = _continuationQueue.Next())
                {
                    await k.HandleCommandAsync(cmd)
                        .ConfigureAwait(false);
                }
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
                /*
                 * rabbitmq-dotnet-client#1777
                 * Send the connection.close-ok message prior to closing within the client,
                 * because ClosedViaPeerAsync will stop the main loop
                 */
                var replyMethod = new ConnectionCloseOk();
                await ModelSendAsync(in replyMethod, cancellationToken)
                    .ConfigureAwait(false);

                await Session.Connection.ClosedViaPeerAsync(reason, cancellationToken)
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
            using (var k = (ConnectionSecureOrTuneAsyncRpcContinuation)_continuationQueue.Next())
            {
                await k.HandleCommandAsync(new IncomingCommand())
                    .ConfigureAwait(false); // release the continuation.
                return true;
            }
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
            using (var k = (ConnectionSecureOrTuneAsyncRpcContinuation)_continuationQueue.Next())
            {
                // Note: releases the continuation and returns the buffers
                await k.HandleCommandAsync(cmd)
                    .ConfigureAwait(false);
            }

            return true;
        }

        protected async Task<bool> HandleConnectionUnblockedAsync(CancellationToken cancellationToken)
        {
            await Session.Connection.HandleConnectionUnblockedAsync(cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        public virtual ValueTask BasicAckAsync(ulong deliveryTag, bool multiple,
            CancellationToken cancellationToken)
        {
            var method = new BasicAck(deliveryTag, multiple);
            return ModelSendAsync(in method, cancellationToken);
        }

        public virtual ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue,
            CancellationToken cancellationToken)
        {
            var method = new BasicNack(deliveryTag, multiple, requeue);
            return ModelSendAsync(in method, cancellationToken);
        }

        public virtual ValueTask BasicRejectAsync(ulong deliveryTag, bool requeue,
            CancellationToken cancellationToken)
        {
            var method = new BasicReject(deliveryTag, requeue);
            return ModelSendAsync(in method, cancellationToken);
        }

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
                MaybeDisposeContinuation(enqueued, k);
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
                MaybeDisposeContinuation(enqueued, k);
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
                    ? RabbitMQActivitySource.BasicGet(result.RoutingKey,
                        result.Exchange,
                        result.DeliveryTag, result.BasicProperties, result.Body.Length)
                    : RabbitMQActivitySource.BasicGetEmpty(queue);

                activity?.SetStartTime(k.StartTime);

                return result;
            }
            finally
            {
                MaybeDisposeContinuation(enqueued, k);
                _rpcSemaphore.Release();
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
                MaybeDisposeContinuation(enqueued, k);
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
                MaybeDisposeContinuation(enqueued, k);
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
                MaybeDisposeContinuation(enqueued, k);
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
                MaybeDisposeContinuation(enqueued, k);
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
                MaybeDisposeContinuation(enqueued, k);
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
                MaybeDisposeContinuation(enqueued, k);
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
                MaybeDisposeContinuation(enqueued, k);
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
                MaybeDisposeContinuation(enqueued, k);
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
                MaybeDisposeContinuation(enqueued, k);
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
                MaybeDisposeContinuation(enqueued, k);
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
                enqueued = Enqueue(k);

                var method = new QueueUnbind(queue, exchange, routingKey, arguments);
                await ModelSendAsync(in method, k.CancellationToken)
                    .ConfigureAwait(false);

                bool result = await k;
                Debug.Assert(result);
                return;
            }
            finally
            {
                MaybeDisposeContinuation(enqueued, k);
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
                MaybeDisposeContinuation(enqueued, k);
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
                MaybeDisposeContinuation(enqueued, k);
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
                enqueued = Enqueue(k);

                var method = new TxSelect();
                await ModelSendAsync(in method, k.CancellationToken)
                    .ConfigureAwait(false);

                bool result = await k;
                Debug.Assert(result);
                return;
            }
            finally
            {
                MaybeDisposeContinuation(enqueued, k);
                _rpcSemaphore.Release();
            }
        }

        internal static Task<IChannel> CreateAndOpenAsync(CreateChannelOptions createChannelOptions, ISession session,
            CancellationToken cancellationToken)
        {
            var channel = new Channel(session, createChannelOptions);
            return channel.OpenAsync(createChannelOptions, cancellationToken);
        }

        private void MaybeDisposeContinuation(bool enqueued, IRpcContinuation continuation)
        {
            try
            {
                if (enqueued)
                {
                    if (_continuationQueue.TryPeek(out IRpcContinuation? enqueuedContinuation))
                    {
                        if (object.ReferenceEquals(continuation, enqueuedContinuation))
                        {
                            IRpcContinuation dequeuedContinuation = _continuationQueue.Next();
                            dequeuedContinuation.Dispose();
                        }
                    }
                }
                else
                {
                    continuation.Dispose();
                }
            }
            catch
            {
                // TODO low-level debug logging
            }
        }

        /// <summary>
        /// Returning <c>true</c> from this method means that the command was server-originated,
        /// and handled already.
        /// Returning <c>false</c> (the default) means that the incoming command is the response to
        /// a client-initiated RPC call, and must be handled.
        /// </summary>
        /// <param name="cmd">The incoming command from the AMQP server</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns></returns>
        private Task<bool> DispatchCommandAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            switch (cmd.CommandId)
            {
                case ProtocolCommandId.BasicCancel:
                    {
                        // Note: always returns true
                        return HandleBasicCancelAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.BasicDeliver:
                    {
                        // Note: always returns true
                        return HandleBasicDeliverAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.BasicAck:
                    {
                        return HandleBasicAck(cmd, cancellationToken);
                    }
                case ProtocolCommandId.BasicNack:
                    {
                        return HandleBasicNack(cmd, cancellationToken);
                    }
                case ProtocolCommandId.BasicReturn:
                    {
                        // Note: always returns true
                        return HandleBasicReturn(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ChannelClose:
                    {
                        // Note: always returns true
                        return HandleChannelCloseAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ChannelCloseOk:
                    {
                        // Note: always returns true
                        return HandleChannelCloseOkAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ChannelFlow:
                    {
                        // Note: always returns true
                        return HandleChannelFlowAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ConnectionBlocked:
                    {
                        // Note: always returns true
                        return HandleConnectionBlockedAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ConnectionClose:
                    {
                        // Note: always returns true
                        return HandleConnectionCloseAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ConnectionSecure:
                    {
                        // Note: always returns true
                        return HandleConnectionSecureAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ConnectionStart:
                    {
                        // Note: always returns true
                        return HandleConnectionStartAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ConnectionTune:
                    {
                        // Note: always returns true
                        return HandleConnectionTuneAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ConnectionUnblocked:
                    {
                        // Note: always returns true
                        return HandleConnectionUnblockedAsync(cancellationToken);
                    }
                default:
                    {
                        return Task.FromResult(false);
                    }
            }
        }

        private bool IsDisposing
        {
            get
            {
                if (Interlocked.Exchange(ref _isDisposing, 1) != 0)
                {
                    return true;
                }

                return false;
            }
        }
    }
}
