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

using System.Text;

namespace RabbitMQ.Client.Framing.Impl
{
    internal sealed class BasicGetOk : Client.Impl.MethodBase
    {
        public ulong _deliveryTag;
        public bool _redelivered;
        public string _exchange;
        public string _routingKey;
        public uint _messageCount;

        public BasicGetOk()
        {
        }

        public BasicGetOk(ulong DeliveryTag, bool Redelivered, string Exchange, string RoutingKey, uint MessageCount)
        {
            _deliveryTag = DeliveryTag;
            _redelivered = Redelivered;
            _exchange = Exchange;
            _routingKey = RoutingKey;
            _messageCount = MessageCount;
        }

        public override ushort ProtocolClassId => ClassConstants.Basic;
        public override ushort ProtocolMethodId => BasicMethodConstants.GetOk;
        public override string ProtocolMethodName => "basic.get-ok";
        public override bool HasContent => true;

        public override void ReadArgumentsFrom(ref Client.Impl.MethodArgumentReader reader)
        {
            _deliveryTag = reader.ReadLonglong();
            _redelivered = reader.ReadBit();
            _exchange = reader.ReadShortstr();
            _routingKey = reader.ReadShortstr();
            _messageCount = reader.ReadLong();
        }

        public override void WriteArgumentsTo(ref Client.Impl.MethodArgumentWriter writer)
        {
            writer.WriteLonglong(_deliveryTag);
            writer.WriteBit(_redelivered);
            writer.EndBits();
            writer.WriteShortstr(_exchange);
            writer.WriteShortstr(_routingKey);
            writer.WriteLong(_messageCount);
        }

        public override int GetRequiredBufferSize()
        {
            int bufferSize = 8 + 1 + 1 + 1 + 4; // bytes for _deliveryTag, bit fields, length of _exchange, length of _routingKey, _messageCount
            bufferSize += Encoding.UTF8.GetByteCount(_exchange); // _exchange in bytes
            bufferSize += Encoding.UTF8.GetByteCount(_routingKey); // _routingKey in bytes
            return bufferSize;
        }
    }
}
