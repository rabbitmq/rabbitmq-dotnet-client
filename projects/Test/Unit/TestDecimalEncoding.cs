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


using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Impl;
using Xunit;

namespace Test.Unit
{
    public class TestDecimalEncoding
    {
        [Theory]
        [InlineData("0", "00-00-00-00-00")]
        [InlineData("12.34", "02-00-00-04-D2")]
        [InlineData("-12.34", "02-80-00-04-D2")]
        [InlineData("2147483647", "00-7F-FF-FF-FF")]
        [InlineData("-2147483647", "00-FF-FF-FF-FF")]
        [InlineData("0.2147483647", "0A-7F-FF-FF-FF")]
        public void ADecimalWithA31BitMantissaRoundTrips_GH2179(string text, string wire)
        {
            decimal value = Parse(text);
            // Dirty, as a pooled buffer would be, so a write that skips a byte shows.
            byte[] bytes = { 0xAA, 0xAA, 0xAA, 0xAA, 0xAA };

            Assert.Equal(5, WireFormatting.WriteDecimal(ref bytes[0], value));
            Assert.Equal(wire, System.BitConverter.ToString(bytes));
            Assert.Equal(value, WireFormatting.ReadDecimal(bytes));
        }

        [Theory]
        [InlineData("2147483648")]
        [InlineData("-2147483648")]
        [InlineData("2147483649")]
        [InlineData("4294967295")]
        [InlineData("-2147483649")]
        [InlineData("0.2147483649")]
        [InlineData("4294967296")]
        [InlineData("-4294967296")]
        [InlineData("79228162514264337593543950335")]
        public void ADecimalWhoseMantissaNeeds32BitsOrMoreIsRejected_GH2179(string text)
        {
            decimal value = Parse(text);
            byte[] bytes = new byte[5];

            Assert.Throws<WireFormattingException>(() => WireFormatting.WriteDecimal(ref bytes[0], value));
            // Sizing rejects it too, before anything is rented for the frame.
            Assert.Throws<WireFormattingException>(() => WireFormatting.GetFieldValueByteCount(value));
        }

        private static decimal Parse(string text)
            => decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
    }
}
