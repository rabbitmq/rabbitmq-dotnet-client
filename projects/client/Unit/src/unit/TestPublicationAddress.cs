// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2013 GoPivotal, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
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
//  at http://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;
using System;
using RabbitMQ.Client;

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
        public void TestParseFail()
        {
            Assert.IsNull(PublicationAddress.Parse("not a valid uri"));
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
