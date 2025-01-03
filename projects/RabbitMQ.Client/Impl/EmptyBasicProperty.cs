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
using System.Collections.Generic;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Impl
{
    internal sealed class EmptyBasicProperty : IReadOnlyBasicProperties, IAmqpHeader
    {
        internal static EmptyBasicProperty Empty => new EmptyBasicProperty();

        ushort IAmqpHeader.ProtocolClassId => ClassConstants.Basic;

        int IAmqpWriteable.WriteTo(Span<byte> span)
        {
            return WireFormatting.WriteShort(ref span.GetStart(), 0);
        }

        int IAmqpWriteable.GetRequiredBufferSize()
        {
            return 2; // number of presence fields (14) in 2 bytes blocks
        }

        public string? AppId => default;
        public string? ClusterId => default;
        public string? ContentEncoding => default;
        public string? ContentType => default;
        public string? CorrelationId => default;
        public DeliveryModes DeliveryMode => default;
        public string? Expiration => default;
        public IDictionary<string, object?>? Headers => default;
        public string? MessageId => default;
        public bool Persistent => default;
        public byte Priority => default;
        public string? ReplyTo => default;
        public PublicationAddress? ReplyToAddress => default;
        public AmqpTimestamp Timestamp => default;
        public string? Type => default;
        public string? UserId => default;

        public bool IsAppIdPresent() => false;
        public bool IsClusterIdPresent() => false;
        public bool IsContentEncodingPresent() => false;
        public bool IsContentTypePresent() => false;
        public bool IsCorrelationIdPresent() => false;
        public bool IsDeliveryModePresent() => false;
        public bool IsExpirationPresent() => false;
        public bool IsHeadersPresent() => false;
        public bool IsMessageIdPresent() => false;
        public bool IsPriorityPresent() => false;
        public bool IsReplyToPresent() => false;
        public bool IsTimestampPresent() => false;
        public bool IsTypePresent() => false;
        public bool IsUserIdPresent() => false;
    }
}
