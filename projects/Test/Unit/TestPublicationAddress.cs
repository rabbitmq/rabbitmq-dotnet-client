// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using RabbitMQ.Client;
using Xunit;

namespace Test.Unit
{
    public class TestPublicationAddress
    {
        [Fact]
        public void TestParseOk()
        {
            string uriLike = "fanout://name/key";
            PublicationAddress addr = PublicationAddress.Parse(uriLike);
            Assert.Equal(ExchangeType.Fanout, addr.ExchangeType);
            Assert.Equal("name", addr.ExchangeName);
            Assert.Equal("key", addr.RoutingKey);
            Assert.Equal(uriLike, addr.ToString());
        }

        [Fact]
        public void TestParseFailWithANE()
        {
            Assert.Throws<ArgumentNullException>(() => PublicationAddress.Parse(null));
        }

        [Fact]
        public void TestParseFailWithUnparseableInput()
        {
            Assert.Null(PublicationAddress.Parse("not a valid URI"));
        }

        [Fact]
        public void TestTryParseFail()
        {
            PublicationAddress.TryParse(null, out PublicationAddress result);
            Assert.Null(result);

            PublicationAddress.TryParse("not a valid URI", out result);
            Assert.Null(result);

            PublicationAddress.TryParse("}}}}}}}}", out result);
            Assert.Null(result);
        }

        [Fact]
        public void TestEmptyExchangeName()
        {
            string uriLike = "direct:///key";
            PublicationAddress addr = PublicationAddress.Parse(uriLike);
            Assert.Equal(ExchangeType.Direct, addr.ExchangeType);
            Assert.Equal("", addr.ExchangeName);
            Assert.Equal("key", addr.RoutingKey);
            Assert.Equal(uriLike, addr.ToString());
        }

        [Fact]
        public void TestEmptyRoutingKey()
        {
            string uriLike = "direct://exch/";
            PublicationAddress addr = PublicationAddress.Parse(uriLike);
            Assert.Equal(ExchangeType.Direct, addr.ExchangeType);
            Assert.Equal("exch", addr.ExchangeName);
            Assert.Equal("", addr.RoutingKey);
            Assert.Equal(uriLike, addr.ToString());
        }

        [Fact]
        public void TestExchangeTypeValidation()
        {
            string uriLike = "direct:///";
            PublicationAddress addr = PublicationAddress.Parse(uriLike);
            int found = 0;
            foreach (string exchangeType in ExchangeType.All())
            {
                if (exchangeType.Equals(addr.ExchangeType))
                {
                    found++;
                }
            }
            Assert.Equal(1, found);
        }

        [Fact]
        public void TestMissingExchangeType()
        {
            Assert.Null(PublicationAddress.Parse("://exch/key"));
        }
    }
}
