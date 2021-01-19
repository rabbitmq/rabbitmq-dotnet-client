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
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.Framing.Impl
{
#nullable enable
    internal sealed partial class AutorecoveringConnection
    {
        private Task? _recoveryTask;
        private CancellationTokenSource? _recoveryCancellationTokenSource;

        private CancellationTokenSource RecoveryCancellationTokenSource => _recoveryCancellationTokenSource ??= new CancellationTokenSource();

        private void HandleConnectionShutdown(object _, ShutdownEventArgs args)
        {
            if (ShouldTriggerConnectionRecovery(args))
            {
                var recoverTask = new Task<Task>(RecoverConnectionAsync);
                if (Interlocked.CompareExchange(ref _recoveryTask, recoverTask.Unwrap(), null) is null)
                {
                    recoverTask.Start();
                }
            }

            static bool ShouldTriggerConnectionRecovery(ShutdownEventArgs args) =>
                args.Initiator == ShutdownInitiator.Peer ||
                // happens when EOF is reached, e.g. due to RabbitMQ node
                // connectivity loss or abrupt shutdown
                args.Initiator == ShutdownInitiator.Library;
        }

        private async Task RecoverConnectionAsync()
        {
            try
            {
                var token = RecoveryCancellationTokenSource.Token;
                bool success;
                do
                {
                    await Task.Delay(_factory.NetworkRecoveryInterval, token).ConfigureAwait(false);
                    success = TryPerformAutomaticRecovery();
                } while (!success && !token.IsCancellationRequested);
            }
            catch (OperationCanceledException)
            {
                // expected when recovery cancellation token is set.
            }
            catch (Exception e)
            {
                ESLog.Error("Main recovery loop threw unexpected exception.", e);
            }

            // clear recovery task
            _recoveryTask = null;
        }

        /// <summary>
        /// Cancels the main recovery loop and will block until the loop finishes, or the timeout
        /// expires, to prevent Close operations overlapping with recovery operations.
        /// </summary>
        private void StopRecoveryLoop()
        {
            var task = _recoveryTask;
            if (task is null)
            {
                return;
            }
            RecoveryCancellationTokenSource.Cancel();

            Task timeout = Task.Delay(_factory.RequestedConnectionTimeout);
            if (Task.WhenAny(task, timeout).Result == timeout)
            {
                ESLog.Warn("Timeout while trying to stop background AutorecoveringConnection recovery loop.");
            }
        }
    }
}
