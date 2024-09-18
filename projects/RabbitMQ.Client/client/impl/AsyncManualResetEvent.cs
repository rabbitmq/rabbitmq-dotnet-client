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

using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client.client.impl
{
    /// <summary>
    /// Inspired by http://blogs.msdn.com/b/pfxteam/archive/2012/02/11/10266920.aspx
    /// </summary>
    sealed class AsyncManualResetEvent
    {
        public AsyncManualResetEvent(bool initialState)
        {
            if (initialState)
            {
                _taskCompletionSource.SetResult(true);
            }
        }

        public bool IsSet => _taskCompletionSource.Task.IsCompleted;

        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            CancellationTokenRegistration tokenRegistration =
#if NET6_0_OR_GREATER
                cancellationToken.UnsafeRegister(
                    state => ((TaskCompletionSource<bool>)state!).TrySetCanceled(), _taskCompletionSource);
#else
                cancellationToken.Register(
                    state => ((TaskCompletionSource<bool>)state!).TrySetCanceled(),
                    state: _taskCompletionSource, useSynchronizationContext: false);
#endif
            try
            {
                await _taskCompletionSource.Task.ConfigureAwait(false);
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

        public void Set()
        {
            _taskCompletionSource.TrySetResult(true);
        }

        public void Reset()
        {
            var sw = new SpinWait();

            do
            {
                var currentTaskCompletionSource = _taskCompletionSource;
                if (!currentTaskCompletionSource.Task.IsCompleted)
                {
                    return;
                }

                var nextTaskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (Interlocked.CompareExchange(ref _taskCompletionSource, nextTaskCompletionSource, currentTaskCompletionSource) == currentTaskCompletionSource)
                {
                    return;
                }

                sw.SpinOnce();
            }
            while (true);
        }

        volatile TaskCompletionSource<bool> _taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
