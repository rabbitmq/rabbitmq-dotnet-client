// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at https://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Text;

using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    abstract class ContentHeaderBase : IContentHeader
    {
        ///<summary>
        /// Retrieve the AMQP class ID of this content header.
        ///</summary>
        public abstract ushort ProtocolClassId { get; }

        ///<summary>
        /// Retrieve the AMQP class name of this content header.
        ///</summary>
        public abstract string ProtocolClassName { get; }

        public virtual object Clone()
        {
            throw new NotImplementedException();
        }

        public abstract void AppendPropertyDebugStringTo(StringBuilder stringBuilder);

        ///<summary>
        /// Fill this instance from the given byte buffer stream.
        ///</summary>
        internal ulong ReadFrom(ReadOnlySpan<byte> span)
        {
            // Skipping the first two bytes since they arent used (weight - not currently used)
            ulong bodySize = NetworkOrderDeserializer.ReadUInt64(span.Slice(2));
            ContentHeaderPropertyReader reader = new ContentHeaderPropertyReader(span.Slice(10));
            ReadPropertiesFrom(ref reader);
            return bodySize;
        }

        internal abstract void ReadPropertiesFrom(ref ContentHeaderPropertyReader reader);
        internal abstract void WritePropertiesTo(ref ContentHeaderPropertyWriter writer);

        private const ushort ZERO = 0;

        internal int WriteTo(Span<byte> span, ulong bodySize)
        {
            NetworkOrderSerializer.WriteUInt16(span, ZERO); // Weight - not used
            NetworkOrderSerializer.WriteUInt64(span.Slice(2), bodySize);

            ContentHeaderPropertyWriter writer = new ContentHeaderPropertyWriter(span.Slice(10));
            WritePropertiesTo(ref writer);
            return 10 + writer.Offset;
        }
        public int GetRequiredBufferSize()
        {
            // The first 10 bytes are the Weight (2 bytes) + body size (8 bytes)
            return 10 + GetRequiredPayloadBufferSize();
        }

        public abstract int GetRequiredPayloadBufferSize();
    }
}
