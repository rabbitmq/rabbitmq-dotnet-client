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
    internal sealed class ConnectionStart : MethodBase
    {
        public byte _versionMajor;
        public byte _versionMinor;
        public Dictionary<string, object> _serverProperties;
        public byte[] _mechanisms;
        public byte[] _locales;

        public ConnectionStart()
        {
        }

        public ConnectionStart(byte VersionMajor, byte VersionMinor, Dictionary<string, object> ServerProperties, byte[] Mechanisms, byte[] Locales)
        {
            _versionMajor = VersionMajor;
            _versionMinor = VersionMinor;
            _serverProperties = ServerProperties;
            _mechanisms = Mechanisms;
            _locales = Locales;
        }

        public ConnectionStart(ReadOnlySpan<byte> span)
        {
            _versionMajor = span[0];
            _versionMinor = span[1];
            int offset = 2 + WireFormatting.ReadDictionary(span, 2, out _serverProperties);
            offset += WireFormatting.ReadLongstr(span, offset, out _mechanisms);
            WireFormatting.ReadLongstr(span, offset, out _locales);
        }

        public override ProtocolCommandId ProtocolCommandId => ProtocolCommandId.ConnectionStart;
        public override string ProtocolMethodName => "connection.start";
        public override bool HasContent => false;

        public override int WriteArgumentsTo(Span<byte> span)
        {
            span[0] = _versionMajor;
            span[1] = _versionMinor;
            int offset = 2 + WireFormatting.WriteTable(span, 2, (IDictionary<string, object>)_serverProperties);
            offset += WireFormatting.WriteLongstr(span, offset, _mechanisms);
            return offset + WireFormatting.WriteLongstr(span, offset, _locales);
        }

        public override int GetRequiredBufferSize()
        {
            int bufferSize = 1 + 1 + 4 + 4; // bytes for _versionMajor, _versionMinor, length of _mechanisms, length of _locales
            bufferSize += WireFormatting.GetTableByteCount((IDictionary<string, object>)_serverProperties); // _serverProperties in bytes
            bufferSize += _mechanisms.Length; // _mechanisms in bytes
            bufferSize += _locales.Length; // _locales in bytes
            return bufferSize;
        }
    }
}
