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

using System.Collections.Generic;

namespace RabbitMQ.Client
{
#nullable enable
    /// <summary>
    /// The AMQP Basic headers class interface,
    /// spanning the union of the functionality offered by versions
    /// 0-8, 0-8qpid, 0-9 and 0-9-1 of AMQP.
    /// </summary>
    public interface IReadOnlyBasicProperties
    {
        /// <summary>
        /// Application Id.
        /// </summary>
        string? AppId { get; }

        /// <summary>
        /// Intra-cluster routing identifier (cluster id is deprecated in AMQP 0-9-1).
        /// </summary>
        string? ClusterId { get; }

        /// <summary>
        /// MIME content encoding.
        /// </summary>
        string? ContentEncoding { get; }

        /// <summary>
        /// MIME content type.
        /// </summary>
        string? ContentType { get; }

        /// <summary>
        /// Application correlation identifier.
        /// </summary>
        string? CorrelationId { get; }

        /// <summary>
        /// Non-persistent (1) or persistent (2).
        /// </summary>
        DeliveryModes DeliveryMode { get; }

        /// <summary>
        /// Message expiration specification.
        /// </summary>
        string? Expiration { get; }

        /// <summary>
        /// Message header field table. Is of type <see cref="IDictionary{TKey,TValue}" />.
        /// </summary>
        IDictionary<string, object?>? Headers { get; }

        /// <summary>
        /// Application message Id.
        /// </summary>
        string? MessageId { get; }

        /// <summary>
        /// Sets <see cref="DeliveryMode"/> to either persistent (2) or non-persistent (1).
        /// </summary>
        bool Persistent { get; }

        /// <summary>
        /// Message priority, 0 to 9.
        /// </summary>
        byte Priority { get; }

        /// <summary>
        /// Destination to reply to.
        /// </summary>
        string? ReplyTo { get; }

        /// <summary>
        /// Convenience property; parses <see cref="ReplyTo"/> property using <see cref="PublicationAddress.TryParse"/>,
        /// and serializes it using <see cref="PublicationAddress.ToString"/>.
        /// Returns null if <see cref="ReplyTo"/> property cannot be parsed by <see cref="PublicationAddress.TryParse"/>.
        /// </summary>
        PublicationAddress? ReplyToAddress { get; }

        /// <summary>
        /// Message timestamp.
        /// </summary>
        AmqpTimestamp Timestamp { get; }

        /// <summary>
        /// Message type name.
        /// </summary>
        string? Type { get; }

        /// <summary>
        /// User Id.
        /// </summary>
        string? UserId { get; }

        /// <summary>
        /// Returns true if the <see cref="IReadOnlyBasicProperties.AppId"/> property is present.
        /// </summary>
        bool IsAppIdPresent();

        /// <summary>
        /// Returns true if the <see cref="IReadOnlyBasicProperties.ClusterId"/> property is present (cluster id is deprecated in AMQP 0-9-1).
        /// </summary>
        bool IsClusterIdPresent();

        /// <summary>
        /// Returns true if the <see cref="IReadOnlyBasicProperties.ContentEncoding"/> property is present.
        /// </summary>
        bool IsContentEncodingPresent();

        /// <summary>
        /// Returns true if the <see cref="IReadOnlyBasicProperties.ContentType"/> property is present.
        /// </summary>
        bool IsContentTypePresent();

        /// <summary>
        /// Returns true if the <see cref="IReadOnlyBasicProperties.CorrelationId"/> property is present.
        /// </summary>
        bool IsCorrelationIdPresent();

        /// <summary>
        /// Returns true if the <see cref="IReadOnlyBasicProperties.DeliveryMode"/> property is present.
        /// </summary>
        bool IsDeliveryModePresent();

        /// <summary>
        /// Returns true if the <see cref="IReadOnlyBasicProperties.Expiration"/> property is present.
        /// </summary>
        bool IsExpirationPresent();

        /// <summary>
        /// Returns true if the <see cref="IReadOnlyBasicProperties.Headers"/> property is present.
        /// </summary>
        bool IsHeadersPresent();

        /// <summary>
        /// Returns true if the <see cref="IReadOnlyBasicProperties.MessageId"/> property is present.
        /// </summary>
        bool IsMessageIdPresent();

        /// <summary>
        /// Returns true if the <see cref="IReadOnlyBasicProperties.Priority"/> property is present.
        /// </summary>
        bool IsPriorityPresent();

        /// <summary>
        /// Returns true if the <see cref="IReadOnlyBasicProperties.ReplyTo"/> property is present.
        /// </summary>
        bool IsReplyToPresent();

        /// <summary>
        /// Returns true if the <see cref="IReadOnlyBasicProperties.Timestamp"/> property is present.
        /// </summary>
        bool IsTimestampPresent();

        /// <summary>
        /// Returns true if the <see cref="IReadOnlyBasicProperties.Type"/> property is present.
        /// </summary>
        bool IsTypePresent();

        /// <summary>
        /// Returns true if the <see cref="IReadOnlyBasicProperties.UserId"/> property is present.
        /// </summary>
        bool IsUserIdPresent();
    }

    /// <summary>
    /// The AMQP Basic headers class interface,
    /// spanning the union of the functionality offered by versions
    /// 0-8, 0-8qpid, 0-9 and 0-9-1 of AMQP.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each property is readable, writable and clearable: a cleared
    /// property will not be transmitted over the wire. Properties on a
    /// fresh instance are clear by default.
    /// </para>
    /// </remarks>
    public interface IBasicProperties : IReadOnlyBasicProperties
    {
        /// <summary>
        /// Application Id.
        /// </summary>
        new string? AppId { get; set; }

        /// <summary>
        /// Intra-cluster routing identifier (cluster id is deprecated in AMQP 0-9-1).
        /// </summary>
        new string? ClusterId { get; set; }

        /// <summary>
        /// MIME content encoding.
        /// </summary>
        new string? ContentEncoding { get; set; }

        /// <summary>
        /// MIME content type.
        /// </summary>
        new string? ContentType { get; set; }

        /// <summary>
        /// Application correlation identifier.
        /// </summary>
        new string? CorrelationId { get; set; }

        /// <summary>
        /// Non-persistent (1) or persistent (2).
        /// </summary>
        new DeliveryModes DeliveryMode { get; set; }

        /// <summary>
        /// Message expiration specification.
        /// </summary>
        new string? Expiration { get; set; }

        /// <summary>
        /// Message header field table. Is of type <see cref="IDictionary{TKey,TValue}" />.
        /// </summary>
        new IDictionary<string, object?>? Headers { get; set; }

        /// <summary>
        /// Application message Id.
        /// </summary>
        new string? MessageId { get; set; }

        /// <summary>
        /// Sets <see cref="DeliveryMode"/> to either persistent (2) or non-persistent (1).
        /// </summary>
        new bool Persistent { get; set; }

        /// <summary>
        /// Message priority, 0 to 9.
        /// </summary>
        new byte Priority { get; set; }

        /// <summary>
        /// Destination to reply to.
        /// </summary>
        new string? ReplyTo { get; set; }

        /// <summary>
        /// Convenience property; parses <see cref="ReplyTo"/> property using <see cref="PublicationAddress.TryParse"/>,
        /// and serializes it using <see cref="PublicationAddress.ToString"/>.
        /// Returns null if <see cref="ReplyTo"/> property cannot be parsed by <see cref="PublicationAddress.TryParse"/>.
        /// </summary>
        new PublicationAddress? ReplyToAddress { get; set; }

        /// <summary>
        /// Message timestamp.
        /// </summary>
        new AmqpTimestamp Timestamp { get; set; }

        /// <summary>
        /// Message type name.
        /// </summary>
        new string? Type { get; set; }

        /// <summary>
        /// User Id.
        /// </summary>
        new string? UserId { get; set; }

        /// <summary>
        /// Clear the <see cref="AppId"/> property.
        /// </summary>
        void ClearAppId();

        /// <summary>
        /// Clear the <see cref="ClusterId"/> property (cluster id is deprecated in AMQP 0-9-1).
        /// </summary>
        void ClearClusterId();

        /// <summary>
        /// Clear the <see cref="ContentEncoding"/> property.
        /// </summary>
        void ClearContentEncoding();

        /// <summary>
        /// Clear the <see cref="ContentType"/> property.
        /// </summary>
        void ClearContentType();

        /// <summary>
        /// Clear the <see cref="CorrelationId"/> property.
        /// </summary>
        void ClearCorrelationId();

        /// <summary>
        /// Clear the <see cref="DeliveryMode"/> property.
        /// </summary>
        void ClearDeliveryMode();

        /// <summary>
        /// Clear the <see cref="Expiration"/> property.
        /// </summary>
        void ClearExpiration();

        /// <summary>
        /// Clear the <see cref="Headers"/> property.
        /// </summary>
        void ClearHeaders();

        /// <summary>
        /// Clear the <see cref="MessageId"/> property.
        /// </summary>
        void ClearMessageId();

        /// <summary>
        /// Clear the <see cref="Priority"/> property.
        /// </summary>
        void ClearPriority();

        /// <summary>
        /// Clear the <see cref="ReplyTo"/> property.
        /// </summary>
        void ClearReplyTo();

        /// <summary>
        /// Clear the <see cref="Timestamp"/> property.
        /// </summary>
        void ClearTimestamp();

        /// <summary>
        /// Clear the <see cref="Type"/> property.
        /// </summary>
        void ClearType();

        /// <summary>
        /// Clear the <see cref="UserId"/> property.
        /// </summary>
        void ClearUserId();
    }
}
