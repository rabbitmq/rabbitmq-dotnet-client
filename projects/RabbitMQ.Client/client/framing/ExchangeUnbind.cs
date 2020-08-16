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

using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing.Impl
{
    internal sealed class ExchangeUnbind : MethodBase
    {
        public ushort _reserved1;
        public string _destination;
        public string _source;
        public string _routingKey;
        public bool _nowait;
        public IDictionary<string, object> _arguments;

        public ExchangeUnbind()
        {
        }

        public ExchangeUnbind(ushort Reserved1, string Destination, string Source, string RoutingKey, bool Nowait, IDictionary<string, object> Arguments)
        {
            _reserved1 = Reserved1;
            _destination = Destination;
            _source = Source;
            _routingKey = RoutingKey;
            _nowait = Nowait;
            _arguments = Arguments;
        }

        public override ProtocolCommandId ProtocolCommandId => ProtocolCommandId.ExchangeUnbind;
        public override string ProtocolMethodName => "exchange.unbind";
        public override bool HasContent => false;

        public override void ReadArgumentsFrom(ref MethodArgumentReader reader)
        {
            _reserved1 = reader.ReadShort();
            _destination = reader.ReadShortstr();
            _source = reader.ReadShortstr();
            _routingKey = reader.ReadShortstr();
            _nowait = reader.ReadBit();
            _arguments = reader.ReadTable();
        }

        public override void WriteArgumentsTo(ref MethodArgumentWriter writer)
        {
            writer.WriteShort(_reserved1);
            writer.WriteShortstr(_destination);
            writer.WriteShortstr(_source);
            writer.WriteShortstr(_routingKey);
            writer.WriteBit(_nowait);
            writer.EndBits();
            writer.WriteTable(_arguments);
        }

        public override int GetRequiredBufferSize()
        {
            int bufferSize = 2 + 1 + 1 + 1 + 1; // bytes for _reserved1, length of _destination, length of _source, length of _routingKey, bit fields
            bufferSize += Encoding.UTF8.GetByteCount(_destination); // _destination in bytes
            bufferSize += Encoding.UTF8.GetByteCount(_source); // _source in bytes
            bufferSize += Encoding.UTF8.GetByteCount(_routingKey); // _routingKey in bytes
            bufferSize += WireFormatting.GetTableByteCount(_arguments); // _arguments in bytes
            return bufferSize;
        }
    }
}
