// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Impl
{
    ///<summary>Manages a queue of waiting AMQP RPC requests.</summary>
    ///<remarks>
    ///<para>
    /// Currently, pipelining of requests is forbidden by this
    /// implementation. The AMQP 0-8 and 0-9 specifications themselves
    /// forbid pipelining, but only by the skin of their teeth and
    /// under a somewhat generous reading.
    ///</para>
    ///</remarks>
    internal class RpcContinuationQueue
    {
        private class EmptyRpcContinuation : IRpcContinuation
        {
            public Task HandleCommandAsync(IncomingCommand _)
            {
                return Task.CompletedTask;
            }

            public void HandleChannelShutdown(ShutdownEventArgs reason)
            {
            }

            public void Dispose()
            {
            }
        }

        private const int CommandIdBufferLength = 2;

        // Two ProtocolCommandId values (each uint) laid out as a struct
        // reinterpreted as a single long for lock-free atomic
        // read-modify-write via Interlocked.Exchange.
        // Since ProtocolCommandId: uint has no zero-valued member,
        // default(LastTimedOutCommandIds) == 0 means "no timed-out command".
        private readonly struct LastTimedOutCommandIds(ProtocolCommandId first, ProtocolCommandId second = 0)
        {
            public readonly ProtocolCommandId First = first;
            public readonly ProtocolCommandId Second = second;
        }

        private static readonly EmptyRpcContinuation s_tmp = new EmptyRpcContinuation();
        private long _lastTimedOutCommandIds;
        private IRpcContinuation _outstandingRpc = s_tmp;

        ///<summary>Enqueue a continuation, marking a pending RPC.</summary>
        ///<remarks>
        ///<para>
        /// Continuations are retrieved in FIFO order by calling Next().
        ///</para>
        ///<para>
        /// In the current implementation, only one continuation can
        /// be queued up at once. Calls to Enqueue() when a
        /// continuation is already enqueued will result in
        /// NotSupportedException being thrown.
        ///</para>
        ///</remarks>
        public void Enqueue(IRpcContinuation k)
        {
            IRpcContinuation result = Interlocked.CompareExchange(ref _outstandingRpc, k, s_tmp);
            if (result is not EmptyRpcContinuation)
            {
                throw new NotSupportedException($"Pipelining of requests forbidden (attempted: {k.GetType()}, enqueued: {result.GetType()})");
            }
        }

        ///<summary>Interrupt all waiting continuations.</summary>
        ///<remarks>
        ///<para>
        /// There's just the one potential waiter in the current
        /// implementation.
        ///</para>
        ///</remarks>
        public void HandleChannelShutdown(ShutdownEventArgs reason)
        {
            using (IRpcContinuation c = Next())
            {
                c.HandleChannelShutdown(reason);
            }
        }

        ///<summary>Retrieve the next waiting continuation.</summary>
        ///<remarks>
        ///<para>
        /// It is an error to call this method when there are no
        /// waiting continuations. In the current implementation, if
        /// this happens, null will be returned (which will usually
        /// result in an immediate NullPointerException in the
        /// caller). Correct code will always arrange for a
        /// continuation to have been Enqueue()d before calling this
        /// method.
        ///</para>
        ///</remarks>
        public IRpcContinuation Next()
        {
            return Interlocked.Exchange(ref _outstandingRpc, s_tmp);
        }

        ///<summary>Peek at the next waiting continuation.</summary>
        ///<remarks>
        ///<para>
        /// It is an error to call this method when there are no
        /// waiting continuations.
        ///</para>
        ///</remarks>
        public bool TryPeek<T>([NotNullWhen(true)] out T? continuation) where T : class, IRpcContinuation
        {
            if (_outstandingRpc is T result)
            {
                continuation = result;
                return true;
            }

            continuation = default;
            return false;
        }

        public void RpcCanceled(bool responseReceived, ReadOnlySpan<ProtocolCommandId> protocolCommandIds)
        {
            if (responseReceived)
            {
                return;
            }

            // AMQP 0-9-1 RPCs handle at most 2 response command IDs
            // (e.g. BasicGetOk/BasicGetEmpty, ConnectionSecure/ConnectionTune)
            Debug.Assert(protocolCommandIds.Length is > 0 and <= CommandIdBufferLength);

            var ids = new LastTimedOutCommandIds(first: protocolCommandIds[0], second: protocolCommandIds.Length > 1 ? protocolCommandIds[1] : 0);

            Interlocked.Exchange(ref _lastTimedOutCommandIds, Unsafe.As<LastTimedOutCommandIds, long>(ref ids));
        }

        public bool ShouldIgnoreCommand(ProtocolCommandId commandId)
        {
            // rabbitmq/rabbitmq-dotnet-client#1802
            // This keeps track of ProtocolCommandId values from previous RPC
            // commands that have timed out.
            long raw = Interlocked.Exchange(ref _lastTimedOutCommandIds, 0L);

            if (raw == 0L)
            {
                return false;
            }

            var ids = Unsafe.As<long, LastTimedOutCommandIds>(ref raw);
            return commandId == ids.First || (ids.Second != 0 && commandId == ids.Second);
        }
    }
}
