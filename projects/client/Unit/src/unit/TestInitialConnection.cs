﻿// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2016 Pivotal Software, Inc.
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
//  at http://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;
using System;
using System.Collections.Generic;
using System.Threading;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestInitialConnection : IntegrationFixture
    {
        [Test]
        public void TestBasicConnectionRecoveryWithHostnameList()
        {
            var c = CreateAutorecoveringConnection(new List<string>() { "127.0.0.1", "localhost" });
            Assert.IsTrue(c.IsOpen);
            c.Close();
        }

        [Test]
        public void TestBasicConnectionRecoveryWithHostnameListAndUnreachableHosts()
        {
            var c = CreateAutorecoveringConnection(new List<string>() { "191.72.44.22", "127.0.0.1", "localhost" });
            Assert.IsTrue(c.IsOpen);
            c.Close();
        }

        [Test]
        public void TestBasicConnectionRecoveryWithHostnameListWithOnlyUnreachableHosts()
        {
            Assert.Throws<BrokerUnreachableException>(delegate {
                CreateAutorecoveringConnection(new List<string>() {
                    "191.72.44.22",
                    "145.23.22.18",
                    "192.255.255.255"
                });
            });
        }
    }
}
