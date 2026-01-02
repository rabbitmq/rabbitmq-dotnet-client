// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RabbitMQ.Client
{
    internal static class TypeExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte GetStart(this ReadOnlySpan<byte> span)
        {
            return ref MemoryMarshal.GetReference(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte GetStart(this byte[] array)
        {
            return ref Unsafe.AsRef(in array[0]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte GetStart(this Span<byte> span)
        {
            return ref MemoryMarshal.GetReference(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte GetOffset(this Span<byte> span, int offset)
        {
            return ref span.GetStart().GetOffset(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte GetOffset(this ref byte source, int offset)
        {
            return ref Unsafe.Add(ref source, offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBitSet(this in byte value, byte bitPosition)
        {
            return (value & (1 << bitPosition)) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBit(this ref byte value, byte bitPosition)
        {
            value |= (byte)(1 << bitPosition);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ToByte(this ref bool source)
        {
            return Unsafe.As<bool, byte>(ref source);
        }
    }
}
