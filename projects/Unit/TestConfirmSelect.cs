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

using System;
using NUnit.Framework;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestConfirmSelect : IntegrationFixture
    {
        public TestConfirmSelect() : base()
        {
        }

        [Test]
        public void TestConfirmSelectIdempotency()
        {
            Model.ConfirmSelect();
            Assert.AreEqual(1, Model.NextPublishSeqNo);
            Publish();
            Assert.AreEqual(2, Model.NextPublishSeqNo);
            Publish();
            Assert.AreEqual(3, Model.NextPublishSeqNo);

            Model.ConfirmSelect();
            Publish();
            Assert.AreEqual(4, Model.NextPublishSeqNo);
            Publish();
            Assert.AreEqual(5, Model.NextPublishSeqNo);
            Publish();
            Assert.AreEqual(6, Model.NextPublishSeqNo);
        }

        protected void Publish()
        {
            Model.BasicPublish("", "amq.fanout", null, encoding.GetBytes("message"));
        }

        [Test]
        [TestCase(255)]
        [TestCase(256)]
        public void TestDeliveryTagDiverged_GH1043(int correlationIdLength)
        {
            bool.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_VERBOSE"), out bool verbose);

            byte[] body = RandomMessageBody();

            Model.ExchangeDeclare("sample", "fanout", autoDelete: true);
            if (verbose)
            {
                Model.BasicAcks += (s, e) => Console.WriteLine("Acked {0}", e.DeliveryTag);
            }
            Model.ConfirmSelect();

            IBasicProperties properties = Model.CreateBasicProperties();
            if (verbose)
            {
                Console.WriteLine("Client delivery tag {0}", Model.NextPublishSeqNo);
            }
            Model.BasicPublish(exchange: "sample", routingKey: string.Empty, properties, body);
            Model.WaitForConfirmsOrDie();

            try
            {
                properties = Model.CreateBasicProperties();
                properties.CorrelationId = new string('o', correlationIdLength);
                if (verbose)
                {
                    Console.WriteLine("Client delivery tag {0}", Model.NextPublishSeqNo);
                }
                Model.BasicPublish("sample", string.Empty, properties, body);
                Model.WaitForConfirmsOrDie();
            }
            catch (Exception e)
            {
                if (verbose)
                {
                    Console.WriteLine("Error when trying to publish with long string: {0}", e.Message);
                }
            }

            properties = Model.CreateBasicProperties();
            if (verbose)
            {
                Console.WriteLine("Client delivery tag {0}", Model.NextPublishSeqNo);
            }
            Model.BasicPublish("sample", string.Empty, properties, body);
            Model.WaitForConfirmsOrDie();
            if (verbose)
            {
                Console.WriteLine("I'm done...");
            }
        }
    }
}
