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

namespace Test
{
    internal static class TaskExtensions
    {
#if NET6_0_OR_GREATER
        public static Task WaitAsync(this Task task, TimeSpan timeout)
        {
            if (task.IsCompletedSuccessfully)
            {
                return task;
            }

            return task.WaitAsync(timeout);
        }
#else
        private static readonly TaskContinuationOptions s_tco = TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously;
        private static void IgnoreTaskContinuation(Task t, object s) => t.Exception.Handle(e => true);

        public static Task WaitAsync(this Task task, TimeSpan timeout)
        {
            if (task.Status == TaskStatus.RanToCompletion)
            {
                return task;
            }

            return DoTimeoutAfter(task, timeout);
        }

        // https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md#using-a-timeout
        private static async Task DoTimeoutAfter(Task task, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource())
            {
                Task delayTask = Task.Delay(timeout, cts.Token);
                Task resultTask = await Task.WhenAny(task, delayTask).ConfigureAwait(false);
                if (resultTask == delayTask)
                {
                    task.Ignore();
                    throw new TimeoutException();
                }
                else
                {
                    cts.Cancel();
                }

                await task.ConfigureAwait(false);
            }
        }

        // https://github.com/dotnet/runtime/issues/23878
        // https://github.com/dotnet/runtime/issues/23878#issuecomment-1398958645
        private static void Ignore(this Task task)
        {
            if (task.IsCompleted)
            {
                _ = task.Exception;
            }
            else
            {
                _ = task.ContinueWith(
                    continuationAction: IgnoreTaskContinuation,
                    state: null,
                    cancellationToken: CancellationToken.None,
                    continuationOptions: s_tco,
                    scheduler: TaskScheduler.Default);
            }
        }
#endif
    }
}
