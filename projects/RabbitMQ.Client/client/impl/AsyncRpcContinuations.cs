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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    internal abstract class AsyncRpcContinuation<T> : IRpcContinuation, IDisposable
    {
        private readonly CancellationTokenSource _ct;

        protected readonly TaskCompletionSource<T> _tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        private bool _disposedValue;

        public AsyncRpcContinuation(TimeSpan continuationTimeout)
        {
            _ct = new CancellationTokenSource(continuationTimeout);
            _ct.Token.Register(() =>
            {
                if (_tcs.TrySetCanceled())
                {
                    // TODO LRB rabbitmq/rabbitmq-dotnet-client#1347
                    // Cancellation was successful, does this mean we should set a TimeoutException
                    // in the same manner as BlockingCell?
                }
            }, useSynchronizationContext: false);
        }

        public TaskAwaiter<T> GetAwaiter() => _tcs.Task.GetAwaiter();

        // TODO LRB #1347
        // What to do if setting a result fails?
        public abstract void HandleCommand(in IncomingCommand cmd);

        public void HandleChannelShutdown(ShutdownEventArgs reason) => _tcs.SetException(new OperationInterruptedException(reason));

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _ct.Dispose();
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

    internal class ConnectionSecureOrTuneContinuation : AsyncRpcContinuation<ConnectionSecureOrTune>
    {
        public ConnectionSecureOrTuneContinuation(TimeSpan continuationTimeout) : base(continuationTimeout)
        {
        }

        public override void HandleCommand(in IncomingCommand cmd)
        {
            try
            {
                if (cmd.CommandId == ProtocolCommandId.ConnectionSecure)
                {
                    var secure = new ConnectionSecure(cmd.MethodBytes.Span);
                    _tcs.TrySetResult(new ConnectionSecureOrTune { m_challenge = secure._challenge });
                }
                else if (cmd.CommandId == ProtocolCommandId.ConnectionTune)
                {
                    var tune = new ConnectionTune(cmd.MethodBytes.Span);
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
                cmd.ReturnMethodBuffer();
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
                cmd.ReturnMethodBuffer();
            }
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
                var method = new Client.Framing.Impl.QueueDeclareOk(cmd.MethodBytes.Span);
                var result = new QueueDeclareOk(method._queue, method._messageCount, method._consumerCount);
                if (cmd.CommandId == ProtocolCommandId.QueueDeclareOk)
                {
                    _tcs.TrySetResult(result);
                }
                else
                {
                    _tcs.SetException(new InvalidOperationException($"Received unexpected command of type {cmd.CommandId}!"));
                }
            }
            finally
            {
                cmd.ReturnMethodBuffer();
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

    internal class QueueDeleteAsyncRpcContinuation : AsyncRpcContinuation<uint>
    {
        public QueueDeleteAsyncRpcContinuation(TimeSpan continuationTimeout) : base(continuationTimeout)
        {
        }

        public override void HandleCommand(in IncomingCommand cmd)
        {
            try
            {
                var method = new Client.Framing.Impl.QueueDeleteOk(cmd.MethodBytes.Span);
                if (cmd.CommandId == ProtocolCommandId.QueueDeleteOk)
                {
                    _tcs.TrySetResult(method._messageCount);
                }
                else
                {
                    _tcs.SetException(new InvalidOperationException($"Received unexpected command of type {cmd.CommandId}!"));
                }
            }
            finally
            {
                cmd.ReturnMethodBuffer();
            }
        }
    }
}
