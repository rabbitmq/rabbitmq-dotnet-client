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
using System.Text;
using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing.Impl
{
    internal sealed class ConnectionUpdateSecret : Client.Impl.MethodBase
    {
        public byte[] _newSecret;
        public string _reason;

        public ConnectionUpdateSecret()
        {
        }

        public ConnectionUpdateSecret(byte[] NewSecret, string Reason)
        {
            _newSecret = NewSecret;
            _reason = Reason;
        }

        public override ProtocolCommandId ProtocolCommandId => ProtocolCommandId.ConnectionUpdateSecret;
        public override string ProtocolMethodName => "connection.update-secret";
        public override bool HasContent => false;

        public override void ReadArgumentsFrom(ref Client.Impl.MethodArgumentReader reader)
        {
            _newSecret = reader.ReadLongstr();
            _reason = reader.ReadShortstr();
        }

        public override void WriteArgumentsTo(ref Client.Impl.MethodArgumentWriter writer)
        {
            writer.WriteLongstr(_newSecret);
            writer.WriteShortstr(_reason);
        }

        public override int WriteArgumentsTo(Span<byte> span)
        {
            int offset = WireFormatting.WriteLongstr(span, _newSecret);
            return offset + WireFormatting.WriteShortstr(span.Slice(offset), _reason);
        }

        public override int GetRequiredBufferSize()
        {
            int bufferSize = 4 + 1; // bytes for length of _newSecret, length of _reason
            bufferSize += _newSecret.Length; // _newSecret in bytes
            bufferSize += Encoding.UTF8.GetByteCount(_reason); // _reason in bytes
            return bufferSize;
        }
    }
}
