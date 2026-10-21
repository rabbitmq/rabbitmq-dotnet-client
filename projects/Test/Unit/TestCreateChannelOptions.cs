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
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

using ImplChannel = RabbitMQ.Client.Impl.Channel;

namespace Test.Unit
{
    public class TestCreateChannelOptions
    {
        [Fact]
        public void ConstructorDefaultsConsumerDispatchConcurrencyToOne()
        {
            // Pins the constructor's default, not the field initializer's `= null`; the two
            // diverge on purpose, for the reasons on the member's remarks.
            var options = new CreateChannelOptions(publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false);

            // Literal, not Constants.DefaultConsumerDispatchConcurrency: that is the expression producing
            // the value, so asserting it cannot fail if the number this test is named after changes.
            Assert.Equal((ushort?)1, options.ConsumerDispatchConcurrency);
        }

        [Fact]
        public void ConstructedOptionsDoNotInheritConnectionFactoryConsumerDispatchConcurrency()
        {
            // The constructor supplied 1, so ForConnection has nothing to inherit into.
            var options = new CreateChannelOptions(publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false);
            ConnectionConfig config = CreateConnectionConfig(consumerDispatchConcurrency: 4);

            options = CreateChannelOptions.ForConnection(options, config);

            Assert.Equal((ushort)1,
                options.InternalConsumerDispatchConcurrency);
        }

        [Fact]
        public void NoOptionsInheritsConnectionFactoryConsumerDispatchConcurrency()
        {
            // The path that does inherit: no options at all, which is the null source.
            ConnectionConfig config = CreateConnectionConfig(consumerDispatchConcurrency: 4);

            CreateChannelOptions options = CreateChannelOptions.ForConnection(null, config);

            Assert.Null(options.ConsumerDispatchConcurrency);
            Assert.Equal((ushort)4, options.InternalConsumerDispatchConcurrency);
        }

        [Fact]
        public void ExplicitNullInheritsConnectionFactoryConsumerDispatchConcurrency()
        {
            var options = new CreateChannelOptions(publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false, consumerDispatchConcurrency: null);
            ConnectionConfig config = CreateConnectionConfig(consumerDispatchConcurrency: 4);

            options = CreateChannelOptions.ForConnection(options, config);

            Assert.Equal((ushort)4, options.InternalConsumerDispatchConcurrency);
        }

        [Fact]
        public void ExplicitValueOverridesConnectionFactoryConsumerDispatchConcurrency()
        {
            var options = new CreateChannelOptions(publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false, consumerDispatchConcurrency: 7);
            ConnectionConfig config = CreateConnectionConfig(consumerDispatchConcurrency: 4);

            options = CreateChannelOptions.ForConnection(options, config);

            Assert.Equal((ushort)7, options.InternalConsumerDispatchConcurrency);
        }

        [Fact]
        public void ForConnectionLeavesTheCallersOptionsUnwritten_GH2034()
        {
            // consumerDispatchConcurrency: null, or the caller's own value short-circuits the
            // resolution and a write-back to the connection's concurrency stays invisible.
            var mine = new CreateChannelOptions(publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false, consumerDispatchConcurrency: null);
            var tracing = new ConnectionTracingOptions { UseRoutingKeyAsOperationName = true };

            CreateChannelOptions forConnection = CreateChannelOptions.ForConnection(mine,
                CreateConnectionConfig(consumerDispatchConcurrency: 4,
                    continuationTimeout: TimeSpan.FromSeconds(31), tracingOptions: tracing));

            Assert.NotSame(mine, forConnection);
            Assert.Equal(TimeSpan.FromSeconds(31), forConnection.ContinuationTimeout);
            Assert.Same(tracing, forConnection.TracingOptions);
            Assert.Equal((ushort)4, forConnection.InternalConsumerDispatchConcurrency);

            // Not Equal(TimeSpan.Zero): that would pin the unfixed default documented in
            // docs/internal/consumer-dispatch-concurrency.md, so fixing it would turn this red.
            Assert.NotEqual(TimeSpan.FromSeconds(31), mine.ContinuationTimeout);
            Assert.Null(mine.TracingOptions);
            Assert.NotEqual((ushort)4, mine.InternalConsumerDispatchConcurrency);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void ForConnectionCopiesEveryPublicMember_GH2034(bool confirmations, bool tracking)
        {
            // Both flag orders, because in each one of the two flags equals its own default, so
            // a dropped copy of that flag is invisible in that case and caught only by the other.
            using RateLimiter limiter = new ThrottlingRateLimiter(7);
            var source = new CreateChannelOptions(confirmations, tracking,
                outstandingPublisherConfirmationsRateLimiter: limiter,
                consumerDispatchConcurrency: 3);

            CreateChannelOptions copy = CreateChannelOptions.ForConnection(source, CreateConnectionConfig());

            Assert.NotSame(source, copy);
            Assert.Equal(confirmations, copy.PublisherConfirmationsEnabled);
            Assert.Equal(tracking, copy.PublisherConfirmationTrackingEnabled);
            Assert.Same(limiter, copy.OutstandingPublisherConfirmationsRateLimiter);
            Assert.Equal((ushort?)3, copy.ConsumerDispatchConcurrency);
        }

        [Fact]
        public void ForConnectionWithNoSourceSuppliesTheLibraryDefaults_GH2034()
        {
            // What channel 0 and CreateChannelAsync with no options get, which the deleted internal
            // constructor left to field initializers and nothing asserted.
            CreateChannelOptions options = CreateChannelOptions.ForConnection(null, CreateConnectionConfig());

            Assert.False(options.PublisherConfirmationsEnabled);
            Assert.False(options.PublisherConfirmationTrackingEnabled);
            Assert.Null(options.ConsumerDispatchConcurrency);
            // The permit count, not just the type, because the limit is quoted in the member's own
            // remarks. #2054 may drop this limiter, and this is the assertion that will say so.
            Assert.NotNull(options.OutstandingPublisherConfirmationsRateLimiter);
            var statistics = options.OutstandingPublisherConfirmationsRateLimiter.GetStatistics();
            Assert.NotNull(statistics);
            Assert.Equal(128L, statistics.CurrentAvailablePermits);
        }

        [Fact]
        public void TheMemberSetOfCreateChannelOptionsIsUnchanged_GH2034()
        {
            // Comparing a copy against its source member by member cannot catch a member added
            // later and left out of the copy: both would carry its default. #2034.
            var expected = new SortedSet<string>(StringComparer.Ordinal)
            {
                "ConsumerDispatchConcurrency",
                "ContinuationTimeout",
                "InternalConsumerDispatchConcurrency",
                "OutstandingPublisherConfirmationsRateLimiter",
                "PublisherConfirmationTrackingEnabled",
                "PublisherConfirmationsEnabled",
                "TracingOptions",
                "_connectionConfigConsumerDispatchConcurrency",
                "_connectionConfigContinuationTimeout",
                "_connectionConfigTracingOptions"
            };

            // CompilerGenerated drops an auto-property's backing field, which would otherwise
            // demand a mangled name in the list above.
            var actual = new SortedSet<string>(typeof(CreateChannelOptions)
                .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m is FieldInfo or PropertyInfo)
                .Where(m => m.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
                .Select(m => m.Name), StringComparer.Ordinal);

            var added = new SortedSet<string>(actual, StringComparer.Ordinal);
            added.ExceptWith(expected);
            var removed = new SortedSet<string>(expected, StringComparer.Ordinal);
            removed.ExceptWith(actual);

            if (added.Count > 0 || removed.Count > 0)
            {
                Assert.Fail("the instance members of CreateChannelOptions changed. Decide whether " +
                    "an added one must be copied from the caller's options in the private " +
                    $"constructor (#2034), assert it above, then update this list.{Environment.NewLine}" +
                    $"added: {string.Join(", ", added)}{Environment.NewLine}" +
                    $"removed: {string.Join(", ", removed)}");
            }
        }

        [Fact]
        public async Task AChannelReadsTheConnectionItWasCreatedOn_GH2034()
        {
            TimeSpan firstTimeout = TimeSpan.FromSeconds(31);
            TimeSpan secondTimeout = TimeSpan.FromSeconds(7);
            var firstTracing = new ConnectionTracingOptions { UseRoutingKeyAsOperationName = true };
            var secondTracing = new ConnectionTracingOptions { UseRoutingKeyAsOperationName = false };

            var shared = new CreateChannelOptions(publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false, consumerDispatchConcurrency: null);

            // Both connections resolve before either channel is built: that ordering is what the
            // in-place write turned into a channel reading the wrong connection. #2034.
            CreateChannelOptions first = CreateChannelOptions.ForConnection(shared,
                CreateConnectionConfig(consumerDispatchConcurrency: 3,
                    continuationTimeout: firstTimeout, tracingOptions: firstTracing));
            CreateChannelOptions second = CreateChannelOptions.ForConnection(shared,
                CreateConnectionConfig(consumerDispatchConcurrency: 5,
                    continuationTimeout: secondTimeout, tracingOptions: secondTracing));

            using var firstSession = new TestSession();
            using var secondSession = new TestSession();
            ImplChannel firstChannel = null;
            ImplChannel secondChannel = null;

            try
            {
                firstChannel = new ImplChannel(firstSession, first);
                secondChannel = new ImplChannel(secondSession, second);

                Assert.Equal(firstTimeout, firstChannel.ContinuationTimeout);
                Assert.Same(firstTracing, firstChannel.TracingOptions);
                Assert.Equal((ushort)3, firstChannel.ConsumerDispatcher.Concurrency);
                Assert.Equal(secondTimeout, secondChannel.ContinuationTimeout);
                Assert.Same(secondTracing, secondChannel.TracingOptions);
                Assert.Equal((ushort)5, secondChannel.ConsumerDispatcher.Concurrency);
            }
            finally
            {
                // Nested so a throw tearing down the second channel still releases the first.
                // Session first in each: that clears IsOpen, so the dispose skips AbortAsync,
                // which the fake session cannot serve.
                try
                {
                    await DisposeChannelAsync(secondSession, secondChannel);
                }
                finally
                {
                    await DisposeChannelAsync(firstSession, firstChannel);
                }
            }
        }

        private static async Task DisposeChannelAsync(TestSession session, ImplChannel channel)
        {
            if (channel is null)
            {
                return;
            }

            await session.CloseAsync(new ShutdownEventArgs(ShutdownInitiator.Library,
                Constants.ReplySuccess, "test teardown"));
            await channel.DisposeAsync();
        }

        private static ConnectionConfig CreateConnectionConfig(ushort consumerDispatchConcurrency = 1,
            TimeSpan? continuationTimeout = null,
            ConnectionTracingOptions tracingOptions = null)
        {
            return new ConnectionConfig(
                virtualHost: "/",
                userName: "guest",
                password: "guest",
                credentialsProvider: null,
                authMechanisms: Array.Empty<IAuthMechanismFactory>(),
                clientProperties: new Dictionary<string, object>(),
                clientProvidedName: null,
                maxChannelCount: ConnectionFactory.DefaultChannelMax,
                maxFrameSize: 0,
                maxInboundMessageBodySize: ConnectionFactory.DefaultMaxInboundMessageBodySize,
                topologyRecoveryEnabled: true,
                topologyRecoveryFilter: new TopologyRecoveryFilter(),
                topologyRecoveryExceptionHandler: new TopologyRecoveryExceptionHandler(),
                networkRecoveryInterval: TimeSpan.FromSeconds(5),
                heartbeatInterval: TimeSpan.FromSeconds(60),
                continuationTimeout: continuationTimeout ?? TimeSpan.FromSeconds(20),
                handshakeContinuationTimeout: TimeSpan.FromSeconds(10),
                requestedConnectionTimeout: TimeSpan.FromSeconds(30),
                consumerDispatchConcurrency: consumerDispatchConcurrency,
                tracingOptions: tracingOptions,
                frameHandlerFactoryAsync: (_, _) => throw new NotSupportedException("not connected in this test"));
        }
    }
}
