// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2011-2015 Pivotal Software, Inc.
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
//  Copyright (c) 2011-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using NUnit.Framework;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    class TestConnectionFactory 
    {
        [Test]
        public void TestProperties()
        {
            var u  = "username";
            var pw = "password";
            var v  = "vhost";
            var h  = "192.168.0.1";
            var p  = 5674;

            var cf = new ConnectionFactory();
            cf.UserName = u;
            cf.Password = pw;
            cf.VirtualHost = v;
            cf.HostName = h;
            cf.Port = p;

            Assert.AreEqual(cf.UserName, u);
            Assert.AreEqual(cf.Password, pw);
            Assert.AreEqual(cf.VirtualHost, v);
            Assert.AreEqual(cf.HostName, h);
            Assert.AreEqual(cf.Port, p);
        }
    }
}
