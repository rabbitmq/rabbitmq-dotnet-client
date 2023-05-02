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
using System.Threading.Tasks;
using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    internal abstract class AsyncRpcContinuation<T> : IRpcContinuation
    {
        protected readonly TaskCompletionSource<T> _tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskAwaiter<T> GetAwaiter() => _tcs.Task.GetAwaiter();

        public abstract void HandleCommand(in IncomingCommand cmd);

        public void HandleChannelShutdown(ShutdownEventArgs reason) => _tcs.SetException(new OperationInterruptedException(reason));
    }

    internal class ConnectionSecureOrTuneContinuation : AsyncRpcContinuation<ConnectionSecureOrTune>
    {
        public override void HandleCommand(in IncomingCommand cmd)
        {
            if (cmd.CommandId == ProtocolCommandId.ConnectionSecure)
            {
                var secure = new ConnectionSecure(cmd.MethodBytes.Span);
                _tcs.TrySetResult(new ConnectionSecureOrTune { m_challenge = secure._challenge });
                cmd.ReturnMethodBuffer();
            }
            else if (cmd.CommandId == ProtocolCommandId.ConnectionTune)
            {
                var tune = new ConnectionTune(cmd.MethodBytes.Span);
                _tcs.TrySetResult(new ConnectionSecureOrTune
                {
                    m_tuneDetails = new() { m_channelMax = tune._channelMax, m_frameMax = tune._frameMax, m_heartbeatInSeconds = tune._heartbeat }
                });
                cmd.ReturnMethodBuffer();
            }
            else
            {
                _tcs.SetException(new InvalidOperationException($"Received unexpected command of type {cmd.CommandId}!"));
            }
        }
    }

    internal class SimpleBlockingRpcContinuation : IRpcContinuation
    {
        private readonly BlockingCell<Either<IncomingCommand, ShutdownEventArgs>> m_cell = new BlockingCell<Either<IncomingCommand, ShutdownEventArgs>>();

        public void GetReply(TimeSpan timeout)
        {
            Either<IncomingCommand, ShutdownEventArgs> result = m_cell.WaitForValue(timeout);
            if (result.Alternative == EitherAlternative.Left)
            {
                return;
            }
            ThrowOperationInterruptedException(result.RightValue);
        }

        public void GetReply(TimeSpan timeout, out IncomingCommand reply)
        {
            Either<IncomingCommand, ShutdownEventArgs> result = m_cell.WaitForValue(timeout);
            if (result.Alternative == EitherAlternative.Left)
            {
                reply = result.LeftValue;
                return;
            }

            reply = IncomingCommand.Empty;
            ThrowOperationInterruptedException(result.RightValue);
        }

        private static void ThrowOperationInterruptedException(ShutdownEventArgs shutdownEventArgs)
            => throw new OperationInterruptedException(shutdownEventArgs);

        public void HandleCommand(in IncomingCommand cmd)
        {
            m_cell.ContinueWithValue(Either<IncomingCommand, ShutdownEventArgs>.Left(cmd));
        }

        public void HandleChannelShutdown(ShutdownEventArgs reason)
        {
            m_cell.ContinueWithValue(Either<IncomingCommand, ShutdownEventArgs>.Right(reason));
        }
    }
}
