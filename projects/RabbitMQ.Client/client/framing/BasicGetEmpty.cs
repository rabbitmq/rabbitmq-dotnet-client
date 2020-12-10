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
    internal sealed class BasicGetEmpty : Client.Impl.MethodBase
    {
        public string _reserved1;

        public BasicGetEmpty()
        {
        }

        public BasicGetEmpty(string Reserved1)
        {
            _reserved1 = Reserved1;
        }

        public BasicGetEmpty(ReadOnlySpan<byte> span)
        {
            WireFormatting.ReadShortstr(span, out _reserved1);
        }

        public override ProtocolCommandId ProtocolCommandId => ProtocolCommandId.BasicGetEmpty;
        public override string ProtocolMethodName => "basic.get-empty";
        public override bool HasContent => false;

        public override int WriteArgumentsTo(Span<byte> span)
        {
            return WireFormatting.WriteShortstr(span, _reserved1);
        }

        public override int GetRequiredBufferSize()
        {
            int bufferSize = 1; // bytes for length of _reserved1
            bufferSize += WireFormatting.GetByteCount(_reserved1); // _reserved1 in bytes
            return bufferSize;
        }
    }
}
