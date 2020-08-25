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
using System.Text;
using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing.Impl
{
    internal sealed class QueueDeclare : MethodBase
    {
        public ushort _reserved1;
        public string _queue;
        public bool _passive;
        public bool _durable;
        public bool _exclusive;
        public bool _autoDelete;
        public bool _nowait;
        public IDictionary<string, object> _arguments;

        public QueueDeclare()
        {
        }

        public QueueDeclare(ushort Reserved1, string Queue, bool Passive, bool Durable, bool Exclusive, bool AutoDelete, bool Nowait, IDictionary<string, object> Arguments)
        {
            _reserved1 = Reserved1;
            _queue = Queue;
            _passive = Passive;
            _durable = Durable;
            _exclusive = Exclusive;
            _autoDelete = AutoDelete;
            _nowait = Nowait;
            _arguments = Arguments;
        }

        public override ProtocolCommandId ProtocolCommandId => ProtocolCommandId.QueueDeclare;
        public override string ProtocolMethodName => "queue.declare";
        public override bool HasContent => false;

        public override void ReadArgumentsFrom(ref MethodArgumentReader reader)
        {
            _reserved1 = reader.ReadShort();
            _queue = reader.ReadShortstr();
            _passive = reader.ReadBit();
            _durable = reader.ReadBit();
            _exclusive = reader.ReadBit();
            _autoDelete = reader.ReadBit();
            _nowait = reader.ReadBit();
            _arguments = reader.ReadTable();
        }

        public override void WriteArgumentsTo(ref MethodArgumentWriter writer)
        {
            writer.WriteShort(_reserved1);
            writer.WriteShortstr(_queue);
            writer.WriteBit(_passive);
            writer.WriteBit(_durable);
            writer.WriteBit(_exclusive);
            writer.WriteBit(_autoDelete);
            writer.WriteBit(_nowait);
            writer.EndBits();
            writer.WriteTable(_arguments);
        }

        public override int WriteArgumentsTo(Span<byte> span)
        {
            int offset = WireFormatting.WriteShort(span, _reserved1);
            offset += WireFormatting.WriteShortstr(span.Slice(offset), _queue);
            offset += WireFormatting.WriteBits(span.Slice(offset), _passive, _durable, _exclusive, _autoDelete, _nowait);
            return offset + WireFormatting.WriteTable(span.Slice(offset), _arguments);
        }

        public override int GetRequiredBufferSize()
        {
            int bufferSize = 2 + 1 + 1; // bytes for _reserved1, length of _queue, bit fields
            bufferSize += Encoding.UTF8.GetByteCount(_queue); // _queue in bytes
            bufferSize += WireFormatting.GetTableByteCount(_arguments); // _arguments in bytes
            return bufferSize;
        }
    }
}
