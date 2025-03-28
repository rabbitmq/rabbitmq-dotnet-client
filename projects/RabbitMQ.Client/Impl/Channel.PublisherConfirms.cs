// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Impl
{
    internal partial class Channel : IChannel, IRecoverable
    {
        private bool _publisherConfirmationsEnabled = false;
        private bool _publisherConfirmationTrackingEnabled = false;
        private ulong _nextPublishSeqNo = 0;
        private readonly SemaphoreSlim _confirmSemaphore = new(1, 1);
        private readonly ConcurrentDictionary<ulong, TaskCompletionSource<bool>> _confirmsTaskCompletionSources = new();
        private RateLimiter? _outstandingPublisherConfirmationsRateLimiter;

        private sealed class PublisherConfirmationInfo : IDisposable
        {
            private TaskCompletionSource<bool>? _publisherConfirmationTcs;
            private readonly RateLimitLease? _lease;

            internal PublisherConfirmationInfo(ulong publishSequenceNumber,
                TaskCompletionSource<bool>? publisherConfirmationTcs,
                RateLimitLease? lease)
            {
                PublishSequenceNumber = publishSequenceNumber;
                _publisherConfirmationTcs = publisherConfirmationTcs;
                _lease = lease;
            }

            internal ulong PublishSequenceNumber { get; }

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

            public void Dispose()
            {
                _lease?.Dispose();
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
            RateLimiter? outstandingPublisherConfirmationsRateLimiter)
        {
            _publisherConfirmationsEnabled = publisherConfirmationsEnabled;
            _publisherConfirmationTrackingEnabled = publisherConfirmationTrackingEnabled;
            _outstandingPublisherConfirmationsRateLimiter = outstandingPublisherConfirmationsRateLimiter;
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
                    foreach (KeyValuePair<ulong, TaskCompletionSource<bool>> pair in _confirmsTaskCompletionSources.ToArray())
                    {
                        if (pair.Key <= deliveryTag)
                        {
                            if (_confirmsTaskCompletionSources.TryRemove(pair.Key, out TaskCompletionSource<bool>? tcs))
                            {
                                tcs.SetResult(true);
                            }
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
                    foreach (KeyValuePair<ulong, TaskCompletionSource<bool>> pair in _confirmsTaskCompletionSources.ToArray())
                    {
                        if (pair.Key <= deliveryTag)
                        {
                            if (_confirmsTaskCompletionSources.TryRemove(pair.Key, out TaskCompletionSource<bool>? tcs))
                            {
                                tcs.SetException(new PublishException(pair.Key, isReturn));
                            }
                        }
                    }
                }
                else
                {
                    if (_confirmsTaskCompletionSources.TryRemove(deliveryTag, out TaskCompletionSource<bool>? tcs))
                    {
                        tcs.SetException(new PublishException(deliveryTag, isReturn));
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HandleReturn(BasicReturnEventArgs basicReturnEvent)
        {
            if (_publisherConfirmationsEnabled && _publisherConfirmationTrackingEnabled)
            {
                ulong publishSequenceNumber = 0;
                IReadOnlyBasicProperties props = basicReturnEvent.BasicProperties;
                object? maybeSeqNum = props.Headers?[Constants.PublishSequenceNumberHeader];
                if (maybeSeqNum != null)
                {
                    switch (maybeSeqNum)
                    {
                        case long seqNumLong:
                            publishSequenceNumber = (ulong)seqNumLong;
                            break;
                        case string seqNumString:
                            publishSequenceNumber = ulong.Parse(seqNumString);
                            break;
                        case byte[] seqNumBytes:
                            publishSequenceNumber = ulong.Parse(Encoding.ASCII.GetString(seqNumBytes));
                            break;
                    }
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
                RateLimitLease? lease = null;
                if (_publisherConfirmationTrackingEnabled)
                {
                    if (_outstandingPublisherConfirmationsRateLimiter is not null)
                    {
                        lease = await _outstandingPublisherConfirmationsRateLimiter.AcquireAsync(
                            cancellationToken: cancellationToken)
                            .ConfigureAwait(false);

                        if (!lease.IsAcquired)
                        {
                            throw new InvalidOperationException("Could not acquire a lease from the rate limiter.");
                        }
                    }
                }

                await _confirmSemaphore.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);

                ulong publishSequenceNumber = _nextPublishSeqNo;

                TaskCompletionSource<bool>? publisherConfirmationTcs = null;
                if (_publisherConfirmationTrackingEnabled)
                {
                    publisherConfirmationTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    if (!_confirmsTaskCompletionSources.TryAdd(publishSequenceNumber, publisherConfirmationTcs))
                    {
                        throw new InvalidOperationException($"Failed to track the publisher confirmation for sequence number '{publishSequenceNumber}' because it already exists.");
                    }
                }

                _nextPublishSeqNo++;

                return new PublisherConfirmationInfo(publishSequenceNumber, publisherConfirmationTcs, lease);
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
                try
                {
                    _confirmSemaphore.Release();
                }
                catch (SemaphoreFullException ex)
                {
                    /*
                     * rabbitmq/rabbitmq-dotnet-client-1793
                     * If MaybeStartPublisherConfirmationTracking throws an exception *prior* to acquiring
                     * _confirmSemaphore, the above Release() call will throw SemaphoreFullException.
                     * In "normal" cases, publisherConfirmationInfo will thus be null, but if not, throw
                     * a "bug found" exception here.
                     */
                    if (publisherConfirmationInfo is not null)
                    {
                        throw new InvalidOperationException(InternalConstants.BugFound, ex);
                    }
                }

                if (publisherConfirmationInfo is not null)
                {
                    try
                    {
                        await publisherConfirmationInfo.MaybeWaitForConfirmationAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        _confirmsTaskCompletionSources.TryRemove(publisherConfirmationInfo.PublishSequenceNumber, out _);
                        throw;
                    }
                    finally
                    {
                        publisherConfirmationInfo.Dispose();
                    }
                }
            }
        }
    }
}
