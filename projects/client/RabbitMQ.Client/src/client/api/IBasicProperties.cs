// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2014 GoPivotal, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at http://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System.Collections.Generic;

namespace RabbitMQ.Client
{
    /// <summary>Common AMQP Basic content-class headers interface,
    /// spanning the union of the functionality offered by versions
    /// 0-8, 0-8qpid, 0-9 and 0-9-1 of AMQP.</summary>
    /// <remarks>
    /// <para>
    /// The specification code generator provides
    /// protocol-version-specific implementations of this interface. To
    /// obtain an implementation of this interface in a
    /// protocol-version-neutral way, use <see cref="IModel.CreateBasicProperties"/>.
    /// </para>
    /// <para>
    /// Each property is readable, writable and clearable: a cleared
    /// property will not be transmitted over the wire. Properties on a
    /// fresh instance are clear by default.
    /// </para>
    /// </remarks>
    public interface IBasicProperties : IContentHeader
    {
        /// <summary>
        /// Application Id.
        /// </summary>
        string AppId { get; set; }

        /// <summary>
        /// Intra-cluster routing identifier (cluster id is deprecated in AMQP 0-9-1).
        /// </summary>
        string ClusterId { get; set; }

        /// <summary>
        /// MIME content encoding.
        /// </summary>
        string ContentEncoding { get; set; }

        /// <summary>
        /// MIME content type.
        /// </summary>
        string ContentType { get; set; }

        /// <summary>
        /// Application correlation identifier.
        /// </summary>
        string CorrelationId { get; set; }

        /// <summary>
        /// Non-persistent (1) or persistent (2).
        /// </summary>
        byte DeliveryMode { get; set; }

        /// <summary>
        /// Message expiration specification.
        /// </summary>
        string Expiration { get; set; }

        /// <summary>
        /// Message header field table. Is of type <see cref="IDictionary{TKey,TValue}" />.
        /// </summary>
        IDictionary<string, object> Headers { get; set; }

        /// <summary>
        /// Application message Id.
        /// </summary>
        string MessageId { get; set; }

        /// <summary>
        /// Message priority, 0 to 9.
        /// </summary>
        byte Priority { get; set; }

        /// <summary>
        /// Destination to reply to.
        /// </summary>
        string ReplyTo { get; set; }

        /// <summary>
        /// Convenience property; parses <see cref="ReplyTo"/> property using <see cref="PublicationAddress.Parse"/>,
        /// and serializes it using <see cref="PublicationAddress.ToString"/>.
        /// Returns null if <see cref="ReplyTo"/> property cannot be parsed by <see cref="PublicationAddress.Parse"/>.
        /// </summary>
        PublicationAddress ReplyToAddress { get; set; }

        /// <summary>
        /// Message timestamp.
        /// </summary>
        AmqpTimestamp Timestamp { get; set; }

        /// <summary>
        /// Message type name.
        /// </summary>
        string Type { get; set; }

        /// <summary>
        /// User Id.
        /// </summary>
        string UserId { get; set; }

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
        /// Clear the Type property.
        /// </summary>
        void ClearType();

        /// <summary>
        /// Clear the <see cref="UserId"/> property.
        /// </summary>
        void ClearUserId();

        /// <summary>
        /// Returns true if the <see cref="AppId"/> property is present.
        /// </summary>
        bool IsAppIdPresent();

        /// <summary>
        /// Returns true if the <see cref="ClusterId"/> property is present (cluster id is deprecated in AMQP 0-9-1).
        /// </summary>
        bool IsClusterIdPresent();

        /// <summary>
        /// Returns true if the <see cref="ContentEncoding"/> property is present.
        /// </summary>
        bool IsContentEncodingPresent();

        /// <summary>
        /// Returns true if the <see cref="ContentType"/> property is present.
        /// </summary>
        bool IsContentTypePresent();

        /// <summary>
        /// Returns true if the <see cref="CorrelationId"/> property is present.
        /// </summary>
        bool IsCorrelationIdPresent();

        /// <summary>
        /// Returns true if the <see cref="DeliveryMode"/> property is present.
        /// </summary>
        bool IsDeliveryModePresent();

        /// <summary>
        /// Returns true if the <see cref="Expiration"/> property is present.
        /// </summary>
        bool IsExpirationPresent();

        /// <summary>
        /// Returns true if the <see cref="Headers"/> property is present.
        /// </summary>
        bool IsHeadersPresent();

        /// <summary>
        /// Returns true if the <see cref="MessageId"/> property is present.
        /// </summary>
        bool IsMessageIdPresent();

        /// <summary>
        /// Returns true if the <see cref="Priority"/> property is present.
        /// </summary>
        bool IsPriorityPresent();

        /// <summary>
        /// Returns true if the <see cref="ReplyTo"/> property is present.
        /// </summary>
        bool IsReplyToPresent();

        /// <summary>
        /// Returns true if the <see cref="Timestamp"/> property is present.
        /// </summary>
        bool IsTimestampPresent();

        /// <summary>
        /// Returns true if the Type property is present.
        /// </summary>
        bool IsTypePresent();

        /// <summary>
        /// Returns true if the <see cref="UserId"/> UserId property is present.
        /// </summary>
        bool IsUserIdPresent();

        /// <summary>Sets <see cref="DeliveryMode"/> to either persistent (2) or non-persistent (1).</summary>
        /// <remarks>
        /// <para>
        /// The numbers 1 and 2 for delivery mode are "magic" in that
        /// they appear in the AMQP 0-8 and 0-9 specifications as part
        /// of the definition of the DeliveryMode Basic-class property,
        /// without being defined as named constants.
        /// </para>
        /// <para>
        /// Calling this method causes <see cref="DeliveryMode"/> to take on a  value.
        /// In order to reset <see cref="DeliveryMode"/> to the default empty condition, call <see cref="ClearDeliveryMode"/> .
        /// </para>
        /// </remarks>
        void SetPersistent(bool persistent);
    }
}