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

using NUnit.Framework;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestPublicationAddress
    {
        [Test]
        public void TestParseOk()
        {
            string uriLike = "fanout://name/key";
            PublicationAddress addr = PublicationAddress.Parse(uriLike);
            Assert.AreEqual(ExchangeType.Fanout, addr.ExchangeType);
            Assert.AreEqual("name", addr.ExchangeName);
            Assert.AreEqual("key", addr.RoutingKey);
            Assert.AreEqual(uriLike, addr.ToString());
        }

        [Test]
        public void TestParseFailWithANE()
        {
            Assert.That(()=> PublicationAddress.Parse(null), Throws.ArgumentNullException);
        }

        [Test]
        public void TestParseFailWithUnparseableInput()
        {
            Assert.IsNull(PublicationAddress.Parse("not a valid URI"));
        }

        [Test]
        public void TestTryParseFail()
        {
            PublicationAddress.TryParse(null, out PublicationAddress result);
            Assert.IsNull(result);

            PublicationAddress.TryParse("not a valid URI", out result);
            Assert.IsNull(result);

            PublicationAddress.TryParse("}}}}}}}}", out result);
            Assert.IsNull(result);
        }

        [Test]
        public void TestEmptyExchangeName()
        {
            string uriLike = "direct:///key";
            PublicationAddress addr = PublicationAddress.Parse(uriLike);
            Assert.AreEqual(ExchangeType.Direct, addr.ExchangeType);
            Assert.AreEqual("", addr.ExchangeName);
            Assert.AreEqual("key", addr.RoutingKey);
            Assert.AreEqual(uriLike, addr.ToString());
        }

        [Test]
        public void TestEmptyRoutingKey()
        {
            string uriLike = "direct://exch/";
            PublicationAddress addr = PublicationAddress.Parse(uriLike);
            Assert.AreEqual(ExchangeType.Direct, addr.ExchangeType);
            Assert.AreEqual("exch", addr.ExchangeName);
            Assert.AreEqual("", addr.RoutingKey);
            Assert.AreEqual(uriLike, addr.ToString());
        }

        [Test]
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
            Assert.AreEqual(1, found);
        }

        [Test]
        public void TestMissingExchangeType()
        {
            Assert.IsNull(PublicationAddress.Parse("://exch/key"));
        }
    }
}
