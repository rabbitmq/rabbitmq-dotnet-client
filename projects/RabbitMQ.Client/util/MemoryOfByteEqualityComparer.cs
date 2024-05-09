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
using System.Runtime.InteropServices;

namespace RabbitMQ.Util
{
    internal sealed class MemoryOfByteEqualityComparer : IEqualityComparer<ReadOnlyMemory<byte>>
    {
        public static MemoryOfByteEqualityComparer Instance { get; } = new MemoryOfByteEqualityComparer();

        public bool Equals(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right)
        {
            return left.Span.SequenceEqual(right.Span);
        }

        public int GetHashCode(ReadOnlyMemory<byte> value)
        {
#if NETSTANDARD
                unchecked
                {
                    int hashCode = 0;
                    var longPart = MemoryMarshal.Cast<byte, long>(value.Span);
                    foreach (long item in longPart)
                    {
                        hashCode = (hashCode * 397) ^ item.GetHashCode();
                    }

                    foreach (int item in value.Span.Slice(longPart.Length * 8))
                    {
                        hashCode = (hashCode * 397) ^ item.GetHashCode();
                    }

                    return hashCode;
                }
#else
            HashCode result = default;
            result.AddBytes(value.Span);
            return result.ToHashCode();
#endif
        }
    }
}
