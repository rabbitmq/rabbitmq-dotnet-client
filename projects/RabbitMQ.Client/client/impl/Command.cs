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
using System.Buffers;
using System.Runtime.InteropServices;

using RabbitMQ.Client.Exceptions;

namespace RabbitMQ.Client.Impl
{
    internal class Command : IDisposable
    {
        /*
        Frame layout
        +----------------------------------------------------------------------------+
        |                |               |                    |                      |
        | Frame type (1) | Channel # (2) | Payload length (4) | Frame-end marker (1) |
        |                |               |                    |                      |
        +----------------------------------------------------------------------------+
        */
        internal const int EmptyFrameSize = 8;
        private readonly bool _returnBufferOnDispose;

        static Command() => CheckEmptyFrameSize();

        internal Command(MethodBase method) : this(method, null, null, false)
        {
        }

        public Command(MethodBase method, ContentHeaderBase header, ReadOnlyMemory<byte> body, bool returnBufferOnDispose)
        {
            Method = method;
            Header = header;
            Body = body;
            _returnBufferOnDispose = returnBufferOnDispose;
        }

        public ReadOnlyMemory<byte> Body { get; private set; }

        internal ContentHeaderBase Header { get; private set; }

        internal MethodBase Method { get; private set; }

        public static void CheckEmptyFrameSize()
        {
            var f = new EmptyOutboundFrame();
            long actualLength = f.GetMinimumBufferSize();

            if (EmptyFrameSize != actualLength)
            {
                string message = $"EmptyFrameSize is incorrect - defined as {EmptyFrameSize} where the computed value is in fact {actualLength}.";
                throw new ProtocolViolationException(message);
            }
        }

        public void Dispose()
        {
            if (_returnBufferOnDispose && MemoryMarshal.TryGetArray(Body, out ArraySegment<byte> segment))
            {
                ArrayPool<byte>.Shared.Return(segment.Array);
            }
        }

        public override string ToString() => $"{Method.ProtocolMethodName}";
    }
}
