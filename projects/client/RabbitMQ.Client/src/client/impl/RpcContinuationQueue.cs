// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2016 Pivotal Software, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at http://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Threading;

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
    public class RpcContinuationQueue
    {
        private class EmptyRpcContinuation : IRpcContinuation
        {
            public void HandleCommand(Command cmd)
            {
            }

            public void HandleModelShutdown(ShutdownEventArgs reason)
            {
            }
        }
        static readonly EmptyRpcContinuation tmp = new EmptyRpcContinuation();
        IRpcContinuation m_outstandingRpc = tmp;

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
            var result = Interlocked.CompareExchange(ref this.m_outstandingRpc, k, tmp);
            if (!(result is EmptyRpcContinuation))
            {
                throw new NotSupportedException("Pipelining of requests forbidden");
            }
        }

        ///<summary>Interrupt all waiting continuations.</summary>
        ///<remarks>
        ///<para>
        /// There's just the one potential waiter in the current
        /// implementation.
        ///</para>
        ///</remarks>
        public void HandleModelShutdown(ShutdownEventArgs reason)
        {
            Next().HandleModelShutdown(reason);
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
            return Interlocked.Exchange(ref m_outstandingRpc, tmp);
        }
    }
}
