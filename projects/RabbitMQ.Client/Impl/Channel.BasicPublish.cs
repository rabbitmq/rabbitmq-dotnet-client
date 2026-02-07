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

        public async ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey,
            bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken = default)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            PublisherConfirmationInfo? publisherConfirmationInfo = null;
            RateLimitLease? lease =
                await MaybeAcquirePublisherConfirmationLockAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                publisherConfirmationInfo = MaybeStartPublisherConfirmationTracking();

                await MaybeEnforceFlowControlAsync(cancellationToken)
                    .ConfigureAwait(false);

                var cmd = new BasicPublish(exchange, routingKey, mandatory, default);

                using Activity? sendActivity = RabbitMQActivitySource.PublisherHasListeners
                    ? RabbitMQActivitySource.BasicPublish(routingKey, exchange, body.Length, basicProperties)
                    : default;

                ulong publishSequenceNumber = 0;
                if (publisherConfirmationInfo is not null)
                {
                    publishSequenceNumber = publisherConfirmationInfo.PublishSequenceNumber;
                }

                BasicProperties? props = PopulateBasicPropertiesHeaders(basicProperties, sendActivity, publishSequenceNumber);
                if (props is null)
                {
                    await ModelSendAsync(in cmd, in basicProperties, body, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await ModelSendAsync(in cmd, in props, body, cancellationToken)
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

        public async ValueTask BasicPublishAsync<TProperties>(CachedString exchange, CachedString routingKey,
            bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken = default)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            PublisherConfirmationInfo? publisherConfirmationInfo = null;
            RateLimitLease? lease =
                await MaybeAcquirePublisherConfirmationLockAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                publisherConfirmationInfo = MaybeStartPublisherConfirmationTracking();

                await MaybeEnforceFlowControlAsync(cancellationToken)
                    .ConfigureAwait(false);

                var cmd = new BasicPublishMemory(exchange.Bytes, routingKey.Bytes, mandatory, default);

                using Activity? sendActivity = RabbitMQActivitySource.PublisherHasListeners
                    ? RabbitMQActivitySource.BasicPublish(routingKey.Value, exchange.Value, body.Length, basicProperties)
                    : default;

                ulong publishSequenceNumber = 0;
                if (publisherConfirmationInfo is not null)
                {
                    publishSequenceNumber = publisherConfirmationInfo.PublishSequenceNumber;
                }

                BasicProperties? props = PopulateBasicPropertiesHeaders(basicProperties, sendActivity, publishSequenceNumber);
                if (props is null)
                {
                    await ModelSendAsync(in cmd, in basicProperties, body, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await ModelSendAsync(in cmd, in props, body, cancellationToken)
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
