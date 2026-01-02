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

using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    sealed class AsyncManualResetEvent
    {
        volatile TaskCompletionSource<bool> _taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public AsyncManualResetEvent(bool initialState = false)
        {
            if (initialState)
            {
                _taskCompletionSource.SetResult(true);
            }
        }

        public bool IsSet => _taskCompletionSource.Task.IsCompleted;

        public Task WaitAsync(CancellationToken cancellationToken = default)
        {
            Task<bool> task = _taskCompletionSource.Task;
            return task.IsCompleted ? task : task.WaitAsync(cancellationToken);
        }

        public void Set() => _taskCompletionSource.TrySetResult(true);

        public void Reset()
        {
            while (true)
            {
                TaskCompletionSource<bool> currentTcs = _taskCompletionSource;
                if (!currentTcs.Task.IsCompleted ||
                    Interlocked.CompareExchange(ref _taskCompletionSource, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously), currentTcs) == currentTcs)
                {
                    return;
                }
            }
        }
    }
}
