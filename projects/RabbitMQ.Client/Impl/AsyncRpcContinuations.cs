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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.ConsumerDispatching;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Impl
{
    internal abstract class AsyncRpcContinuation<T> : IRpcContinuation
    {
        private readonly TimeSpan _continuationTimeout;
        private readonly CancellationToken _rpcCancellationToken;
        private readonly CancellationToken _continuationTimeoutCancellationToken;
        private readonly CancellationTokenSource _continuationTimeoutCancellationTokenSource;
        private readonly CancellationTokenRegistration _continuationTimeoutCancellationTokenRegistration;
        private readonly CancellationTokenSource _linkedCancellationTokenSource;
        private readonly ConfiguredTaskAwaitable<T> _tcsConfiguredTaskAwaitable;
        protected readonly TaskCompletionSource<T> _tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        private bool _disposedValue;

        public AsyncRpcContinuation(TimeSpan continuationTimeout, CancellationToken rpcCancellationToken)
        {
            _continuationTimeout = continuationTimeout;
            _rpcCancellationToken = rpcCancellationToken;

            /*
             * Note: we can't use an ObjectPool for these because the netstandard2.0
             * version of CancellationTokenSource can't be reset prior to checking
             * in to the ObjectPool
             */
            _continuationTimeoutCancellationTokenSource = new CancellationTokenSource(continuationTimeout);
            _continuationTimeoutCancellationToken = _continuationTimeoutCancellationTokenSource.Token;

#if NET
            _continuationTimeoutCancellationTokenRegistration =
                _continuationTimeoutCancellationToken.UnsafeRegister(
                    callback: HandleContinuationTimeout, state: _tcs);
#else
            _continuationTimeoutCancellationTokenRegistration =
                _continuationTimeoutCancellationToken.Register(
                    callback: HandleContinuationTimeout, state: _tcs, useSynchronizationContext: false);
#endif

            _tcsConfiguredTaskAwaitable = _tcs.Task.ConfigureAwait(false);

            _linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                _continuationTimeoutCancellationTokenSource.Token, rpcCancellationToken);
        }

        public CancellationToken CancellationToken
        {
            get
            {
                return _linkedCancellationTokenSource.Token;
            }
        }

        public ConfiguredTaskAwaitable<T>.ConfiguredTaskAwaiter GetAwaiter()
        {
            return _tcsConfiguredTaskAwaitable.GetAwaiter();
        }

        public abstract ProtocolCommandId[] HandledProtocolCommandIds { get; }

        public async Task HandleCommandAsync(IncomingCommand cmd)
        {
            try
            {
                await DoHandleCommandAsync(cmd)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (_rpcCancellationToken.IsCancellationRequested)
                {
#if NET
                    _tcs.TrySetCanceled(_rpcCancellationToken);
#else
                    _tcs.TrySetCanceled();
#endif
                }
                else if (_continuationTimeoutCancellationToken.IsCancellationRequested)
                {
#if NET
                    if (_tcs.TrySetCanceled(_continuationTimeoutCancellationToken))
#else
                    if (_tcs.TrySetCanceled())
#endif
                    {
                        // Cancellation was successful, does this mean we set a TimeoutException
                        // in the same manner as BlockingCell used to
                        _tcs.TrySetException(GetTimeoutException());
                    }
                }
                else
                {
                    throw;
                }
            }
        }

        public virtual void HandleChannelShutdown(ShutdownEventArgs reason)
        {
            _tcs.TrySetException(new OperationInterruptedException(reason));
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected abstract Task DoHandleCommandAsync(IncomingCommand cmd);

        protected void HandleUnexpectedCommand(IncomingCommand cmd)
        {
            _tcs.SetException(new InvalidOperationException($"Received unexpected command of type {cmd.CommandId}!"));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _continuationTimeoutCancellationTokenRegistration.Dispose();
                    _continuationTimeoutCancellationTokenSource.Dispose();
                    _linkedCancellationTokenSource.Dispose();
                }

                _disposedValue = true;
            }
        }

#if NET
        private void HandleContinuationTimeout(object? state, CancellationToken cancellationToken)
        {
            var tcs = (TaskCompletionSource<T>)state!;
            if (tcs.TrySetCanceled(cancellationToken))
            {
                tcs.TrySetException(GetTimeoutException());
            }
        }
#else
        private void HandleContinuationTimeout(object state)
        {
            var tcs = (TaskCompletionSource<T>)state;
            if (tcs.TrySetCanceled())
            {
                tcs.TrySetException(GetTimeoutException());
            }
        }
#endif

        private TimeoutException GetTimeoutException()
        {
            // TODO
            // Cancellation was successful, does this mean we set a TimeoutException
            // in the same manner as BlockingCell used to
            string msg = $"operation '{GetType().FullName}' timed out after {_continuationTimeout}";
            return new TimeoutException(msg);
        }
    }

    internal sealed class ConnectionSecureOrTuneAsyncRpcContinuation : AsyncRpcContinuation<ConnectionSecureOrTune>
    {
        public ConnectionSecureOrTuneAsyncRpcContinuation(TimeSpan continuationTimeout, CancellationToken cancellationToken)
            : base(continuationTimeout, cancellationToken)
        {
        }

        public override ProtocolCommandId[] HandledProtocolCommandIds
            => [ProtocolCommandId.ConnectionSecure, ProtocolCommandId.ConnectionTune];

        protected override Task DoHandleCommandAsync(IncomingCommand cmd)
        {
            if (cmd.CommandId == ProtocolCommandId.ConnectionSecure)
            {
                var secure = new ConnectionSecure(cmd.MethodSpan);
                _tcs.SetResult(new ConnectionSecureOrTune(secure._challenge, default));
            }
            else if (cmd.CommandId == ProtocolCommandId.ConnectionTune)
            {
                var tune = new ConnectionTune(cmd.MethodSpan);
                _tcs.SetResult(new ConnectionSecureOrTune(default, new ConnectionTuneDetails
                {
                    m_channelMax = tune._channelMax,
                    m_frameMax = tune._frameMax,
                    m_heartbeatInSeconds = tune._heartbeat
                }));
            }
            else
            {
                HandleUnexpectedCommand(cmd);
            }

            return Task.CompletedTask;
        }
    }

    internal class SimpleAsyncRpcContinuation : AsyncRpcContinuation<bool>
    {
        private readonly ProtocolCommandId _expectedCommandId;

        public SimpleAsyncRpcContinuation(ProtocolCommandId expectedCommandId, TimeSpan continuationTimeout,
            CancellationToken cancellationToken)
            : base(continuationTimeout, cancellationToken)
        {
            _expectedCommandId = expectedCommandId;
        }

        public override ProtocolCommandId[] HandledProtocolCommandIds
            => [_expectedCommandId];

        protected override Task DoHandleCommandAsync(IncomingCommand cmd)
        {
            if (cmd.CommandId == _expectedCommandId)
            {
                _tcs.SetResult(true);
            }
            else
            {
                HandleUnexpectedCommand(cmd);
            }

            return Task.CompletedTask;
        }
    }

    internal sealed class BasicCancelAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        private readonly string _consumerTag;
        private readonly IConsumerDispatcher _consumerDispatcher;

        public BasicCancelAsyncRpcContinuation(string consumerTag, IConsumerDispatcher consumerDispatcher,
            TimeSpan continuationTimeout, CancellationToken cancellationToken)
            : base(ProtocolCommandId.BasicCancelOk, continuationTimeout, cancellationToken)
        {
            _consumerTag = consumerTag;
            _consumerDispatcher = consumerDispatcher;
        }

        protected override async Task DoHandleCommandAsync(IncomingCommand cmd)
        {
            if (cmd.CommandId == ProtocolCommandId.BasicCancelOk)
            {
                var result = new BasicCancelOk(cmd.MethodSpan);
                if (_consumerTag == result._consumerTag)
                {
                    await _consumerDispatcher.HandleBasicCancelOkAsync(_consumerTag, CancellationToken)
                        .ConfigureAwait(false);
                    _tcs.SetResult(true);
                }
                else
                {
                    string msg = string.Format("Consumer tag '{0}' does not match expected consumer tag for basic.cancel operation {1}",
                        result._consumerTag, _consumerTag);
                    var ex = new InvalidOperationException(msg);
                    _tcs.SetException(ex);
                }
            }
            else
            {
                HandleUnexpectedCommand(cmd);
            }
        }
    }

    internal sealed class BasicConsumeAsyncRpcContinuation : AsyncRpcContinuation<string>
    {
        private readonly IAsyncBasicConsumer _consumer;
        private readonly IConsumerDispatcher _consumerDispatcher;

        public BasicConsumeAsyncRpcContinuation(IAsyncBasicConsumer consumer, IConsumerDispatcher consumerDispatcher,
            TimeSpan continuationTimeout, CancellationToken cancellationToken)
            : base(continuationTimeout, cancellationToken)
        {
            _consumer = consumer;
            _consumerDispatcher = consumerDispatcher;
        }

        public override ProtocolCommandId[] HandledProtocolCommandIds
            => [ProtocolCommandId.BasicConsumeOk];

        protected override async Task DoHandleCommandAsync(IncomingCommand cmd)
        {
            if (cmd.CommandId == ProtocolCommandId.BasicConsumeOk)
            {
                var method = new BasicConsumeOk(cmd.MethodSpan);

                await _consumerDispatcher.HandleBasicConsumeOkAsync(_consumer, method._consumerTag, CancellationToken)
                    .ConfigureAwait(false);

                _tcs.SetResult(method._consumerTag);
            }
            else
            {
                HandleUnexpectedCommand(cmd);
            }
        }
    }

    internal sealed class BasicGetAsyncRpcContinuation : AsyncRpcContinuation<BasicGetResult?>
    {
        private readonly Func<ulong, ulong> _adjustDeliveryTag;

        public BasicGetAsyncRpcContinuation(Func<ulong, ulong> adjustDeliveryTag,
            TimeSpan continuationTimeout, CancellationToken cancellationToken)
            : base(continuationTimeout, cancellationToken)
        {
            _adjustDeliveryTag = adjustDeliveryTag;
        }

        public override ProtocolCommandId[] HandledProtocolCommandIds
            => [ProtocolCommandId.BasicGetOk, ProtocolCommandId.BasicGetEmpty];

        internal DateTime StartTime { get; } = DateTime.UtcNow;

        protected override Task DoHandleCommandAsync(IncomingCommand cmd)
        {
            if (cmd.CommandId == ProtocolCommandId.BasicGetOk)
            {
                var method = new BasicGetOk(cmd.MethodSpan);
                var header = new ReadOnlyBasicProperties(cmd.HeaderSpan);

                var result = new BasicGetResult(
                    _adjustDeliveryTag(method._deliveryTag),
                    method._redelivered,
                    method._exchange,
                    method._routingKey,
                    method._messageCount,
                    header,
                    cmd.Body.ToArray());

                _tcs.SetResult(result);
            }
            else if (cmd.CommandId == ProtocolCommandId.BasicGetEmpty)
            {
                _tcs.SetResult(null);
            }
            else
            {
                HandleUnexpectedCommand(cmd);
            }

            return Task.CompletedTask;
        }
    }

    internal sealed class BasicQosAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public BasicQosAsyncRpcContinuation(TimeSpan continuationTimeout, CancellationToken cancellationToken)
            : base(ProtocolCommandId.BasicQosOk, continuationTimeout, cancellationToken)
        {
        }
    }

    internal sealed class ChannelOpenAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public ChannelOpenAsyncRpcContinuation(TimeSpan continuationTimeout, CancellationToken cancellationToken)
            : base(ProtocolCommandId.ChannelOpenOk, continuationTimeout, cancellationToken)
        {
        }
    }

    internal sealed class ChannelCloseAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public ChannelCloseAsyncRpcContinuation(TimeSpan continuationTimeout, CancellationToken cancellationToken)
            : base(ProtocolCommandId.ChannelCloseOk, continuationTimeout, cancellationToken)
        {
        }

        public override void HandleChannelShutdown(ShutdownEventArgs reason)
        {
            // Nothing to do here!
        }

        public Task OnConnectionShutdownAsync(object? sender, ShutdownEventArgs reason)
        {
            _tcs.SetResult(true);
            return Task.CompletedTask;
        }
    }

    internal sealed class ConfirmSelectAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public ConfirmSelectAsyncRpcContinuation(TimeSpan continuationTimeout, CancellationToken cancellationToken)
            : base(ProtocolCommandId.ConfirmSelectOk, continuationTimeout, cancellationToken)
        {
        }
    }

    internal sealed class ExchangeBindAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public ExchangeBindAsyncRpcContinuation(TimeSpan continuationTimeout, CancellationToken cancellationToken)
            : base(ProtocolCommandId.ExchangeBindOk, continuationTimeout, cancellationToken)
        {
        }
    }

    internal sealed class ExchangeDeclareAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public ExchangeDeclareAsyncRpcContinuation(TimeSpan continuationTimeout, CancellationToken cancellationToken)
            : base(ProtocolCommandId.ExchangeDeclareOk, continuationTimeout, cancellationToken)
        {
        }
    }

    internal sealed class ExchangeDeleteAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public ExchangeDeleteAsyncRpcContinuation(TimeSpan continuationTimeout, CancellationToken cancellationToken)
            : base(ProtocolCommandId.ExchangeDeleteOk, continuationTimeout, cancellationToken)
        {
        }
    }

    internal sealed class ExchangeUnbindAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public ExchangeUnbindAsyncRpcContinuation(TimeSpan continuationTimeout, CancellationToken cancellationToken)
            : base(ProtocolCommandId.ExchangeUnbindOk, continuationTimeout, cancellationToken)
        {
        }
    }

    internal sealed class QueueDeclareAsyncRpcContinuation : AsyncRpcContinuation<QueueDeclareOk>
    {
        public QueueDeclareAsyncRpcContinuation(TimeSpan continuationTimeout, CancellationToken cancellationToken)
            : base(continuationTimeout, cancellationToken)
        {
        }

        public override ProtocolCommandId[] HandledProtocolCommandIds
            => [ProtocolCommandId.QueueDeclareOk];

        protected override Task DoHandleCommandAsync(IncomingCommand cmd)
        {
            if (cmd.CommandId == ProtocolCommandId.QueueDeclareOk)
            {
                var method = new Client.Framing.QueueDeclareOk(cmd.MethodSpan);
                var result = new QueueDeclareOk(method._queue, method._messageCount, method._consumerCount);
                _tcs.SetResult(result);
            }
            else
            {
                HandleUnexpectedCommand(cmd);
            }

            return Task.CompletedTask;
        }
    }

    internal sealed class QueueBindAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public QueueBindAsyncRpcContinuation(TimeSpan continuationTimeout, CancellationToken cancellationToken)
            : base(ProtocolCommandId.QueueBindOk, continuationTimeout, cancellationToken)
        {
        }
    }

    internal sealed class QueueUnbindAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public QueueUnbindAsyncRpcContinuation(TimeSpan continuationTimeout, CancellationToken cancellationToken)
            : base(ProtocolCommandId.QueueUnbindOk, continuationTimeout, cancellationToken)
        {
        }
    }

    internal sealed class QueueDeleteAsyncRpcContinuation : AsyncRpcContinuation<uint>
    {
        public QueueDeleteAsyncRpcContinuation(TimeSpan continuationTimeout, CancellationToken cancellationToken)
            : base(continuationTimeout, cancellationToken)
        {
        }

        public override ProtocolCommandId[] HandledProtocolCommandIds
            => [ProtocolCommandId.QueueDeleteOk];

        protected override Task DoHandleCommandAsync(IncomingCommand cmd)
        {
            if (cmd.CommandId == ProtocolCommandId.QueueDeleteOk)
            {
                var method = new QueueDeleteOk(cmd.MethodSpan);
                _tcs.SetResult(method._messageCount);
            }
            else
            {
                HandleUnexpectedCommand(cmd);
            }

            return Task.CompletedTask;
        }
    }

    internal sealed class QueuePurgeAsyncRpcContinuation : AsyncRpcContinuation<uint>
    {
        public QueuePurgeAsyncRpcContinuation(TimeSpan continuationTimeout, CancellationToken cancellationToken)
            : base(continuationTimeout, cancellationToken)
        {
        }

        public override ProtocolCommandId[] HandledProtocolCommandIds
            => [ProtocolCommandId.QueuePurgeOk];

        protected override Task DoHandleCommandAsync(IncomingCommand cmd)
        {
            if (cmd.CommandId == ProtocolCommandId.QueuePurgeOk)
            {
                var method = new QueuePurgeOk(cmd.MethodSpan);
                _tcs.SetResult(method._messageCount);
            }
            else
            {
                HandleUnexpectedCommand(cmd);
            }

            return Task.CompletedTask;
        }
    }

    internal sealed class TxCommitAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public TxCommitAsyncRpcContinuation(TimeSpan continuationTimeout, CancellationToken cancellationToken)
            : base(ProtocolCommandId.TxCommitOk, continuationTimeout, cancellationToken)
        {
        }
    }

    internal sealed class TxRollbackAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public TxRollbackAsyncRpcContinuation(TimeSpan continuationTimeout, CancellationToken cancellationToken)
            : base(ProtocolCommandId.TxRollbackOk, continuationTimeout, cancellationToken)
        {
        }
    }

    internal sealed class TxSelectAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public TxSelectAsyncRpcContinuation(TimeSpan continuationTimeout, CancellationToken cancellationToken)
            : base(ProtocolCommandId.TxSelectOk, continuationTimeout, cancellationToken)
        {
        }
    }
}
