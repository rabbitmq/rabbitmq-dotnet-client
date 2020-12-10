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
    internal sealed class QueueDeclareOk : Client.Impl.MethodBase
    {
        public string _queue;
        public uint _messageCount;
        public uint _consumerCount;

        public QueueDeclareOk()
        {
        }

        public QueueDeclareOk(string Queue, uint MessageCount, uint ConsumerCount)
        {
            _queue = Queue;
            _messageCount = MessageCount;
            _consumerCount = ConsumerCount;
        }

        public QueueDeclareOk(ReadOnlySpan<byte> span)
        {
            int offset = WireFormatting.ReadShortstr(span, out _queue);
            offset += WireFormatting.ReadLong(span.Slice(offset), out _messageCount);
            WireFormatting.ReadLong(span.Slice(offset), out _consumerCount);
        }

        public override ProtocolCommandId ProtocolCommandId => ProtocolCommandId.QueueDeclareOk;
        public override string ProtocolMethodName => "queue.declare-ok";
        public override bool HasContent => false;

        public override int WriteArgumentsTo(Span<byte> span)
        {
            int offset = WireFormatting.WriteShortstr(span, _queue);
            offset += WireFormatting.WriteLong(span.Slice(offset), _messageCount);
            return offset + WireFormatting.WriteLong(span.Slice(offset), _consumerCount);
        }

        public override int GetRequiredBufferSize()
        {
            int bufferSize = 1 + 4 + 4; // bytes for length of _queue, _messageCount, _consumerCount
            bufferSize += WireFormatting.GetByteCount(_queue); // _queue in bytes
            return bufferSize;
        }
    }
}
