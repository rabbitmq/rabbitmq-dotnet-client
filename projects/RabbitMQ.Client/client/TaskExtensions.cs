﻿// This source code is dual-licensed under the Apache License, version
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
#if NET6_0_OR_GREATER
        public static bool IsCompletedSuccessfully(this Task task)
        {
            return task.IsCompletedSuccessfully;
        }
#else
        public static bool IsCompletedSuccessfully(this Task task)
        {
            return task.Status == TaskStatus.RanToCompletion;
        }
#endif

#if !NET6_0_OR_GREATER
        private static readonly TaskContinuationOptions s_tco = TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously;
        private static void IgnoreTaskContinuation(Task t, object s) => t.Exception.Handle(e => true);

        // https://devblogs.microsoft.com/pfxteam/how-do-i-cancel-non-cancelable-async-operations/
        public static Task WaitAsync(this Task task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (task.IsCompletedSuccessfully())
            {
                return task;
            }
            else
            {
                return DoWaitWithTimeoutAsync(task, timeout, cancellationToken);
            }
        }

        private static async Task DoWaitWithTimeoutAsync(this Task task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using var timeoutTokenCts = new CancellationTokenSource(timeout);
            CancellationToken timeoutToken = timeoutTokenCts.Token;

            var linkedTokenTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutToken, cancellationToken);
            using CancellationTokenRegistration cancellationTokenRegistration =
                linkedCts.Token.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true),
                    state: linkedTokenTcs, useSynchronizationContext: false);

            if (task != await Task.WhenAny(task, linkedTokenTcs.Task).ConfigureAwait(false))
            {
                task.Ignore();
                if (timeoutToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException($"Operation timed out after {timeout}");
                }
                else
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            await task.ConfigureAwait(false);
        }

        // https://devblogs.microsoft.com/pfxteam/how-do-i-cancel-non-cancelable-async-operations/
        public static Task WaitAsync(this Task task, CancellationToken cancellationToken)
        {
            if (task.IsCompletedSuccessfully())
            {
                return task;
            }
            else
            {
                return DoWaitAsync(task, cancellationToken);
            }
        }

        private static async Task DoWaitAsync(this Task task, CancellationToken cancellationToken)
        {
            var cancellationTokenTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true),
                state: cancellationTokenTcs, useSynchronizationContext: false))
            {
                if (task != await Task.WhenAny(task, cancellationTokenTcs.Task).ConfigureAwait(false))
                {
                    task.Ignore();
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            await task.ConfigureAwait(false);
        }
#endif

        public static Task WaitAsync(this Task task, TimeSpan timeout)
        {
#if NET6_0_OR_GREATER
            if (task.IsCompletedSuccessfully)
            {
                return task;
            }

            return task.WaitAsync(timeout);
#else
            if (task.IsCompletedSuccessfully())
            {
                return task;
            }

            return DoTimeoutAfter(task, timeout);

            // https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md#using-a-timeout
            static async Task DoTimeoutAfter(Task task, TimeSpan timeout)
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
#endif
        }

        public static async ValueTask TimeoutAfter(this ValueTask valueTask, TimeSpan timeout)
        {
            if (valueTask.IsCompletedSuccessfully)
            {
                return;
            }

#if NET6_0_OR_GREATER
            Task task = valueTask.AsTask();
            await task.WaitAsync(timeout)
                .ConfigureAwait(false);
#else
            await DoTimeoutAfter(valueTask, timeout)
                .ConfigureAwait(false);

            // https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md#using-a-timeout
            static async ValueTask DoTimeoutAfter(ValueTask valueTask, TimeSpan timeout)
            {
                Task task = valueTask.AsTask();
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

                    await valueTask.ConfigureAwait(false);
                }
            }
#endif
        }

        /*
         * https://devblogs.microsoft.com/dotnet/configureawait-faq/
         * I'm using GetAwaiter().GetResult(). Do I need to use ConfigureAwait(false)?
         * Answer: No
         */
        public static void EnsureCompleted(this Task task)
        {
            task.GetAwaiter().GetResult();
        }

        public static T EnsureCompleted<T>(this Task<T> task)
        {
            return task.GetAwaiter().GetResult();
        }

        public static T EnsureCompleted<T>(this ValueTask<T> task)
        {
            return task.GetAwaiter().GetResult();
        }

        public static void EnsureCompleted(this ValueTask task)
        {
            if (false == task.IsCompletedSuccessfully)
            {
                task.GetAwaiter().GetResult();
            }
        }

#if NETSTANDARD
        // https://github.com/dotnet/runtime/issues/23878
        // https://github.com/dotnet/runtime/issues/23878#issuecomment-1398958645
        public static void Ignore(this Task task)
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
