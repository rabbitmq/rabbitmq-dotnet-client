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
//  Copyright (c) 2007-2013 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;

using System;
using System.IO;
using System.Text;
using System.Collections;

using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestConnectionClose : IntegrationFixture
    {

        [Test]
        public void TestConnectionCloseWithSingleInvocation()
        {
            ConnectionFactory connFactory = new ConnectionFactory();
            IConnection conn = connFactory.CreateConnection();

            conn.Close();
            Assert.IsFalse(conn.IsOpen);
        }

        [Test]
        public void TestConnectionCloseWithMultipleInvocations()
        {
            ConnectionFactory connFactory = new ConnectionFactory();
            IConnection conn = connFactory.CreateConnection();

            conn.Close();
            Assert.IsFalse(conn.IsOpen);
            Assert.Throws(Is.InstanceOf<AlreadyClosedException>(),
                          delegate
                          {
                              conn.Close();
                          });
            Assert.Throws(Is.InstanceOf<AlreadyClosedException>(),
                          delegate
                          {
                              conn.Close(200, "Goodbye");
                          });
            Assert.Throws(Is.InstanceOf<AlreadyClosedException>(),
                          delegate
                          {
                              conn.Close(500);
                          });
            Assert.Throws(Is.InstanceOf<AlreadyClosedException>(),
                          delegate
                          {
                              conn.Close(200, "Goodbye", 500);
                          });
            Assert.IsFalse(conn.IsOpen);
        }


        [Test]
        public void TestConnectionAbortWithSingleInvocation()
        {
            ConnectionFactory connFactory = new ConnectionFactory();
            IConnection conn = connFactory.CreateConnection();

            conn.Abort();
            Assert.IsFalse(conn.IsOpen);
        }

        [Test]
        public void TestConnectionAbortWithMultipleInvocations()
        {
            ConnectionFactory connFactory = new ConnectionFactory();
            IConnection conn = connFactory.CreateConnection();

            conn.Abort();
            Assert.IsFalse(conn.IsOpen);
            conn.Abort();
            conn.Abort(200, "Goodbye");
            conn.Abort(500);
            conn.Abort(200, "Goodbye", 500);
            Assert.IsFalse(conn.IsOpen);
        }
    }
}

