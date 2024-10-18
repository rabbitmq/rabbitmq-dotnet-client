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

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Impl
{
    internal partial class Channel : IChannel, IRecoverable
    {
        // private readonly AsyncManualResetEvent _flowControlBlock = new AsyncManualResetEvent(true);

        private bool _publisherConfirmationsEnabled = false;
        private bool _publisherConfirmationTrackingEnabled = false;
        private ulong _nextPublishSeqNo = 0;
        private readonly SemaphoreSlim _confirmSemaphore = new(1, 1);
        private readonly ConcurrentDictionary<ulong, TaskCompletionSource<bool>> _confirmsTaskCompletionSources = new();

        private void ConfigurePublisherConfirmations(bool publisherConfirmationsEnabled, bool publisherConfirmationTrackingEnabled)
        {
            _publisherConfirmationsEnabled = publisherConfirmationsEnabled;
            _publisherConfirmationTrackingEnabled = publisherConfirmationTrackingEnabled;
        }

        private async Task MaybeConfirmSelect(CancellationToken cancellationToken)
        {
            if (_publisherConfirmationsEnabled)
            {
                // NOTE: _rpcSemaphore is held
                bool enqueued = false;
                var k = new ConfirmSelectAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

                try
                {
                    if (_nextPublishSeqNo == 0UL)
                    {
                        if (_publisherConfirmationTrackingEnabled)
                        {
                            _confirmsTaskCompletionSources.Clear();
                        }
                        _nextPublishSeqNo = 1;
                    }

                    enqueued = Enqueue(k);

                    var method = new ConfirmSelect(false);
                    await ModelSendAsync(in method, k.CancellationToken)
                        .ConfigureAwait(false);

                    bool result = await k;
                    Debug.Assert(result);

                    return;
                }
                finally
                {
                    if (false == enqueued)
                    {
                        k.Dispose();
                    }
                }
            }
        }
    }
}
