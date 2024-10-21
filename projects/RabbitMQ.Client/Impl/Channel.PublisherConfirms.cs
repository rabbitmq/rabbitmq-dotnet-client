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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Util;

namespace RabbitMQ.Client.Impl
{
    internal partial class Channel : IChannel, IRecoverable
    {
        private bool _publisherConfirmationsEnabled = false;
        private bool _publisherConfirmationTrackingEnabled = false;
        private ushort? _maxOutstandingPublisherConfirmations = null;
        private SemaphoreSlim? _maxOutstandingConfirmationsSemaphore;
        private ulong _nextPublishSeqNo = 0;
        private readonly SemaphoreSlim _confirmSemaphore = new(1, 1);
        private readonly ConcurrentDictionary<ulong, TaskCompletionSource<bool>> _confirmsTaskCompletionSources = new();

        private class PublisherConfirmationInfo
        {
            private ulong _publishSequenceNumber;
            private TaskCompletionSource<bool>? _publisherConfirmationTcs;

            internal PublisherConfirmationInfo()
            {
                _publishSequenceNumber = 0;
                _publisherConfirmationTcs = null;
            }

            internal PublisherConfirmationInfo(ulong publishSequenceNumber, TaskCompletionSource<bool>? publisherConfirmationTcs)
            {
                _publishSequenceNumber = publishSequenceNumber;
                _publisherConfirmationTcs = publisherConfirmationTcs;
            }

            internal ulong PublishSequenceNumber => _publishSequenceNumber;

            internal TaskCompletionSource<bool>? PublisherConfirmationTcs => _publisherConfirmationTcs;

            internal async Task MaybeWaitForConfirmationAsync(CancellationToken cancellationToken)
            {
                if (_publisherConfirmationTcs is not null)
                {
                    await _publisherConfirmationTcs.Task.WaitAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            internal bool MaybeHandleException(Exception ex)
            {
                bool exceptionWasHandled = false;

                if (_publisherConfirmationTcs is not null)
                {
                    _publisherConfirmationTcs.SetException(ex);
                    exceptionWasHandled = true;
                }

                return exceptionWasHandled;
            }
        }

        public async ValueTask<ulong> GetNextPublishSequenceNumberAsync(CancellationToken cancellationToken = default)
        {
            if (_publisherConfirmationsEnabled)
            {
                await _confirmSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    return _nextPublishSeqNo;
                }
                finally
                {
                    _confirmSemaphore.Release();
                }
            }
            else
            {
                return _nextPublishSeqNo;
            }
        }

        private void ConfigurePublisherConfirmations(bool publisherConfirmationsEnabled,
            bool publisherConfirmationTrackingEnabled,
            ushort? maxOutstandingPublisherConfirmations)
        {
            _publisherConfirmationsEnabled = publisherConfirmationsEnabled;
            _publisherConfirmationTrackingEnabled = publisherConfirmationTrackingEnabled;
            _maxOutstandingPublisherConfirmations = maxOutstandingPublisherConfirmations;

            if (_publisherConfirmationTrackingEnabled && _maxOutstandingPublisherConfirmations is not null)
            {
                _maxOutstandingConfirmationsSemaphore = new SemaphoreSlim(
                    (int)_maxOutstandingPublisherConfirmations,
                    (int)_maxOutstandingPublisherConfirmations);
            }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldHandleAckOrNack(ulong deliveryTag)
        {
            return _publisherConfirmationsEnabled && _publisherConfirmationTrackingEnabled &&
                deliveryTag > 0 && !_confirmsTaskCompletionSources.IsEmpty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HandleAck(ulong deliveryTag, bool multiple)
        {
            if (ShouldHandleAckOrNack(deliveryTag))
            {
                if (multiple)
                {
                    foreach (KeyValuePair<ulong, TaskCompletionSource<bool>> pair in _confirmsTaskCompletionSources)
                    {
                        if (pair.Key <= deliveryTag)
                        {
                            pair.Value.SetResult(true);
                            _confirmsTaskCompletionSources.Remove(pair.Key, out _);
                        }
                    }
                }
                else
                {
                    if (_confirmsTaskCompletionSources.TryRemove(deliveryTag, out TaskCompletionSource<bool>? tcs))
                    {
                        tcs.SetResult(true);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HandleNack(ulong deliveryTag, bool multiple, bool isReturn)
        {
            if (ShouldHandleAckOrNack(deliveryTag))
            {
                if (multiple)
                {
                    foreach (KeyValuePair<ulong, TaskCompletionSource<bool>> pair in _confirmsTaskCompletionSources)
                    {
                        if (pair.Key <= deliveryTag)
                        {
                            pair.Value.SetException(new PublishException(pair.Key, isReturn));
                            _confirmsTaskCompletionSources.Remove(pair.Key, out _);
                        }
                    }
                }
                else
                {
                    if (_confirmsTaskCompletionSources.Remove(deliveryTag, out TaskCompletionSource<bool>? tcs))
                    {
                        tcs.SetException(new PublishException(deliveryTag, isReturn));
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HandleReturn(BasicReturnEventArgs basicReturnEvent)
        {
            if (_publisherConfirmationsEnabled)
            {
                ulong publishSequenceNumber = 0;
                IReadOnlyBasicProperties props = basicReturnEvent.BasicProperties;
                object? maybeSeqNum = props.Headers?[Constants.PublishSequenceNumberHeader];
                if (maybeSeqNum != null &&
                    maybeSeqNum is byte[] seqNumBytes)
                {
                    publishSequenceNumber = NetworkOrderDeserializer.ReadUInt64(seqNumBytes);
                }

                HandleNack(publishSequenceNumber, multiple: false, isReturn: true);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task MaybeHandlePublisherConfirmationTcsOnChannelShutdownAsync(ShutdownEventArgs reason)
        {
            if (_publisherConfirmationsEnabled)
            {
                await _confirmSemaphore.WaitAsync(reason.CancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    if (!_confirmsTaskCompletionSources.IsEmpty)
                    {
                        var exception = new AlreadyClosedException(reason);
                        foreach (TaskCompletionSource<bool> confirmsTaskCompletionSource in _confirmsTaskCompletionSources.Values)
                        {
                            confirmsTaskCompletionSource.TrySetException(exception);
                        }

                        _confirmsTaskCompletionSources.Clear();
                    }
                }
                finally
                {
                    _confirmSemaphore.Release();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<PublisherConfirmationInfo?> MaybeStartPublisherConfirmationTracking(CancellationToken cancellationToken)
        {
            if (_publisherConfirmationsEnabled)
            {
                /*
                if (_publisherConfirmationTrackingEnabled)
                {
                    if (_maxOutstandingConfirmationsSemaphore is not null)
                    {
                        await _maxOutstandingConfirmationsSemaphore.WaitAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                */

                await _confirmSemaphore.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);

                ulong publishSequenceNumber = _nextPublishSeqNo;

                TaskCompletionSource<bool>? publisherConfirmationTcs = null;
                if (_publisherConfirmationTrackingEnabled)
                {
                    publisherConfirmationTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _confirmsTaskCompletionSources[publishSequenceNumber] = publisherConfirmationTcs;
                }

                _nextPublishSeqNo++;

                return new PublisherConfirmationInfo(publishSequenceNumber, publisherConfirmationTcs);
            }
            else
            {
                return null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool MaybeHandleExceptionWithEnabledPublisherConfirmations(PublisherConfirmationInfo? publisherConfirmationInfo,
            Exception ex)
        {
            bool exceptionWasHandled = false;

            if (_publisherConfirmationsEnabled &&
                publisherConfirmationInfo is not null)
            {
                _nextPublishSeqNo--;

                if (_publisherConfirmationTrackingEnabled)
                {
                    _confirmsTaskCompletionSources.TryRemove(publisherConfirmationInfo.PublishSequenceNumber, out _);
                }

                exceptionWasHandled = publisherConfirmationInfo.MaybeHandleException(ex);
            }

            return exceptionWasHandled;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task MaybeEndPublisherConfirmationTracking(PublisherConfirmationInfo? publisherConfirmationInfo,
            CancellationToken cancellationToken)
        {
            if (_publisherConfirmationsEnabled)
            {
                _confirmSemaphore.Release();

                if (publisherConfirmationInfo is not null)
                {
                    await publisherConfirmationInfo.MaybeWaitForConfirmationAsync(cancellationToken)
                        .ConfigureAwait(false);
                }

                if (_publisherConfirmationTrackingEnabled &&
                    _maxOutstandingConfirmationsSemaphore is not null)
                {
                    // _maxOutstandingConfirmationsSemaphore.Release();
                }
            }
        }
    }
}
