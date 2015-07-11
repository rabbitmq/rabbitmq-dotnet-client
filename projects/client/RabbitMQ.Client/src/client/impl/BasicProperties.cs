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

using System;
using System.Collections.Generic;

namespace RabbitMQ.Client.Impl
{
    public abstract class BasicProperties : ContentHeaderBase, IBasicProperties
    {
        /// <summary>
        /// Application Id.
        /// </summary>
        public abstract string AppId { get; set; }

        /// <summary>
        /// Intra-cluster routing identifier (cluster id is deprecated in AMQP 0-9-1).
        /// </summary>
        public abstract string ClusterId { get; set; }

        /// <summary>
        /// MIME content encoding.
        /// </summary>
        public abstract string ContentEncoding { get; set; }

        /// <summary>
        /// MIME content type.
        /// </summary>
        public abstract string ContentType { get; set; }

        /// <summary>
        /// Application correlation identifier.
        /// </summary>
        public abstract string CorrelationId { get; set; }

        /// <summary>
        /// Non-persistent (1) or persistent (2).
        /// </summary>
        public abstract byte DeliveryMode { get; set; }

        /// <summary>
        /// Message expiration specification.
        /// </summary>
        public abstract string Expiration { get; set; }

        /// <summary>
        /// Message header field table. Is of type <see cref="IDictionary{TKey,TValue}" />.
        /// </summary>
        public abstract IDictionary<string, object> Headers { get; set; }

        /// <summary>
        /// Application message Id.
        /// </summary>
        public abstract string MessageId { get; set; }

        /// <summary>
        /// Sets <see cref="DeliveryMode"/> to either persistent (2) or non-persistent (1).
        /// </summary>
        public bool Persistent
        {
            get { return DeliveryMode == 2; }
            set { DeliveryMode = value ? (byte)2 : (byte)1; }
        }

        /// <summary>
        /// Message priority, 0 to 9.
        /// </summary>
        public abstract byte Priority { get; set; }

        /// <summary>
        /// Destination to reply to.
        /// </summary>
        public abstract string ReplyTo { get; set; }

        /// <summary>
        /// Convenience property; parses <see cref="ReplyTo"/> property using <see cref="PublicationAddress.Parse"/>,
        /// and serializes it using <see cref="PublicationAddress.ToString"/>.
        /// Returns null if <see cref="ReplyTo"/> property cannot be parsed by <see cref="PublicationAddress.Parse"/>.
        /// </summary>
        public PublicationAddress ReplyToAddress
        {
            get { return PublicationAddress.Parse(ReplyTo); }
            set { ReplyTo = value.ToString(); }
        }

        /// <summary>
        /// Message timestamp.
        /// </summary>
        public abstract AmqpTimestamp Timestamp { get; set; }

        /// <summary>
        /// Message type name.
        /// </summary>
        public abstract string Type { get; set; }

        /// <summary>
        /// User Id.
        /// </summary>
        public abstract string UserId { get; set; }

        /// <summary>
        /// Clear the <see cref="AppId"/> property.
        /// </summary>
        public abstract void ClearAppId();

        /// <summary>
        /// Clear the <see cref="ClusterId"/> property (cluster id is deprecated in AMQP 0-9-1).
        /// </summary>
        public abstract void ClearClusterId();

        /// <summary>
        /// Clear the <see cref="ContentEncoding"/> property.
        /// </summary>
        public abstract void ClearContentEncoding();

        /// <summary>
        /// Clear the <see cref="ContentType"/> property.
        /// </summary>
        public abstract void ClearContentType();

        /// <summary>
        /// Clear the <see cref="CorrelationId"/> property.
        /// </summary>
        public abstract void ClearCorrelationId();

        /// <summary>
        /// Clear the <see cref="DeliveryMode"/> property.
        /// </summary>
        public abstract void ClearDeliveryMode();

        /// <summary>
        /// Clear the <see cref="Expiration"/> property.
        /// </summary>
        public abstract void ClearExpiration();

        /// <summary>
        /// Clear the <see cref="Headers"/> property.
        /// </summary>
        public abstract void ClearHeaders();

        /// <summary>
        /// Clear the <see cref="MessageId"/> property.
        /// </summary>
        public abstract void ClearMessageId();

        /// <summary>
        /// Clear the <see cref="Priority"/> property.
        /// </summary>
        public abstract void ClearPriority();

        /// <summary>
        /// Clear the <see cref="ReplyTo"/> property.
        /// </summary>
        public abstract void ClearReplyTo();

        /// <summary>
        /// Clear the <see cref="Timestamp"/> property.
        /// </summary>
        public abstract void ClearTimestamp();

        /// <summary>
        /// Clear the Type property.
        /// </summary>
        public abstract void ClearType();

        /// <summary>
        /// Clear the <see cref="UserId"/> property.
        /// </summary>
        public abstract void ClearUserId();

        /// <summary>
        /// Returns true if the <see cref="AppId"/> property is present.
        /// </summary>
        public abstract bool IsAppIdPresent();

        /// <summary>
        /// Returns true if the <see cref="ClusterId"/> property is present (cluster id is deprecated in AMQP 0-9-1).
        /// </summary>
        public abstract bool IsClusterIdPresent();

        /// <summary>
        /// Returns true if the <see cref="ContentEncoding"/> property is present.
        /// </summary>
        public abstract bool IsContentEncodingPresent();

        /// <summary>
        /// Returns true if the <see cref="ContentType"/> property is present.
        /// </summary>
        public abstract bool IsContentTypePresent();

        /// <summary>
        /// Returns true if the <see cref="CorrelationId"/> property is present.
        /// </summary>
        public abstract bool IsCorrelationIdPresent();

        /// <summary>
        /// Returns true if the <see cref="DeliveryMode"/> property is present.
        /// </summary>
        public abstract bool IsDeliveryModePresent();

        /// <summary>
        /// Returns true if the <see cref="Expiration"/> property is present.
        /// </summary>
        public abstract bool IsExpirationPresent();

        /// <summary>
        /// Returns true if the <see cref="Headers"/> property is present.
        /// </summary>
        public abstract bool IsHeadersPresent();

        /// <summary>
        /// Returns true if the <see cref="MessageId"/> property is present.
        /// </summary>
        public abstract bool IsMessageIdPresent();

        /// <summary>
        /// Returns true if the <see cref="Priority"/> property is present.
        /// </summary>
        public abstract bool IsPriorityPresent();

        /// <summary>
        /// Returns true if the <see cref="ReplyTo"/> property is present.
        /// </summary>
        public abstract bool IsReplyToPresent();

        /// <summary>
        /// Returns true if the <see cref="Timestamp"/> property is present.
        /// </summary>
        public abstract bool IsTimestampPresent();

        /// <summary>
        /// Returns true if the Type property is present.
        /// </summary>
        public abstract bool IsTypePresent();

        /// <summary>
        /// Returns true if the <see cref="UserId"/> UserId property is present.
        /// </summary>
        public abstract bool IsUserIdPresent();

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
        [Obsolete("Usage of this setter method is deprecated. Use the Persistent property instead.")]
        public void SetPersistent(bool persistent)
        {
            Persistent = persistent;
        }

        public override object Clone()
        {
            var clone = MemberwiseClone() as BasicProperties;
            if (IsHeadersPresent())
            {
                clone.Headers = new Dictionary<string, object>();
                foreach (KeyValuePair<string, object> entry in Headers)
                {
                    clone.Headers[entry.Key] = entry.Value;
                }
            }

            return clone;
        }
    }
}