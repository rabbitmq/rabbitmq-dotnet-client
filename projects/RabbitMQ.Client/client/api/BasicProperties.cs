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
using System.Collections.Generic;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client
{
#nullable enable
    /// <summary>
    /// AMQP specification content header properties for content class "basic".
    /// </summary>
    public struct BasicProperties : IBasicProperties, IAmqpHeader
    {
        public string? ContentType { get; set; }
        public string? ContentEncoding { get; set; }
        public IDictionary<string, object?>? Headers { get; set; }
        public DeliveryModes DeliveryMode { get; set; }
        public byte Priority { get; set; }
        public string? CorrelationId { get; set; }
        public string? ReplyTo { get; set; }
        public string? Expiration { get; set; }
        public string? MessageId { get; set; }
        public AmqpTimestamp Timestamp { get; set; }
        public string? Type { get; set; }
        public string? UserId { get; set; }
        public string? AppId { get; set; }
        public string? ClusterId { get; set; }

        public bool Persistent
        {
            get
            {
                return DeliveryMode == DeliveryModes.Persistent;
            }

            set
            {
                DeliveryMode = value ? DeliveryModes.Persistent : DeliveryModes.Transient;
            }
        }

        public PublicationAddress? ReplyToAddress
        {
            readonly get
            {
                PublicationAddress.TryParse(ReplyTo, out PublicationAddress result);
                return result;
            }
            set { ReplyTo = value?.ToString(); }
        }

        public BasicProperties(in ReadOnlyBasicProperties input)
        {
            ContentType = input.ContentType;
            ContentEncoding = input.ContentEncoding;
            Headers = input.Headers;
            DeliveryMode = input.DeliveryMode;
            Priority = input.Priority;
            CorrelationId = input.CorrelationId;
            ReplyTo = input.ReplyTo;
            Expiration = input.Expiration;
            MessageId = input.MessageId;
            Timestamp = input.Timestamp;
            Type = input.Type;
            UserId = input.UserId;
            AppId = input.AppId;
            ClusterId = input.ClusterId;
        }

        public void ClearContentType() => ContentType = default;
        public void ClearContentEncoding() => ContentEncoding = default;
        public void ClearHeaders() => Headers = default;
        public void ClearDeliveryMode() => DeliveryMode = default;
        public void ClearPriority() => Priority = default;
        public void ClearCorrelationId() => CorrelationId = default;
        public void ClearReplyTo() => ReplyTo = default;
        public void ClearExpiration() => Expiration = default;
        public void ClearMessageId() => MessageId = default;
        public void ClearTimestamp() => Timestamp = default;
        public void ClearType() => Type = default;
        public void ClearUserId() => UserId = default;
        public void ClearAppId() => AppId = default;
        public void ClearClusterId() => ClusterId = default;

        public readonly bool IsContentTypePresent() => ContentType != default;
        public readonly bool IsContentEncodingPresent() => ContentEncoding != default;
        public readonly bool IsHeadersPresent() => Headers != default;
        public readonly bool IsDeliveryModePresent() => DeliveryMode != default;
        public readonly bool IsPriorityPresent() => Priority != default;
        public readonly bool IsCorrelationIdPresent() => CorrelationId != default;
        public readonly bool IsReplyToPresent() => ReplyTo != default;
        public readonly bool IsExpirationPresent() => Expiration != default;
        public readonly bool IsMessageIdPresent() => MessageId != default;
        public readonly bool IsTimestampPresent() => Timestamp != default;
        public readonly bool IsTypePresent() => Type != default;
        public readonly bool IsUserIdPresent() => UserId != default;
        public readonly bool IsAppIdPresent() => AppId != default;
        public readonly bool IsClusterIdPresent() => ClusterId != default;

        ushort IAmqpHeader.ProtocolClassId => ClassConstants.Basic;

        //----------------------------------
        // First byte
        //----------------------------------
        internal const byte ContentTypeBit = 7;
        internal const byte ContentEncodingBit = 6;
        internal const byte HeaderBit = 5;
        internal const byte DeliveryModeBit = 4;
        internal const byte PriorityBit = 3;
        internal const byte CorrelationIdBit = 2;
        internal const byte ReplyToBit = 1;
        internal const byte ExpirationBit = 0;

        //----------------------------------
        // Second byte
        //----------------------------------
        internal const byte MessageIdBit = 7;
        internal const byte TimestampBit = 6;
        internal const byte TypeBit = 5;
        internal const byte UserIdBit = 4;
        internal const byte AppIdBit = 3;
        internal const byte ClusterIdBit = 2;

        readonly int IAmqpWriteable.WriteTo(Span<byte> span)
        {
            int offset = 2;
            ref byte bitValue = ref span.GetStart();
            bitValue = 0;
            if (IsContentTypePresent())
            {
                bitValue.SetBit(ContentTypeBit);
                offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), ContentType);
            }

            if (IsContentEncodingPresent())
            {
                bitValue.SetBit(ContentEncodingBit);
                offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), ContentEncoding);
            }

            if (IsHeadersPresent())
            {
                bitValue.SetBit(HeaderBit);
                offset += WireFormatting.WriteTable(ref span.GetOffset(offset), Headers);
            }

            if (IsDeliveryModePresent())
            {
                bitValue.SetBit(DeliveryModeBit);
                span.GetOffset(offset++) = (byte)DeliveryMode;
            }

            if (IsPriorityPresent())
            {
                bitValue.SetBit(PriorityBit);
                span.GetOffset(offset++) = Priority;
            }

            if (IsCorrelationIdPresent())
            {
                bitValue.SetBit(CorrelationIdBit);
                offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), CorrelationId);
            }

            if (IsReplyToPresent())
            {
                bitValue.SetBit(ReplyToBit);
                offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), ReplyTo);
            }

            if (IsExpirationPresent())
            {
                bitValue.SetBit(ExpirationBit);
                offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), Expiration);
            }

            bitValue = ref span.GetOffset(1);
            bitValue = 0;
            if (IsMessageIdPresent())
            {
                bitValue.SetBit(MessageIdBit);
                offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), MessageId);
            }

            if (IsTimestampPresent())
            {
                bitValue.SetBit(TimestampBit);
                offset += WireFormatting.WriteTimestamp(ref span.GetOffset(offset), Timestamp);
            }

            if (IsTypePresent())
            {
                bitValue.SetBit(TypeBit);
                offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), Type);
            }

            if (IsUserIdPresent())
            {
                bitValue.SetBit(UserIdBit);
                offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), UserId);
            }

            if (IsAppIdPresent())
            {
                bitValue.SetBit(AppIdBit);
                offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), AppId);
            }

            if (IsClusterIdPresent())
            {
                bitValue.SetBit(ClusterIdBit);
                offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), ClusterId);
            }

            return offset;
        }

        readonly int IAmqpWriteable.GetRequiredBufferSize()
        {
            int bufferSize = 2; // number of presence fields (14) in 2 bytes blocks
            if (IsContentTypePresent()) { bufferSize += 1 + WireFormatting.GetByteCount(ContentType); } // _contentType in bytes
            if (IsContentEncodingPresent()) { bufferSize += 1 + WireFormatting.GetByteCount(ContentEncoding); } // _contentEncoding in bytes
            if (IsHeadersPresent()) { bufferSize += WireFormatting.GetTableByteCount(Headers); } // _headers in bytes
            if (IsDeliveryModePresent()) { bufferSize++; } // _deliveryMode in bytes
            if (IsPriorityPresent()) { bufferSize++; } // _priority in bytes
            if (IsCorrelationIdPresent()) { bufferSize += 1 + WireFormatting.GetByteCount(CorrelationId); } // _correlationId in bytes
            if (IsReplyToPresent()) { bufferSize += 1 + WireFormatting.GetByteCount(ReplyTo); } // _replyTo in bytes
            if (IsExpirationPresent()) { bufferSize += 1 + WireFormatting.GetByteCount(Expiration); } // _expiration in bytes
            if (IsMessageIdPresent()) { bufferSize += 1 + WireFormatting.GetByteCount(MessageId); } // _messageId in bytes
            if (IsTimestampPresent()) { bufferSize += 8; } // _timestamp in bytes
            if (IsTypePresent()) { bufferSize += 1 + WireFormatting.GetByteCount(Type); } // _type in bytes
            if (IsUserIdPresent()) { bufferSize += 1 + WireFormatting.GetByteCount(UserId); } // _userId in bytes
            if (IsAppIdPresent()) { bufferSize += 1 + WireFormatting.GetByteCount(AppId); } // _appId in bytes
            if (IsClusterIdPresent()) { bufferSize += 1 + WireFormatting.GetByteCount(ClusterId); } // _clusterId in bytes
            return bufferSize;
        }
    }
}
