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
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client
{
    internal static class TaskExtensions
    {
#if !NET6_0_OR_GREATER
        private static readonly TaskContinuationOptions s_tco = TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously;
        private static void continuation(Task t, object s) => t.Exception.Handle(e => true);
#endif

        public static Task TimeoutAfter(this Task task, TimeSpan timeout)
        {
#if NET6_0_OR_GREATER
            if (task.IsCompletedSuccessfully)
            {
                return task;
            }

            return task.WaitAsync(timeout);
#else
            if (task.Status == TaskStatus.RanToCompletion)
            {
                return task;
            }

            return DoTimeoutAfter(task, timeout);

            static async Task DoTimeoutAfter(Task task, TimeSpan timeout)
            {
                if (task == await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false))
                {
                    await task.ConfigureAwait(false);
                }
                else
                {
                    Task supressErrorTask = task.ContinueWith(
                        continuationAction: continuation,
                        state: null,
                        cancellationToken: CancellationToken.None,
                        continuationOptions: s_tco,
                        scheduler: TaskScheduler.Default);
                    throw new TimeoutException();
                }
            }
#endif
        }

        public static async ValueTask TimeoutAfter(this ValueTask task, TimeSpan timeout)
        {
            if (task.IsCompletedSuccessfully)
            {
                return;
            }

#if NET6_0_OR_GREATER
            Task actualTask = task.AsTask();
            await actualTask.WaitAsync(timeout)
                .ConfigureAwait(false);
#else
            await DoTimeoutAfter(task, timeout)
                .ConfigureAwait(false);

            async static ValueTask DoTimeoutAfter(ValueTask task, TimeSpan timeout)
            {
                Task actualTask = task.AsTask();
                if (actualTask == await Task.WhenAny(actualTask, Task.Delay(timeout)).ConfigureAwait(false))
                {
                    await actualTask.ConfigureAwait(false);
                }
                else
                {
                    Task supressErrorTask = actualTask.ContinueWith(
                        continuationAction: continuation,
                        state: null,
                        cancellationToken: CancellationToken.None,
                        continuationOptions: s_tco,
                        scheduler: TaskScheduler.Default);
                    throw new TimeoutException();
                }
            }
#endif
        }
    }
}
