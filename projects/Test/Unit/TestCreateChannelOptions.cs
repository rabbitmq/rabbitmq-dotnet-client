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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using Xunit;

namespace Test.Unit
{
    public class TestCreateChannelOptions
    {
        [Fact]
        public void ConstructorDefaultsConsumerDispatchConcurrencyToOne()
        {
            /*
             * Pins the constructor's default rather than the field initializer's. The field is
             * declared `= null` and its documentation describes null as "inherit from the factory",
             * but the public constructor defaults the parameter to 1 and assigns it unconditionally,
             * so options built through the constructor are serialized whatever the factory says.
             * That divergence is what #2027 set out to remove; it stays deliberately, because the
             * fix would be a compile-time break rather than a runtime one (see the remarks on the
             * member). This test exists so the two cannot drift apart silently again.
             */
            var options = new CreateChannelOptions(publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false);

            Assert.Equal(Constants.DefaultConsumerDispatchConcurrency, options.ConsumerDispatchConcurrency);
        }

        [Fact]
        public void ConstructedOptionsDoNotInheritConnectionFactoryConsumerDispatchConcurrency()
        {
            /*
             * The counterpart: because the constructor supplied 1, CreateOrUpdate has nothing to
             * inherit into, so the factory's value is deliberately ignored. Passing null explicitly
             * is what opts in - see ExplicitNullInherits... below.
             */
            var options = new CreateChannelOptions(publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false);
            ConnectionConfig config = CreateConnectionConfig(consumerDispatchConcurrency: 4);

            options = CreateChannelOptions.CreateOrUpdate(options, config);

            Assert.Equal(Constants.DefaultConsumerDispatchConcurrency,
                options.InternalConsumerDispatchConcurrency);
        }

        [Fact]
        public void NoOptionsInheritsConnectionFactoryConsumerDispatchConcurrency()
        {
            /*
             * The path that does inherit, and the only one where the field initializers apply:
             * CreateChannelAsync() with no options at all, which reaches the internal
             * CreateChannelOptions(ConnectionConfig) constructor.
             */
            ConnectionConfig config = CreateConnectionConfig(consumerDispatchConcurrency: 4);

            CreateChannelOptions options = CreateChannelOptions.CreateOrUpdate(null, config);

            Assert.Null(options.ConsumerDispatchConcurrency);
            Assert.Equal((ushort)4, options.InternalConsumerDispatchConcurrency);
        }

        [Fact]
        public void ExplicitNullInheritsConnectionFactoryConsumerDispatchConcurrency()
        {
            var options = new CreateChannelOptions(publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false, consumerDispatchConcurrency: null);
            ConnectionConfig config = CreateConnectionConfig(consumerDispatchConcurrency: 4);

            options = CreateChannelOptions.CreateOrUpdate(options, config);

            Assert.Equal((ushort)4, options.InternalConsumerDispatchConcurrency);
        }

        [Fact]
        public void ExplicitValueOverridesConnectionFactoryConsumerDispatchConcurrency()
        {
            var options = new CreateChannelOptions(publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false, consumerDispatchConcurrency: 7);
            ConnectionConfig config = CreateConnectionConfig(consumerDispatchConcurrency: 4);

            options = CreateChannelOptions.CreateOrUpdate(options, config);

            Assert.Equal((ushort)7, options.InternalConsumerDispatchConcurrency);
        }

        private static ConnectionConfig CreateConnectionConfig(ushort consumerDispatchConcurrency)
        {
            return new ConnectionConfig(
                virtualHost: "/",
                userName: "guest",
                password: "guest",
                credentialsProvider: null,
                authMechanisms: Array.Empty<IAuthMechanismFactory>(),
                clientProperties: new Dictionary<string, object>(),
                clientProvidedName: null,
                maxChannelCount: 2047,
                maxFrameSize: 0,
                maxInboundMessageBodySize: 134217728,
                topologyRecoveryEnabled: true,
                topologyRecoveryFilter: new TopologyRecoveryFilter(),
                topologyRecoveryExceptionHandler: new TopologyRecoveryExceptionHandler(),
                networkRecoveryInterval: TimeSpan.FromSeconds(5),
                heartbeatInterval: TimeSpan.FromSeconds(60),
                continuationTimeout: TimeSpan.FromSeconds(20),
                handshakeContinuationTimeout: TimeSpan.FromSeconds(10),
                requestedConnectionTimeout: TimeSpan.FromSeconds(30),
                consumerDispatchConcurrency: consumerDispatchConcurrency,
                frameHandlerFactoryAsync: (_, _) => Task.FromResult<IFrameHandler>(null!));
        }
    }
}
