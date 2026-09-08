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
    /// rabbitmq/rabbitmq-dotnet-client#2035
    ///
    /// A dispatch concurrency of zero is a legal <see cref="ushort"/> and was unvalidated at every
    /// layer that can supply one, but it builds a consumer dispatcher whose concurrency loop runs zero
    /// times, so nothing ever drains the work channel. Consumers register successfully and never fire,
    /// deliveries queue forever, and because a queued delivery owns a pooled buffer whose only
    /// disposal sites are inside that loop, message bodies leak until the process dies. Nothing
    /// reports it: close even looks clean, because the dispatcher's worker is an already-completed
    /// <c>Task.WhenAll</c> over an empty array.
    ///
    /// <see cref="CreateChannelOptions.InternalConsumerDispatchConcurrency"/> is the one place every
    /// channel-creation path passes through, so it is where the value is coerced. These tests assert
    /// against that resolution directly, because a dispatcher built with zero produces no observable
    /// signal to assert on.
    /// </summary>
    public class TestConsumerDispatchConcurrency
    {
        [Theory]
        [InlineData((ushort)0)]
        [InlineData((ushort)1)]
        [InlineData((ushort)9)]
        public void ExplicitConcurrencyIsNeverZero_GH2035(ushort requested)
        {
            var options = new CreateChannelOptions(publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false, consumerDispatchConcurrency: requested);

            ushort effective = options.InternalConsumerDispatchConcurrency;

            Assert.True(effective > 0,
                $"requested {requested} resolved to {effective}, which builds a dispatcher with no reader loops");
            Assert.Equal(requested == 0 ? Constants.DefaultConsumerDispatchConcurrency : requested, effective);
        }

        [Theory]
        [InlineData((ushort)0)]
        [InlineData((ushort)1)]
        [InlineData((ushort)9)]
        public void ConcurrencyInheritedFromTheFactoryIsNeverZero_GH2035(ushort requested)
        {
            /*
             * The other supplier of the value, and a different branch of the resolution: a factory
             * set to zero, inherited by a channel created with no explicit concurrency. Built from a
             * real ConnectionFactory rather than a hand-rolled ConnectionConfig so that the property
             * this test names is the one actually exercised.
             */
            var factory = new ConnectionFactory { ConsumerDispatchConcurrency = requested };

            CreateChannelOptions options =
                CreateChannelOptions.CreateOrUpdate(null, factory.CreateConfig(null));

            ushort effective = options.InternalConsumerDispatchConcurrency;

            Assert.True(effective > 0,
                $"factory concurrency {requested} resolved to {effective}, which builds a dispatcher with no reader loops");
            Assert.Equal(requested == 0 ? Constants.DefaultConsumerDispatchConcurrency : requested, effective);
        }
    }
}
