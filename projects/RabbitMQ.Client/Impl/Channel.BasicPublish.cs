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

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Impl
{
    internal partial class Channel : IChannel, IRecoverable
    {
        private readonly AsyncManualResetEvent _flowControlBlock = new(true);

        public ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey,
            bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken = default)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            var cmd = new BasicPublish(exchange, routingKey, mandatory, default);
            return BasicPublishCoreAsync(cmd, basicProperties, body, bodyOwner: null, exchange, routingKey, cancellationToken);
        }

        public ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey,
            bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body, IDisposable? bodyOwner,
            CancellationToken cancellationToken = default)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            var cmd = new BasicPublish(exchange, routingKey, mandatory, default);
            return BasicPublishCoreAsync(cmd, basicProperties, body, bodyOwner, exchange, routingKey, cancellationToken);
        }

        public ValueTask BasicPublishAsync<TProperties>(CachedString exchange, CachedString routingKey,
            bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken = default)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            var cmd = new BasicPublishMemory(exchange.Bytes, routingKey.Bytes, mandatory, default);
            return BasicPublishCoreAsync(cmd, basicProperties, body, bodyOwner: null, exchange.Value, routingKey.Value, cancellationToken);
        }

        public ValueTask BasicPublishAsync<TProperties>(CachedString exchange, CachedString routingKey,
            bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body, IDisposable? bodyOwner,
            CancellationToken cancellationToken = default)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            var cmd = new BasicPublishMemory(exchange.Bytes, routingKey.Bytes, mandatory, default);
            return BasicPublishCoreAsync(cmd, basicProperties, body, bodyOwner, exchange.Value, routingKey.Value, cancellationToken);
        }

        public ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey,
            bool mandatory, TProperties basicProperties, ReadOnlySequence<byte> body, IDisposable? bodyOwner,
            CancellationToken cancellationToken = default)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            ValidateBodyLength(body, bodyOwner);
            var cmd = new BasicPublish(exchange, routingKey, mandatory, default);
            return BasicPublishCoreAsync(cmd, basicProperties, body, bodyOwner, exchange, routingKey, cancellationToken);
        }

        public ValueTask BasicPublishAsync<TProperties>(CachedString exchange, CachedString routingKey,
            bool mandatory, TProperties basicProperties, ReadOnlySequence<byte> body, IDisposable? bodyOwner,
            CancellationToken cancellationToken = default)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            ValidateBodyLength(body, bodyOwner);
            var cmd = new BasicPublishMemory(exchange.Bytes, routingKey.Bytes, mandatory, default);
            return BasicPublishCoreAsync(cmd, basicProperties, body, bodyOwner, exchange.Value, routingKey.Value, cancellationToken);
        }

        /// <summary>
        /// A message body must be addressable with an <see cref="int"/>, both because that is the
        /// limit of a single AMQP content header and because the set of frames used to send it is
        /// allocated as one contiguous block. Ownership of <paramref name="bodyOwner"/> has already
        /// been transferred by the caller, so it has to be disposed here when the body is rejected.
        /// A stricter total-frame-set-size check in <see cref="Framing.GetTotalFrameSetSize"/> guards further downstream.
        /// This check is kept here as well so an oversize body is rejected synchronously, before a publisher confirmation sequence number is consumed.
        /// </summary>
        private static void ValidateBodyLength(ReadOnlySequence<byte> body, IDisposable? bodyOwner)
        {
            if (body.Length > int.MaxValue)
            {
                bodyOwner?.Dispose();
                throw new ArgumentOutOfRangeException(nameof(body), body.Length,
                    $"Message body of {body.Length} bytes exceeds the maximum of {int.MaxValue} bytes.");
            }
        }

        // Deliberately kept parallel to the ReadOnlySequence overload below rather than unified: a
        // dedicated ReadOnlyMemory path keeps the memory publish hot path from wrapping the body in
        // a ReadOnlySequence, which is the cost the split exists to avoid.
        private async ValueTask BasicPublishCoreAsync<TMethod, TProperties>(
            TMethod cmd, TProperties basicProperties, ReadOnlyMemory<byte> body, IDisposable? bodyOwner,
            string exchange, string routingKey, CancellationToken cancellationToken)
            where TMethod : struct, IOutgoingAmqpMethod
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            // Track whether bodyOwner has been transferred to the OutgoingFrame (and thus to
            // SocketFrameHandler, which will dispose it). If not, we must dispose it ourselves on
            // any exception path to prevent a resource leak.
            bool bodyOwnerTransferred = false;
            try
            {
                PublisherConfirmationInfo? publisherConfirmationInfo = null;
                RateLimitLease? lease =
                    await MaybeAcquirePublisherConfirmationLockAsync(cancellationToken)
                        .ConfigureAwait(false);
                /*
                 * sendActivity is declared before the try, not with a `using` scoped to
                 * it, so two properties hold at once. First, the catch and the finally's
                 * confirmation-await catch can record a publish failure on it; with the
                 * `using` scoped to the inner try the span was already disposed by the
                 * time the catch ran, so no failure was reported and the span ended
                 * status=Unset, which tracing backends read as a successful publish.
                 * Second, it is assigned as the first statement inside the try (below)
                 * rather than out here, so that if starting the activity throws - a user
                 * ActivityListener callback can - the inner finally still runs and
                 * releases the publisher-confirmation lock acquired above. Created out
                 * here, a throw would skip that finally, leak the semaphore, and deadlock
                 * the next publish on this channel. It is disposed in that finally, after
                 * the confirmation await. See issue #1967.
                 */
                Activity? sendActivity = null;
                /*
                 * Tracks the exception (if any) already recorded on sendActivity by the
                 * catch below, so the finally's confirmation-await catch does not record
                 * the same instance twice. When MaybeHandleExceptionWithEnabledPublisherConfirmations
                 * faults the confirm TCS, the finally's await re-raises that exception;
                 * without this guard a publish whose send failed (e.g. on a closed
                 * connection) recorded the same exception twice. See issue #1967.
                 */
                Exception? recordedSendError = null;
                try
                {
                    sendActivity = RabbitMQActivitySource.PublisherHasListeners
                        ? RabbitMQActivitySource.BasicPublish(routingKey, exchange, body.Length, basicProperties)
                        : default;

                    publisherConfirmationInfo = MaybeStartPublisherConfirmationTracking();

                    await MaybeEnforceFlowControlAsync(cancellationToken)
                        .ConfigureAwait(false);

                    ulong publishSequenceNumber = publisherConfirmationInfo?.PublishSequenceNumber ?? 0;

                    BasicProperties? props = PopulateBasicPropertiesHeaders(basicProperties, sendActivity, publishSequenceNumber);
                    bodyOwnerTransferred = true;
                    if (props is null)
                    {
                        await ModelSendAsync(in cmd, in basicProperties, body, bodyOwner, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await ModelSendAsync(in cmd, in props, body, bodyOwner, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    /*
                     * Caller-initiated cancellation is not a publish failure, so it is
                     * not recorded on the span. Confirmation tracking still needs the
                     * cleanup below (faulting the TCS, decrementing the sequence number),
                     * which is why this is an inline guard rather than a `when` filter:
                     * a filter that skipped this catch would skip the cleanup too.
                     * See issue #1967.
                     */
                    bool isCallerCancellation =
                        ex is OperationCanceledException && cancellationToken.IsCancellationRequested;
                    if (!isCallerCancellation)
                    {
                        sendActivity.SetActivityError(ex);
                        recordedSendError = ex;
                    }

                    /*
                     * "Handled" here means the exception was routed onto the publisher
                     * confirmation task, not that it was swallowed: the finally below
                     * awaits that task and re-raises the same instance to the caller. So
                     * recording the error above is correct even when exceptionWasHandled
                     * is true - the publish failed and the caller sees it, just through
                     * the confirmation channel rather than a throw from here. This is not
                     * the spec's "handled or retried and completed gracefully" exemption,
                     * which is for operations that recover; a faulted publish never does.
                     * Every path that records Error is one the caller observes as a
                     * failure. See issue #1967.
                     */
                    bool exceptionWasHandled =
                        MaybeHandleExceptionWithEnabledPublisherConfirmations(publisherConfirmationInfo, ex);
                    if (!exceptionWasHandled)
                    {
                        throw;
                    }
                }
                finally
                {
                    MaybeReleasePublisherConfirmationLock(lease);

                    /*
                     * This await is the one that surfaces a nack or an unroutable
                     * mandatory publish (PublishException), so it is a publish failure
                     * like any other and belongs on the span. It cannot simply be
                     * wrapped by the catch above, because it runs in the finally: the
                     * confirmation is only awaited once the send has been issued.
                     */
                    try
                    {
                        await MaybeEndPublisherConfirmationTrackingAsync(publisherConfirmationInfo, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // Caller-initiated cancellation during the confirmation await is
                        // not a publish failure. See issue #1967.
                        throw;
                    }
                    catch (Exception ex) when (!ReferenceEquals(ex, recordedSendError))
                    {
                        sendActivity.SetActivityError(ex);
                        throw;
                    }
                    finally
                    {
                        // Dispose here, not via `using`, so the span outlives the
                        // confirmation await above whose catch may record on it.
                        sendActivity?.Dispose();
                    }
                }
            }
            finally
            {
                if (!bodyOwnerTransferred)
                {
                    bodyOwner?.Dispose();
                }
            }
        }

        // NOTE: BasicPublishCoreAsync is an async method, so the ReadOnlySequence<byte> body cannot be
        // passed by reference (async methods cannot have in/ref parameters); it is taken by value here.
        // The public overloads also take the body by value rather than by `in`: because this core copies
        // it regardless, `in` on the public API would have saved at most one 24-byte copy while
        // constraining the signature (for example, blocking a future move to a truly async forwarder).
        private async ValueTask BasicPublishCoreAsync<TMethod, TProperties>(
            TMethod cmd, TProperties basicProperties, ReadOnlySequence<byte> body, IDisposable? bodyOwner,
            string exchange, string routingKey, CancellationToken cancellationToken)
            where TMethod : struct, IOutgoingAmqpMethod
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            // Track whether bodyOwner has been transferred to the OutgoingFrame (and thus to
            // SocketFrameHandler, which will dispose it). If not, we must dispose it ourselves on
            // any exception path to prevent a resource leak.
            bool bodyOwnerTransferred = false;
            try
            {
                PublisherConfirmationInfo? publisherConfirmationInfo = null;
                RateLimitLease? lease =
                    await MaybeAcquirePublisherConfirmationLockAsync(cancellationToken)
                        .ConfigureAwait(false);
                try
                {
                    publisherConfirmationInfo = MaybeStartPublisherConfirmationTracking();

                    await MaybeEnforceFlowControlAsync(cancellationToken)
                        .ConfigureAwait(false);

                    using Activity? sendActivity = RabbitMQActivitySource.PublisherHasListeners
                        ? RabbitMQActivitySource.BasicPublish(routingKey, exchange, (int)body.Length, basicProperties)
                        : default;

                    ulong publishSequenceNumber = publisherConfirmationInfo?.PublishSequenceNumber ?? 0;

                    BasicProperties? props = PopulateBasicPropertiesHeaders(basicProperties, sendActivity, publishSequenceNumber);
                    bodyOwnerTransferred = true;
                    if (props is null)
                    {
                        await ModelSendAsync(in cmd, in basicProperties, body, bodyOwner, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await ModelSendAsync(in cmd, in props, body, bodyOwner, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    bool exceptionWasHandled =
                        MaybeHandleExceptionWithEnabledPublisherConfirmations(publisherConfirmationInfo, ex);
                    if (!exceptionWasHandled)
                    {
                        throw;
                    }
                }
                finally
                {
                    MaybeReleasePublisherConfirmationLock(lease);
                    await MaybeEndPublisherConfirmationTrackingAsync(publisherConfirmationInfo, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                if (!bodyOwnerTransferred)
                {
                    bodyOwner?.Dispose();
                }
            }
        }

        private BasicProperties? PopulateBasicPropertiesHeaders<TProperties>(TProperties basicProperties,
            Activity? sendActivity, ulong publishSequenceNumber)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            /*
             * Note: there is nothing to do in this method if *both* of these
             * conditions are true:
             *
             * sendActivity is null - there is no activity to add as a header
             * publisher confirmations are NOT enabled
             */
            if (sendActivity is null && !_publisherConfirmationsEnabled)
            {
                return null;
            }

            bool newHeaders = false;
            IDictionary<string, object?>? headers = basicProperties.Headers;
            if (headers is null)
            {
                headers = new Dictionary<string, object?>();
                newHeaders = true;
            }
            MaybeAddActivityToHeaders(headers, basicProperties.CorrelationId, sendActivity);
            MaybeAddPublishSequenceNumberToHeaders(headers);

            switch (basicProperties)
            {
                case BasicProperties writableProperties:
                    if (newHeaders)
                    {
                        writableProperties.Headers = headers;
                    }
                    return null;
                case EmptyBasicProperty:
                    return new BasicProperties { Headers = headers };
                default:
                    return new BasicProperties(basicProperties) { Headers = headers };
            }

            void MaybeAddActivityToHeaders(IDictionary<string, object?> headers,
                string? correlationId, Activity? sendActivity)
            {
                if (sendActivity is not null)
                {
                    // This activity is marked as recorded, so let's propagate the trace and span ids.
                    if (sendActivity.IsAllDataRequested)
                    {
                        if (!string.IsNullOrEmpty(correlationId))
                        {
                            sendActivity.SetTag(RabbitMQActivitySource.MessageConversationId, correlationId);
                        }
                    }

                    // Inject the ActivityContext into the message headers to propagate trace context to the receiving service.
                    RabbitMQActivitySource.ContextInjector(sendActivity, headers);
                }
            }

            void MaybeAddPublishSequenceNumberToHeaders(IDictionary<string, object?> headers)
            {
                if (_publisherConfirmationsEnabled && _publisherConfirmationTrackingEnabled)
                {
                    if (publishSequenceNumber > long.MaxValue)
                    {
                        headers[Constants.PublishSequenceNumberHeader] = publishSequenceNumber.ToString();
                    }
                    else
                    {
                        headers[Constants.PublishSequenceNumberHeader] = (long)publishSequenceNumber;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Task MaybeEnforceFlowControlAsync(CancellationToken cancellationToken)
        {
            if (_flowControlBlock.IsSet)
            {
                return Task.CompletedTask;
            }

            return _flowControlBlock.WaitAsync(cancellationToken);
        }
    }
}
