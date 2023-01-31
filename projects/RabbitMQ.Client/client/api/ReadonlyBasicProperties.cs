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
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client
{
#nullable enable
    /// <summary>
    /// AMQP specification content header properties for content class "basic"
    /// </summary>
    public readonly struct ReadOnlyBasicProperties : IReadOnlyBasicProperties
    {
        private readonly string? _contentType;
        private readonly string? _contentEncoding;
        private readonly IReadOnlyDictionary<string, object?>? _headers;
        private readonly DeliveryModes _deliveryMode;
        private readonly byte _priority;
        private readonly string? _correlationId;
        private readonly string? _replyTo;
        private readonly string? _expiration;
        private readonly string? _messageId;
        private readonly AmqpTimestamp _timestamp;
        private readonly string? _type;
        private readonly string? _userId;
        private readonly string? _appId;
        private readonly string? _clusterId;

        public string? ContentType => _contentType;
        public string? ContentEncoding => _contentEncoding;
        public IReadOnlyDictionary<string, object?>? Headers => _headers;
        public DeliveryModes DeliveryMode => _deliveryMode;
        public byte Priority => _priority;
        public string? CorrelationId => _correlationId;
        public string? ReplyTo => _replyTo;
        public string? Expiration => _expiration;
        public string? MessageId => _messageId;
        public AmqpTimestamp Timestamp => _timestamp;
        public string? Type => _type;
        public string? UserId => _userId;
        public string? AppId => _appId;
        public string? ClusterId => _clusterId;

        public bool Persistent => DeliveryMode == DeliveryModes.Persistent;

        public PublicationAddress? ReplyToAddress
        {
            get
            {
                PublicationAddress.TryParse(ReplyTo, out PublicationAddress result);
                return result;
            }
        }

        public ReadOnlyBasicProperties(ReadOnlySpan<byte> span)
            : this()
        {
            int offset = 2;
            ref readonly byte bits = ref span[0];
            if (bits.IsBitSet(BasicProperties.ContentTypeBit)) { offset += WireFormatting.ReadShortstr(span.Slice(offset), out _contentType); }
            if (bits.IsBitSet(BasicProperties.ContentEncodingBit)) { offset += WireFormatting.ReadShortstr(span.Slice(offset), out _contentEncoding); }
            if (bits.IsBitSet(BasicProperties.HeaderBit)) { offset += WireFormatting.ReadDictionary(span.Slice(offset), out var tmpDirectory); _headers = tmpDirectory; }
            if (bits.IsBitSet(BasicProperties.DeliveryModeBit)) { _deliveryMode = (DeliveryModes)span[offset++]; }
            if (bits.IsBitSet(BasicProperties.PriorityBit)) { _priority = span[offset++]; }
            if (bits.IsBitSet(BasicProperties.CorrelationIdBit)) { offset += WireFormatting.ReadShortstr(span.Slice(offset), out _correlationId); }
            if (bits.IsBitSet(BasicProperties.ReplyToBit)) { offset += WireFormatting.ReadShortstr(span.Slice(offset), out _replyTo); }
            if (bits.IsBitSet(BasicProperties.ExpirationBit)) { offset += WireFormatting.ReadShortstr(span.Slice(offset), out _expiration); }

            bits = ref span[1];
            if (bits.IsBitSet(BasicProperties.MessageIdBit)) { offset += WireFormatting.ReadShortstr(span.Slice(offset), out _messageId); }
            if (bits.IsBitSet(BasicProperties.TimestampBit)) { offset += WireFormatting.ReadTimestamp(span.Slice(offset), out _timestamp); }
            if (bits.IsBitSet(BasicProperties.TypeBit)) { offset += WireFormatting.ReadShortstr(span.Slice(offset), out _type); }
            if (bits.IsBitSet(BasicProperties.UserIdBit)) { offset += WireFormatting.ReadShortstr(span.Slice(offset), out _userId); }
            if (bits.IsBitSet(BasicProperties.AppIdBit)) { offset += WireFormatting.ReadShortstr(span.Slice(offset), out _appId); }
            if (bits.IsBitSet(BasicProperties.ClusterIdBit)) { WireFormatting.ReadShortstr(span.Slice(offset), out _clusterId); }
        }

        public bool IsContentTypePresent() => ContentType != default;
        public bool IsContentEncodingPresent() => ContentEncoding != default;
        public bool IsHeadersPresent() => Headers != default;
        public bool IsDeliveryModePresent() => DeliveryMode != default;
        public bool IsPriorityPresent() => Priority != default;
        public bool IsCorrelationIdPresent() => CorrelationId != default;
        public bool IsReplyToPresent() => ReplyTo != default;
        public bool IsExpirationPresent() => Expiration != default;
        public bool IsMessageIdPresent() => MessageId != default;
        public bool IsTimestampPresent() => Timestamp != default;
        public bool IsTypePresent() => Type != default;
        public bool IsUserIdPresent() => UserId != default;
        public bool IsAppIdPresent() => AppId != default;
        public bool IsClusterIdPresent() => ClusterId != default;
    }
}
