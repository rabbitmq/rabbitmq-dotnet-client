// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at https://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestExtensions : IntegrationFixture
    {
        [Test]
        public async ValueTask TestConfirmBarrier()
        {
            await Model.ConfirmSelect();
            for (int i = 0; i < 10; i++)
            {
                await Model.BasicPublish("", string.Empty, null, new byte[] {});
            }
            Assert.That(await Model.WaitForConfirms(), Is.True);
        }

        [Test]
        public void TestConfirmBeforeWait()
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () => await Model.WaitForConfirms());
        }

        [Test]
        public async ValueTask TestExchangeBinding()
        {
            await Model.ConfirmSelect();

            await Model.ExchangeDeclare("src", ExchangeType.Direct, false, false, null);
            await Model.ExchangeDeclare("dest", ExchangeType.Direct, false, false, null);
            string queue = await Model.QueueDeclare();

            await Model.ExchangeBind("dest", "src", string.Empty);
            await Model.ExchangeBind("dest", "src", string.Empty);
            await Model.QueueBind(queue, "dest", string.Empty);

            await Model.BasicPublish("src", string.Empty, null, new byte[] {});
            await Model.WaitForConfirms();
            Assert.IsNotNull(await Model.BasicGet(queue, true));

            await Model.ExchangeUnbind("dest", "src", string.Empty);
            await Model.WaitForConfirms();
            Assert.IsNull(await Model.BasicGet(queue, true));

            await Model.ExchangeDelete("src");
            await Model.ExchangeDelete("dest");
        }
    }
}
