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

using System.Runtime.InteropServices;
using System.Text;

namespace RabbitMQ.Client.Impl
{
    internal static partial class WireFormatting
    {
        public static readonly object TrueBoolean = true;
        public static readonly object FalseBoolean = false;

        // * DESCRIPTION TAKEN FROM MS REFERENCE SOURCE *
        // https://github.com/microsoft/referencesource/blob/master/mscorlib/system/decimal.cs
        // The lo, mid, hi, and flags fields contain the representation of the
        // Decimal value. The lo, mid, and hi fields contain the 96-bit integer
        // part of the Decimal. Bits 0-15 (the lower word) of the flags field are
        // unused and must be zero; bits 16-23 contain must contain a value between
        // 0 and 28, indicating the power of 10 to divide the 96-bit integer part
        // by to produce the Decimal value; bits 24-30 are unused and must be zero;
        // and finally bit 31 indicates the sign of the Decimal value, 0 meaning
        // positive and 1 meaning negative.
        // [StructLayout(LayoutKind.Explicit)] overlays Value with the four component
        // fields, replacing Unsafe.As reinterpret casts in ReadDecimal/WriteDecimal
        // with type-safe field access. The CLR validates the layout at JIT time.
        // The same pattern was applied to LastTimedOutCommandIds in PR #1939.
        [StructLayout(LayoutKind.Explicit)]
        private struct DecimalData
        {
            [FieldOffset(0)]
            public decimal Value;
            [FieldOffset(0)]
            public uint Flags;
            [FieldOffset(4)]
            public uint Hi;
            [FieldOffset(8)]
            public uint Lo;
            [FieldOffset(12)]
            public uint Mid;
        }

        private static readonly UTF8Encoding UTF8 = new UTF8Encoding();
    }
}
