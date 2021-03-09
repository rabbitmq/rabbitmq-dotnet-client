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

using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing.Impl
{
    internal sealed class ConnectionStartOk : MethodBase
    {
        public IDictionary<string, object> _clientProperties;
        public string _mechanism;
        public byte[] _response;
        public string _locale;

        public ConnectionStartOk()
        {
        }

        public ConnectionStartOk(IDictionary<string, object> ClientProperties, string Mechanism, byte[] Response, string Locale)
        {
            _clientProperties = ClientProperties;
            _mechanism = Mechanism;
            _response = Response;
            _locale = Locale;
        }

        public ConnectionStartOk(ReadOnlySpan<byte> span)
        {
            int offset = WireFormatting.ReadDictionary(span, 0, out var tmpDictionary);
            _clientProperties = tmpDictionary;
            offset += WireFormatting.ReadShortstr(span, offset, out _mechanism);
            offset += WireFormatting.ReadLongstr(span, offset, out _response);
            WireFormatting.ReadShortstr(span, offset, out _locale);
        }

        public override ProtocolCommandId ProtocolCommandId => ProtocolCommandId.ConnectionStartOk;
        public override string ProtocolMethodName => "connection.start-ok";
        public override bool HasContent => false;

        public override int WriteArgumentsTo(Span<byte> span)
        {
            int offset = WireFormatting.WriteTable(span, 0, _clientProperties);
            offset += WireFormatting.WriteShortstr(span, offset, _mechanism);
            offset += WireFormatting.WriteLongstr(span, offset, _response);
            return offset + WireFormatting.WriteShortstr(span, offset, _locale);
        }

        public override int GetRequiredBufferSize()
        {
            int bufferSize = 1 + 4 + 1; // bytes for length of _mechanism, length of _response, length of _locale
            bufferSize += WireFormatting.GetTableByteCount(_clientProperties); // _clientProperties in bytes
            bufferSize += WireFormatting.GetByteCount(_mechanism); // _mechanism in bytes
            bufferSize += _response.Length; // _response in bytes
            bufferSize += WireFormatting.GetByteCount(_locale); // _locale in bytes
            return bufferSize;
        }
    }
}
