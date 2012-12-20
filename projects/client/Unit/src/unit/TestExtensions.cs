// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2012 VMware, Inc.
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
//  The Initial Developer of the Original Code is VMware, Inc.
//  Copyright (c) 2007-2012 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Threading;
using NUnit.Framework;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestExtensions : IntegrationFixture
    {
        [Test]
        public void TestExchangeBinding()
        {
            Model.ConfirmSelect();

            Model.ExchangeDeclare("src", ExchangeType.Direct, false, false, null);
            Model.ExchangeDeclare("dest", ExchangeType.Direct, false, false, null);
            String queue = Model.QueueDeclare();

            Model.ExchangeBind("dest", "src", String.Empty);
            Model.QueueBind(queue, "dest", String.Empty);

            Model.BasicPublish("src", String.Empty, null, new byte[] { });
            Model.WaitForConfirms();
            Assert.IsNotNull(Model.BasicGet(queue, true));

            Model.ExchangeUnbind("dest", "src", String.Empty);
            Model.BasicPublish("src", String.Empty, null, new byte[] { });
            Model.WaitForConfirms();
            Assert.IsNull(Model.BasicGet(queue, true));

            Model.ExchangeDelete("src");
            Model.ExchangeDelete("dest");
        }

        [Test]
        public void TestConfirmBeforeWait()
        {
            Assert.Throws(
                typeof (InvalidOperationException),
                delegate { Model.WaitForConfirms(); });
        }

        [Test]
        public void TestConfirmBarrier()
        {
            Model.ConfirmSelect();
            for (int i = 0; i < 10; i++)
                Model.BasicPublish("", String.Empty, null, new byte[] { });
            Assert.That(Model.WaitForConfirms(), Is.True);
        }
    }
}
