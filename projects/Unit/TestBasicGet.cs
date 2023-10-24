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

using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client.Exceptions;
using Xunit;
using Xunit.Abstractions;

namespace RabbitMQ.Client.Unit
{
    public class TestBasicGet : IntegrationFixture
    {
        public TestBasicGet(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void TestBasicGetWithClosedChannel()
        {
            WithNonEmptyQueue((_, q) =>
               {
                   WithClosedChannel(cm =>
                   {
                       Assert.Throws<AlreadyClosedException>(() => cm.BasicGet(q, true));
                   });
               });
        }

        [Fact]
        public void TestBasicGetWithEmptyResponse()
        {
            WithEmptyQueue((channel, queue) =>
            {
                BasicGetResult res = channel.BasicGet(queue, false);
                Assert.Null(res);
            });
        }

        [Fact]
        public void TestBasicGetWithNonEmptyResponseAndAutoAckMode()
        {
            const string msg = "for basic.get";
            WithNonEmptyQueue((channel, queue) =>
            {
                BasicGetResult res = channel.BasicGet(queue, true);
                Assert.Equal(msg, _encoding.GetString(res.Body.ToArray()));
                AssertMessageCount(queue, 0);
            }, msg);
        }

        [Fact]
        public async Task TestBasicGetAsync()
        {
            const string msg = "for async basic.get";

            QueueDeclareOk queueResult = await _channel.QueueDeclareAsync(string.Empty, false, true, true, true, null);
            string queueName = queueResult.QueueName;

            await _channel.BasicPublishAsync(string.Empty, queueName, _encoding.GetBytes(msg), true);

            BasicGetResult getResult = await _channel.BasicGetAsync(queueName, true);
            Assert.Equal(msg, _encoding.GetString(getResult.Body.ToArray()));

            QueueDeclareOk queueResultPassive = await _channel.QueueDeclareAsync(queueName, true, true, true, true, null);
            Assert.Equal((uint)0, queueResultPassive.MessageCount);
        }
    }
}
