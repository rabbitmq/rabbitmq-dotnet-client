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
//  Copyright (c) 2011-2020 VMware, Inc. or its affiliates.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using RabbitMQ.Client;
using Xunit;

namespace Test.Unit
{
    public class TestAmqpString
    {
        [Theory]
        [InlineData("exchange-ABC:123.abc_456")]
        [InlineData("6PiY80XbjBKnY39R947i2s03cAg261412IS1FzS4uEoJJ6cWZ50P0SJ3S4yqvzx0n4TN4NsROlWyEwaUG4I5Glrj1mI2N28QGbkf5t8Kyo7EavaqME5TrvhPxtJGY1p")]
        [InlineData("foo_bar_baz")]
        public void TestValidExchangeNames(string arg)
        {
            var e = new ExchangeName(arg);
            Assert.Equal(e, arg);
        }

        [Theory]
        [InlineData("exchange-Евгений")]
        [InlineData("6PiY80XbjBK9nY39R947i2s03cAg261412IS1FzS4uEoJJ6cWZ50P0SJ3S4yqvzx0n4TN4NsROlWyEwaUG4I5Glrj1mI2N28QGbkf5t8Kyo7EavaqME5TrvhPxtJGY1p")]
        [InlineData("foo/bar%baz")]
        public void TestInvalidExchangeNameThrows(string arg)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExchangeName(arg));
        }

        [Theory]
        [InlineData("queue-ABC:123.abc_456")]
        [InlineData("6PiY80XbjBKnY39R947i2s03cAg261412IS1FzS4uEoJJ6cWZ50P0SJ3S4yqvzx0n4TN4NsROlWyEwaUG4I5Glrj1mI2N28QGbkf5t8Kyo7EavaqME5TrvhPxtJGY1p")]
        [InlineData("foo_bar_baz")]
        public void TestValidQueueNames(string arg)
        {
            var e = new QueueName(arg);
            Assert.Equal(e, arg);
        }

        [Theory]
        [InlineData("exchange-Евгений")]
        [InlineData("6PiY80XbjBK9nY39R947i2s03cAg261412IS1FzS4uEoJJ6cWZ50P0SJ3S4yqvzx0n4TN4NsROlWyEwaUG4I5Glrj1mI2N28QGbkf5t8Kyo7EavaqME5TrvhPxtJGY1p")]
        [InlineData("foo/bar%baz")]
        public void TestInvalidQueueNameThrows(string arg)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new QueueName(arg));
        }

        [Theory]
        [InlineData("$key-ABC:123.abc_456*blargh///~~~?")]
        [InlineData("")]
        public void TestValidRoutingKeys(string arg)
        {
            var e = new RoutingKey(arg);
            Assert.Equal(e, arg);
        }

        [Theory]
        [InlineData("key-Евгений")]
        [InlineData("O0s5Vu04DVyVSMp8twUjsxa0gjcQ9ixV43U10VDVS6wPTQr14mC2x8cK6fl1G0INoJlCs4v7291iOMlBLj171F09a3VBx4sFc7SKaF683YeG9nn5IQAhQZxDE6grPr20EmHPZpMy3TO3R5aW976WTNVPDzZC5vprruJgaBOh8P9ZKRwc77v2nXUnDcz1t2rX84V8l5deX6V3l0Fv4S0bQzSbGdpD5jiPSZ44yBsZVXtv61saCgu0FNGoFSuV9207s6PiY80XbjBK9nY39R947i2s03cAg261412IS1FzS4uEoJJ6cWZ50P0SJ3S4yqvzx0n4TN4NsROlWyEwaUG4I5Glrj1mI2N28QGbkf5t8Kyo7EavaqME5TrvhPxtJGY1p")]
        public void TestInvalidRoutingKeyThrows(string arg)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RoutingKey(arg));
        }
    }
}
