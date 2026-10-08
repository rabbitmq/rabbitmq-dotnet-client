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
    /// regardless of routing key. Expected values are written out per row rather than computed, so
    /// an assertion cannot restate the implementation.
    /// </summary>
    public class TestMessagingDestinationName
    {
        [Theory]
        // Producer. The three values the convention document gives as its own Send-span examples
        // are direct_logs:warning, logs, and amq.default.
        [InlineData("direct_logs", "warning", null, "direct_logs:warning")]
        [InlineData("logs", "", null, "logs")]
        [InlineData("", "", null, "amq.default")]
        // Derived from the rule "when only one is available, only that value SHOULD be used".
        [InlineData("", "warning", null, "warning")]
        public void ProducerDestinationFollowsTheConvention_GH1980(string exchange, string routingKey,
            string queue, string expected)
        {
            Assert.Equal(expected, RabbitMQActivitySource.BuildDestinationName(
                RabbitMQActivitySource.MessagingRole.Producer, exchange, routingKey, queue));
        }

        [Theory]
        // Consumer: three parts, empty ones omitted, and no amq.default fallback at all.
        [InlineData("direct_logs", "warning", "my_queue", "direct_logs:warning:my_queue")]
        [InlineData("logs", "", "my_queue", "logs:my_queue")]
        [InlineData("", "", "my_queue", "my_queue")]
        [InlineData("", "warning", "my_queue", "warning:my_queue")]
        // "When {routing key} and {queue} are equal, only one of them SHOULD be used."
        [InlineData("direct_logs", "warning", "warning", "direct_logs:warning")]
        [InlineData("", "my_queue", "my_queue", "my_queue")]
        // Nothing to name it with: omit, rather than borrow the producer's fallback.
        [InlineData("", "", "", "")]
        [InlineData("", "", null, "")]
        public void ConsumerDestinationFollowsTheConvention_GH1980(string exchange, string routingKey,
            string queue, string expected)
        {
            Assert.Equal(expected, RabbitMQActivitySource.BuildDestinationName(
                RabbitMQActivitySource.MessagingRole.Consumer, exchange, routingKey, queue));
        }

        [Fact]
        public void AmqDefaultIsProducerOnly_GH1980()
        {
            /*
             * The one case where the two roles disagree, and the reason the role is an explicit
             * parameter rather than inferred from whether a queue was supplied: a delivery passes no
             * queue because a delivery frame carries none, which is not the same as being a
             * producer. Inferring it put amq.default on consumer spans.
             */
            Assert.Equal("amq.default", RabbitMQActivitySource.BuildDestinationName(
                RabbitMQActivitySource.MessagingRole.Producer, "", "", null));
            Assert.Equal("", RabbitMQActivitySource.BuildDestinationName(
                RabbitMQActivitySource.MessagingRole.Consumer, "", "", null));
        }
    }
}
