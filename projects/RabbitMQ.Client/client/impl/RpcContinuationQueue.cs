// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at https://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

using RabbitMQ.Client.Exceptions;

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
    internal class RpcContinuationQueue : IValueTaskSource<Command>, IValueTaskSource
    {
        private readonly ISession _session;
        private readonly SemaphoreSlim _continuationLock = new SemaphoreSlim(1, 1);
        public RpcContinuationQueue(ISession session) => _session = session;

        private ManualResetValueTaskSourceCore<Command> _vts = new ManualResetValueTaskSourceCore<Command>() { RunContinuationsAsynchronously = true };

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
        public async ValueTask<T> SendAndReceiveAsync<T>(Command command, TimeSpan timeout = default) where T : MethodBase
        {
            Command response = await SendAndReceiveAsync(command, timeout).ConfigureAwait(false);
            return (response.Method as T) ?? throw new UnexpectedMethodException(response.Method);
        }

        public async ValueTask<T> SendAndReceiveAsync<T>(MethodBase method, TimeSpan timeout = default) where T : MethodBase
        {
            Command response = await SendAndReceiveAsync(new Command(method), timeout).ConfigureAwait(false);
            return (response.Method as T) ?? throw new UnexpectedMethodException(response.Method);
        }

        public ValueTask<Command> SendAndReceiveAsync(Command command, TimeSpan timeout = default) => _continuationLock.Wait(0) ? GetAwaiter(command, timeout) : SendAndReceiveAsyncSlow(command, timeout);

        private async ValueTask<Command> SendAndReceiveAsyncSlow(Command command, TimeSpan timeout = default)
        {
            if (timeout != default)
            {
                var timer = Stopwatch.StartNew();
                if (await _continuationLock.WaitAsync(timeout).ConfigureAwait(false))
                {
                    return await GetAwaiter(command, timeout - timer.Elapsed).ConfigureAwait(false);
                }

                throw new TimeoutException();
            }

            await _continuationLock.WaitAsync().ConfigureAwait(false);
            return await GetAwaiter(command, timeout).ConfigureAwait(false);
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
            if (_continuationLock.CurrentCount == 0)
            {
                // Only set an exception if we have an outstanding task.
                _vts.SetException(new OperationInterruptedException(reason));
            }
        }

        public void HandleCommand(Command responseCommand) => _vts.SetResult(responseCommand);

        private ValueTask<Command> GetAwaiter(Command command, TimeSpan timeout = default)
        {
            _vts.Reset();
            if (timeout != default)
            {
                return GetAwaiterWithTimeout(command, timeout);
            }

            ValueTask transmitTask = (command is object) ? _session.Transmit(command) : default;
            if (transmitTask.IsCompletedSuccessfully)
            {
                return new ValueTask<Command>(this, _vts.Version);
            }

            return AwaitTransmitAndCreate(transmitTask);
        }

        private async ValueTask<Command> AwaitTransmitAndCreate(ValueTask transmitTask)
        {
            await transmitTask.ConfigureAwait(false);
            return await new ValueTask<Command>(this, _vts.Version);
        }

        private async ValueTask<Command> GetAwaiterWithTimeout(Command command, TimeSpan timeout)
        {
            ValueTask transmitTask = (command is object) ? _session.Transmit(command) : default;
            if (!transmitTask.IsCompletedSuccessfully)
            {
                await transmitTask.ConfigureAwait(false);
            }

            using (CancellationTokenSource cts = new CancellationTokenSource(timeout))
            using (cts.Token.Register(() => _vts.SetException(new TaskCanceledException())))
            {
                try
                {
                    return await new ValueTask<Command>(this, _vts.Version).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    throw new TimeoutException();
                }
            }
        }

        public Command GetResult(short token)
        {
            try
            {
                return _vts.GetResult(token);
            }
            finally
            {
                _continuationLock.Release();
            }
        }

        public ValueTaskSourceStatus GetStatus(short token) => _vts.GetStatus(token);

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags) => _vts.OnCompleted(continuation, state, token, flags);

        void IValueTaskSource.GetResult(short token) => _vts.GetResult(token);
    }
}
