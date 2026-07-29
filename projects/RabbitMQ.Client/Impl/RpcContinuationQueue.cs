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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

        private static readonly EmptyRpcContinuation s_tmp = new EmptyRpcContinuation();

        // rabbitmq/rabbitmq-dotnet-client#1964
        // Every timed-out RPC leaves a late response in flight, and several can be
        // outstanding at once: the channel's RPC semaphore serializes requests, but
        // nothing makes the broker's replies arrive before the next request is sent.
        // A single-slot record therefore loses responses, and the survivors get
        // matched against an unrelated continuation ("Received unexpected command
        // of type ...!"). Queue one entry per timed-out RPC instead.
        private readonly ConcurrentQueue<TimedOutRpc> _timedOutRpcs = new ConcurrentQueue<TimedOutRpc>();

        private IRpcContinuation _outstandingRpc = s_tmp;

        private readonly struct TimedOutRpc
        {
            public TimedOutRpc(ProtocolCommandId first, ProtocolCommandId second)
            {
                First = first;
                Second = second;
            }

            public ProtocolCommandId First { get; }
            public ProtocolCommandId Second { get; }

            public bool Matches(ProtocolCommandId commandId)
            {
                return commandId == First || (Second != default && commandId == Second);
            }
        }

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
            // No further frames will arrive on this channel, so any late responses
            // still being waited for will never show up. Drop them so that a recovered
            // channel does not start out expecting to discard commands.
            // Note: ConcurrentQueue<T>.Clear() is not available on netstandard2.0.
            while (_timedOutRpcs.TryDequeue(out _))
            {
            }

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

            var timedOut = new TimedOutRpc(
                protocolCommandIds[0],
                protocolCommandIds.Length > 1 ? protocolCommandIds[1] : default);

            _timedOutRpcs.Enqueue(timedOut);
        }

        public bool ShouldIgnoreCommand(ProtocolCommandId commandId)
        {
            // rabbitmq/rabbitmq-dotnet-client#1802
            // This keeps track of ProtocolCommandId values from previous RPC
            // commands that have timed out, so that their late responses are
            // discarded rather than matched against a later continuation.
            // AMQP 0-9-1 enforces strict request-response ordering on a channel, so late
            // responses arrive in the order the requests were sent. Only the oldest
            // outstanding entry can match; consume it either way, since a non-match means
            // that response is never coming.
            if (_timedOutRpcs.TryDequeue(out TimedOutRpc timedOut))
            {
                return timedOut.Matches(commandId);
            }

            return false;
        }
    }
}
