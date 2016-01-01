// This source code is dual-licensed under the Apache License, version
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
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;

using System;
using System.Collections.Generic;

using RabbitMQ.Client.Exceptions;

namespace RabbitMQ.Client.Unit {
    [TestFixture]
    public class TestNoWait : IntegrationFixture {
        [Test]
        public void TestQueueDeclareNoWait()
        {
            string q = GenerateQueueName();
            Model.QueueDeclareNoWait(q, false, true, false, null);
            Model.QueueDeclarePassive(q);
        }

        [Test]
        public void TestQueueBindNoWait()
        {
            string q = GenerateQueueName();
            Model.QueueDeclareNoWait(q, false, true, false, null);
            Model.QueueBindNoWait(q, "amq.fanout", "", null);
        }

        [Test]
        public void TestQueueDeleteNoWait()
        {
            string q = GenerateQueueName();
            Model.QueueDeclareNoWait(q, false, true, false, null);
            Model.QueueDeleteNoWait(q, false, false);
        }

        [Test]
        public void TestExchangeDeclareNoWait()
        {
            string x = GenerateExchangeName();
            try
            {
                Model.ExchangeDeclareNoWait(x, "fanout", false, true, null);
                Model.ExchangeDeclarePassive(x);
            } finally {
                Model.ExchangeDelete(x);
            }
        }

        [Test]
        public void TestExchangeBindNoWait()
        {
            string x = GenerateExchangeName();
            try
            {
                Model.ExchangeDeclareNoWait(x, "fanout", false, true, null);
                Model.ExchangeBindNoWait(x, "amq.fanout", "", null);
            } finally {
                Model.ExchangeDelete(x);
            }
        }

        [Test]
        public void TestExchangeUnbindNoWait()
        {
            string x = GenerateExchangeName();
            try
            {
                Model.ExchangeDeclare(x, "fanout", false, true, null);
                Model.ExchangeBind(x, "amq.fanout", "", null);
                Model.ExchangeUnbindNoWait(x, "amq.fanout", "", null);
            } finally {
                Model.ExchangeDelete(x);
            }
        }

        [Test]
        public void TestExchangeDeleteNoWait()
        {
            string x = GenerateExchangeName();
            Model.ExchangeDeclareNoWait(x, "fanout", false, true, null);
            Model.ExchangeDeleteNoWait(x, false);
        }
    }
}
