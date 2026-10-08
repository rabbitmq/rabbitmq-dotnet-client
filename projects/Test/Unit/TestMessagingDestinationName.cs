// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using RabbitMQ.Client;
using Xunit;

namespace Test.Unit
{
    /// <summary>
    /// rabbitmq/rabbitmq-dotnet-client#1980
    ///
    /// <c>messaging.destination.name</c> per the RabbitMQ semantic convention. The producer form is
    /// <c>{exchange}:{routing key}</c> and the consumer form adds the queue; the client previously
    /// emitted the bare exchange, or the literal <c>amq.default</c> whenever the exchange was empty
    /// regardless of routing key. The expected values below are written out rather than computed, so
    /// an assertion cannot restate the implementation, and the first three are the convention
    /// document's own examples.
    /// </summary>
    public class TestMessagingDestinationName
    {
        [Theory]
        // Producer: queue is null because a publish has no queue.
        [InlineData("direct_logs", "warning", null, "direct_logs:warning")]
        [InlineData("logs", "", null, "logs")]
        [InlineData("", "warning", null, "warning")]
        [InlineData("", "", null, "amq.default")]
        // Consumer: queue is known, so three parts.
        [InlineData("direct_logs", "warning", "my_queue", "direct_logs:warning:my_queue")]
        [InlineData("direct_logs", "warning", "warning", "direct_logs:warning")]
        [InlineData("", "", "my_queue", "my_queue")]
        [InlineData("logs", "", "my_queue", "logs:my_queue")]
        // Consumer with nothing to name it: omit rather than invent amq.default.
        [InlineData("", "", "", "")]
        public void DestinationNameFollowsTheRabbitMQConvention_GH1980(string exchange, string routingKey,
            string queue, string expected)
        {
            Assert.Equal(expected, RabbitMQActivitySource.BuildDestinationName(exchange, routingKey, queue));
        }

        [Fact]
        public void AmqDefaultIsProducerOnly_GH1980()
        {
            /*
             * The convention gives amq.default only for the default exchange with no routing key, and
             * only on the producer side - the consumer form has no such fallback and omits instead.
             * The old code returned it whenever the exchange was empty, which made a fetch from a
             * known queue claim it had touched the default exchange.
             */
            Assert.Equal("amq.default", RabbitMQActivitySource.BuildDestinationName("", "", null));
            Assert.Equal("", RabbitMQActivitySource.BuildDestinationName("", "", ""));
        }
    }
}
