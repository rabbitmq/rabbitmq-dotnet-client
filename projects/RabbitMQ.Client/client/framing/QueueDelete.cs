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
    internal sealed class QueueDelete : Client.Impl.MethodBase
    {
        public ushort _reserved1;
        public string _queue;
        public bool _ifUnused;
        public bool _ifEmpty;
        public bool _nowait;

        public QueueDelete()
        {
        }

        public QueueDelete(ushort Reserved1, string Queue, bool IfUnused, bool IfEmpty, bool Nowait)
        {
            _reserved1 = Reserved1;
            _queue = Queue;
            _ifUnused = IfUnused;
            _ifEmpty = IfEmpty;
            _nowait = Nowait;
        }

        public override ushort ProtocolClassId => ClassConstants.Queue;
        public override ushort ProtocolMethodId => QueueMethodConstants.Delete;
        public override string ProtocolMethodName => "queue.delete";
        public override bool HasContent => false;

        public override void ReadArgumentsFrom(ref Client.Impl.MethodArgumentReader reader)
        {
            _reserved1 = reader.ReadShort();
            _queue = reader.ReadShortstr();
            _ifUnused = reader.ReadBit();
            _ifEmpty = reader.ReadBit();
            _nowait = reader.ReadBit();
        }

        public override void WriteArgumentsTo(ref Client.Impl.MethodArgumentWriter writer)
        {
            writer.WriteShort(_reserved1);
            writer.WriteShortstr(_queue);
            writer.WriteBit(_ifUnused);
            writer.WriteBit(_ifEmpty);
            writer.WriteBit(_nowait);
            writer.EndBits();
        }

        public override int GetRequiredBufferSize()
        {
            int bufferSize = 2 + 1 + 1; // bytes for _reserved1, length of _queue, bit fields
            bufferSize += Encoding.UTF8.GetByteCount(_queue); // _queue in bytes
            return bufferSize;
        }
    }
}
