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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.ConsumerDispatching;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    internal abstract class AsyncRpcContinuation<T> : IRpcContinuation, IDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ConfiguredTaskAwaitable<T> _tcsConfiguredTaskAwaitable;

        protected readonly TaskCompletionSource<T> _tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        private bool _disposedValue;

        public AsyncRpcContinuation(TimeSpan continuationTimeout)
        {
            /*
             * Note: we can't use an ObjectPool for these because the netstandard2.0
             * version of CancellationTokenSource can't be reset prior to checking
             * in to the ObjectPool
             */
            _cancellationTokenSource = new CancellationTokenSource(continuationTimeout);

            _cancellationTokenSource.Token.Register(() =>
            {
                if (_tcs.TrySetCanceled())
                {
                    // TODO LRB rabbitmq/rabbitmq-dotnet-client#1347
                    // Cancellation was successful, does this mean we should set a TimeoutException
                    // in the same manner as BlockingCell?
                }
            }, useSynchronizationContext: false);

            _tcsConfiguredTaskAwaitable = _tcs.Task.ConfigureAwait(false);
        }

        public ConfiguredTaskAwaitable<T>.ConfiguredTaskAwaiter GetAwaiter()
        {
            return _tcsConfiguredTaskAwaitable.GetAwaiter();
        }

        public abstract void HandleCommand(in IncomingCommand cmd);

        public virtual void HandleChannelShutdown(ShutdownEventArgs reason)
        {
            _tcs.SetException(new OperationInterruptedException(reason));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _cancellationTokenSource.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    internal class ConnectionSecureOrTuneAsyncRpcContinuation : AsyncRpcContinuation<ConnectionSecureOrTune>
    {
        public ConnectionSecureOrTuneAsyncRpcContinuation(TimeSpan continuationTimeout) : base(continuationTimeout)
        {
        }

        public override void HandleCommand(in IncomingCommand cmd)
        {
            try
            {
                if (cmd.CommandId == ProtocolCommandId.ConnectionSecure)
                {
                    var secure = new ConnectionSecure(cmd.MethodSpan);
                    _tcs.TrySetResult(new ConnectionSecureOrTune { m_challenge = secure._challenge });
                }
                else if (cmd.CommandId == ProtocolCommandId.ConnectionTune)
                {
                    var tune = new ConnectionTune(cmd.MethodSpan);
                    // TODO LRB rabbitmq/rabbitmq-dotnet-client#1347
                    // What to do if setting a result fails?
                    _tcs.TrySetResult(new ConnectionSecureOrTune
                    {
                        m_tuneDetails = new() { m_channelMax = tune._channelMax, m_frameMax = tune._frameMax, m_heartbeatInSeconds = tune._heartbeat }
                    });
                }
                else
                {
                    _tcs.SetException(new InvalidOperationException($"Received unexpected command of type {cmd.CommandId}!"));
                }
            }
            finally
            {
                cmd.ReturnBuffers();
            }
        }
    }

    internal class SimpleAsyncRpcContinuation : AsyncRpcContinuation<bool>
    {
        private readonly ProtocolCommandId _expectedCommandId;

        public SimpleAsyncRpcContinuation(ProtocolCommandId expectedCommandId, TimeSpan continuationTimeout) : base(continuationTimeout)
        {
            _expectedCommandId = expectedCommandId;
        }

        public override void HandleCommand(in IncomingCommand cmd)
        {
            try
            {
                if (cmd.CommandId == _expectedCommandId)
                {
                    _tcs.TrySetResult(true);
                }
                else
                {
                    _tcs.SetException(new InvalidOperationException($"Received unexpected command of type {cmd.CommandId}!"));
                }
            }
            finally
            {
                cmd.ReturnBuffers();
            }
        }
    }

    internal class BasicCancelAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        private readonly string _consumerTag;
        private readonly IConsumerDispatcher _consumerDispatcher;

        public BasicCancelAsyncRpcContinuation(string consumerTag, IConsumerDispatcher consumerDispatcher, TimeSpan continuationTimeout)
            : base(ProtocolCommandId.BasicCancelOk, continuationTimeout)
        {
            _consumerTag = consumerTag;
            _consumerDispatcher = consumerDispatcher;
        }

        public override void HandleCommand(in IncomingCommand cmd)
        {
            try
            {
                if (cmd.CommandId == ProtocolCommandId.BasicCancelOk)
                {
                    var method = new Client.Framing.Impl.BasicCancelOk(cmd.MethodSpan);
                    _tcs.TrySetResult(true);
                    Debug.Assert(_consumerTag == method._consumerTag);
                    _consumerDispatcher.HandleBasicCancelOk(_consumerTag);
                }
                else
                {
                    _tcs.SetException(new InvalidOperationException($"Received unexpected command of type {cmd.CommandId}!"));
                }
            }
            finally
            {
                cmd.ReturnBuffers();
            }
        }
    }

    internal class BasicConsumeAsyncRpcContinuation : AsyncRpcContinuation<string>
    {
        private readonly IBasicConsumer _consumer;
        private readonly IConsumerDispatcher _consumerDispatcher;

        public BasicConsumeAsyncRpcContinuation(IBasicConsumer consumer, IConsumerDispatcher consumerDispatcher, TimeSpan continuationTimeout)
            : base(continuationTimeout)
        {
            _consumer = consumer;
            _consumerDispatcher = consumerDispatcher;
        }

        public override void HandleCommand(in IncomingCommand cmd)
        {
            try
            {
                if (cmd.CommandId == ProtocolCommandId.BasicConsumeOk)
                {
                    var method = new Client.Framing.Impl.BasicConsumeOk(cmd.MethodSpan);
                    _tcs.TrySetResult(method._consumerTag);
                    _consumerDispatcher.HandleBasicConsumeOk(_consumer, method._consumerTag);
                }
                else
                {
                    _tcs.SetException(new InvalidOperationException($"Received unexpected command of type {cmd.CommandId}!"));
                }
            }
            finally
            {
                cmd.ReturnBuffers();
            }
        }
    }

    internal class BasicGetAsyncRpcContinuation : AsyncRpcContinuation<BasicGetResult>
    {
        private readonly Func<ulong, ulong> _adjustDeliveryTag;

        public BasicGetAsyncRpcContinuation(Func<ulong, ulong> adjustDeliveryTag, TimeSpan continuationTimeout)
            : base(continuationTimeout)
        {
            _adjustDeliveryTag = adjustDeliveryTag;
        }

        public override void HandleCommand(in IncomingCommand cmd)
        {
            try
            {
                if (cmd.CommandId == ProtocolCommandId.BasicGetOk)
                {
                    var method = new Client.Framing.Impl.BasicGetOk(cmd.MethodSpan);
                    var header = new ReadOnlyBasicProperties(cmd.HeaderSpan);

                    var result = new BasicGetResult(
                        _adjustDeliveryTag(method._deliveryTag),
                        method._redelivered,
                        method._exchange,
                        method._routingKey,
                        method._messageCount,
                        header,
                        cmd.Body.ToArray());

                    _tcs.TrySetResult(result);
                }
                else if (cmd.CommandId == ProtocolCommandId.BasicGetEmpty)
                {
                    _tcs.TrySetResult(null);
                }
                else
                {
                    _tcs.SetException(new InvalidOperationException($"Received unexpected command of type {cmd.CommandId}!"));
                }
            }
            finally
            {
                // Note: since we copy the body buffer above, we want to return all buffers here
                cmd.ReturnBuffers();
            }
        }
    }

    internal class BasicQosAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public BasicQosAsyncRpcContinuation(TimeSpan continuationTimeout)
            : base(ProtocolCommandId.BasicQosOk, continuationTimeout)
        {
        }
    }

    internal class ChannelOpenAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public ChannelOpenAsyncRpcContinuation(TimeSpan continuationTimeout)
            : base(ProtocolCommandId.ChannelOpenOk, continuationTimeout)
        {
        }
    }

    internal class ChannelCloseAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public ChannelCloseAsyncRpcContinuation(TimeSpan continuationTimeout)
            : base(ProtocolCommandId.ChannelCloseOk, continuationTimeout)
        {
        }

        public override void HandleChannelShutdown(ShutdownEventArgs reason)
        {
            // Nothing to do here!
        }

        public void OnConnectionShutdown(object sender, ShutdownEventArgs reason)
        {
            _tcs.TrySetResult(true);
        }
    }

    internal class ConfirmSelectAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public ConfirmSelectAsyncRpcContinuation(TimeSpan continuationTimeout)
            : base(ProtocolCommandId.ConfirmSelectOk, continuationTimeout)
        {
        }
    }

    internal class ExchangeBindAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public ExchangeBindAsyncRpcContinuation(TimeSpan continuationTimeout)
            : base(ProtocolCommandId.ExchangeBindOk, continuationTimeout)
        {
        }
    }

    internal class ExchangeDeclareAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public ExchangeDeclareAsyncRpcContinuation(TimeSpan continuationTimeout)
            : base(ProtocolCommandId.ExchangeDeclareOk, continuationTimeout)
        {
        }
    }

    internal class ExchangeDeleteAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public ExchangeDeleteAsyncRpcContinuation(TimeSpan continuationTimeout)
            : base(ProtocolCommandId.ExchangeDeleteOk, continuationTimeout)
        {
        }
    }

    internal class ExchangeUnbindAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public ExchangeUnbindAsyncRpcContinuation(TimeSpan continuationTimeout)
            : base(ProtocolCommandId.ExchangeUnbindOk, continuationTimeout)
        {
        }
    }

    internal class QueueDeclareAsyncRpcContinuation : AsyncRpcContinuation<QueueDeclareOk>
    {
        public QueueDeclareAsyncRpcContinuation(TimeSpan continuationTimeout) : base(continuationTimeout)
        {
        }

        public override void HandleCommand(in IncomingCommand cmd)
        {
            try
            {
                if (cmd.CommandId == ProtocolCommandId.QueueDeclareOk)
                {
                    var method = new Client.Framing.Impl.QueueDeclareOk(cmd.MethodSpan);
                    var result = new QueueDeclareOk(method._queue, method._messageCount, method._consumerCount);
                    _tcs.TrySetResult(result);
                }
                else
                {
                    _tcs.SetException(new InvalidOperationException($"Received unexpected command of type {cmd.CommandId}!"));
                }
            }
            finally
            {
                cmd.ReturnBuffers();
            }
        }
    }

    internal class QueueBindAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public QueueBindAsyncRpcContinuation(TimeSpan continuationTimeout)
            : base(ProtocolCommandId.QueueBindOk, continuationTimeout)
        {
        }
    }

    internal class QueueUnbindAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public QueueUnbindAsyncRpcContinuation(TimeSpan continuationTimeout)
            : base(ProtocolCommandId.QueueUnbindOk, continuationTimeout)
        {
        }
    }

    internal class QueueDeleteAsyncRpcContinuation : AsyncRpcContinuation<uint>
    {
        public QueueDeleteAsyncRpcContinuation(TimeSpan continuationTimeout) : base(continuationTimeout)
        {
        }

        public override void HandleCommand(in IncomingCommand cmd)
        {
            try
            {
                if (cmd.CommandId == ProtocolCommandId.QueueDeleteOk)
                {
                    var method = new Client.Framing.Impl.QueueDeleteOk(cmd.MethodSpan);
                    _tcs.TrySetResult(method._messageCount);
                }
                else
                {
                    _tcs.SetException(new InvalidOperationException($"Received unexpected command of type {cmd.CommandId}!"));
                }
            }
            finally
            {
                cmd.ReturnBuffers();
            }
        }
    }

    internal class QueuePurgeAsyncRpcContinuation : AsyncRpcContinuation<uint>
    {
        public QueuePurgeAsyncRpcContinuation(TimeSpan continuationTimeout) : base(continuationTimeout)
        {
        }

        public override void HandleCommand(in IncomingCommand cmd)
        {
            try
            {
                if (cmd.CommandId == ProtocolCommandId.QueuePurgeOk)
                {
                    var method = new Client.Framing.Impl.QueuePurgeOk(cmd.MethodSpan);
                    _tcs.TrySetResult(method._messageCount);
                }
                else
                {
                    _tcs.SetException(new InvalidOperationException($"Received unexpected command of type {cmd.CommandId}!"));
                }
            }
            finally
            {
                cmd.ReturnBuffers();
            }
        }
    }

    internal class TxCommitAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public TxCommitAsyncRpcContinuation(TimeSpan continuationTimeout)
            : base(ProtocolCommandId.TxCommitOk, continuationTimeout)
        {
        }
    }

    internal class TxRollbackAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public TxRollbackAsyncRpcContinuation(TimeSpan continuationTimeout)
            : base(ProtocolCommandId.TxRollbackOk, continuationTimeout)
        {
        }
    }

    internal class TxSelectAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public TxSelectAsyncRpcContinuation(TimeSpan continuationTimeout)
            : base(ProtocolCommandId.TxSelectOk, continuationTimeout)
        {
        }
    }
}
