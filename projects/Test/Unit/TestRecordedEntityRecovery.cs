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
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Xunit;

namespace Test.Unit
{
    /// <summary>
    /// rabbitmq/rabbitmq-dotnet-client#1997
    ///
    /// Every recorded topology entity recovers itself by issuing a protocol operation, and each
    /// of those has to receive the recovery cancellation token. Consumers issue theirs on the
    /// shared recovering channel; exchanges, queues and bindings each open a channel of their
    /// own. Without the token the operation falls back to
    /// <see cref="CancellationToken.None"/>, so nothing about the request observes that recovery
    /// is being torn down. Note that cancelling an RPC's token does not shorten the wait for its
    /// reply, before or after this fix - see
    /// <c>docs/internal/connection-shutdown-and-cancellation.md</c>. What it governs is the
    /// unbounded wait for the channel's RPC semaphore, which is where a torn-down recovery
    /// actually stalls.
    ///
    /// <see cref="RabbitMQ.Client.Impl.RecordedConsumer"/> was the one entity missing it.
    /// Two tests here, covering two different failures. The reflection test pins the *set* of
    /// recovery methods and requires each to accept the token, so a newly added entity cannot
    /// repeat the omission unnoticed; it is in the same reflection-over-the-assembly style as
    /// <c>TestNoSemaphoreSlimFieldIsDisposedAnywhere_GH1976</c> in the Integration project. That
    /// test cannot see a method that accepts the token and then ignores it, which is a one-word
    /// change and the shape a refactor really takes, so the second test drives
    /// <c>RecoverAsync</c> against a recording channel and asserts the token that
    /// <c>basic.consume</c> was actually given.
    /// </summary>
    public class TestRecordedEntityRecovery
    {
        [Fact]
        public void EveryRecordedEntityRecoveryAcceptsACancellationToken_GH1997()
        {
            var expected = new SortedSet<string>(StringComparer.Ordinal)
            {
                "RabbitMQ.Client.Impl.RecordedBinding.RecoverAsync",
                "RabbitMQ.Client.Impl.RecordedConsumer.RecoverAsync",
                "RabbitMQ.Client.Impl.RecordedExchange.RecoverAsync",
                "RabbitMQ.Client.Impl.RecordedQueue.RecoverAsync"
            };

            var actual = new SortedSet<string>(StringComparer.Ordinal);
            var missingToken = new SortedSet<string>(StringComparer.Ordinal);

            // Deliberately an unguarded GetTypes(), matching the precedent this test follows. A
            // fallback that scanned only the types that loaded made a loader failure surface as
            // "the set of recovery methods changed, update this list", which is worse than the
            // ReflectionTypeLoadException it replaced.
            foreach (Type type in typeof(IChannel).Assembly.GetTypes()
                .Where(t => t.Name.StartsWith("Recorded", StringComparison.Ordinal)))
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.DeclaredOnly |
                    BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "RecoverAsync"))
                {
                    string name = $"{type.FullName}.{method.Name}";
                    actual.Add(name);

                    // The token must be REQUIRED. An optional `CancellationToken cancellationToken =
                    // default`, the style every IChannel method uses, would satisfy a mere
                    // "declares one" check while a call site that omits it silently supplies
                    // CancellationToken.None, which is exactly how #1997 survived for years.
                    if (!method.GetParameters().Any(p =>
                        p.ParameterType == typeof(CancellationToken) && false == p.IsOptional))
                    {
                        missingToken.Add(name);
                    }
                }
            }

            if (missingToken.Count > 0)
            {
                Assert.Fail("every recorded entity's RecoverAsync must accept a CancellationToken so " +
                    "the recovery token reaches the protocol operation; without it nothing about the " +
                    "request observes that recovery is being torn down, and the wait for the " +
                    "channel's RPC semaphore is unbounded. See #1997." + Environment.NewLine +
                    $"missing the token: {string.Join(", ", missingToken)}");
            }

            var added = new SortedSet<string>(actual, StringComparer.Ordinal);
            added.ExceptWith(expected);
            var removed = new SortedSet<string>(expected, StringComparer.Ordinal);
            removed.ExceptWith(actual);

            if (added.Count > 0 || removed.Count > 0)
            {
                Assert.Fail("the set of recorded entity recovery methods changed. Confirm the new one " +
                    "both accepts the recovery cancellation token and passes it to the protocol " +
                    "operation it issues (see #1997), then update this list. A recovery method named " +
                    "something other than RecoverAsync would not be seen by this test at all." +
                    Environment.NewLine +
                    $"added: {string.Join(", ", added)}" + Environment.NewLine +
                    $"removed: {string.Join(", ", removed)}");
            }
        }

#if NET
        /*
         * net8.0 only: DispatchProxy does not exist on net472, and the Unit project multi-targets
         * both. The behaviour under test is framework-independent, so covering it on the modern
         * target is enough; adding a net472-only DispatchProxy package to reach the other would put
         * a new dependency in the repo for one test.
         */
        [Fact]
        public async Task RecordedConsumerActuallyPassesTheTokenToBasicConsume_GH1997()
        {
            /*
             * The reflection test above pins the signature; this pins the body. Accepting the token
             * and then passing CancellationToken.None is a one-word change that no signature or
             * call-site check can see, and it is the shape a refactor actually takes - it was
             * measured to leave the whole suite green.
             *
             * RecoverAsync takes the channel as a parameter and never touches the recorded one, so
             * default(RecordedConsumer) is enough and no AutorecoveringChannel is needed. The
             * recording channel captures what basic.consume was really given.
             */
            using var cts = new CancellationTokenSource();
            IChannel channel = RecordingChannel.Create(out RecordingChannel recorder);

            await default(RabbitMQ.Client.Impl.RecordedConsumer).RecoverAsync(channel, cts.Token);

            Assert.True(recorder.BasicConsumeWasCalled,
                "basic.consume was never issued, so no token was captured and this test is vacuous");
            Assert.Equal(cts.Token, recorder.CapturedToken);
        }

        /// <summary>
        /// An <see cref="IChannel"/> that records the cancellation token handed to
        /// <c>BasicConsumeAsync</c>. DispatchProxy rather than a hand-written stub because
        /// <see cref="IChannel"/> is large and only this one member is of interest; any other
        /// member being called is a signal the test has drifted, so it throws rather than
        /// returning a default that would hide the drift.
        /// </summary>
        public class RecordingChannel : DispatchProxy
        {
            public bool BasicConsumeWasCalled { get; private set; }

            public CancellationToken CapturedToken { get; private set; }

            public static IChannel Create(out RecordingChannel recorder)
            {
                IChannel proxy = DispatchProxy.Create<IChannel, RecordingChannel>();
                recorder = (RecordingChannel)(object)proxy;
                return proxy;
            }

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod.Name == nameof(IChannel.BasicConsumeAsync))
                {
                    BasicConsumeWasCalled = true;
                    CapturedToken = args.OfType<CancellationToken>().Single();
                    return Task.FromResult("recorded-tag");
                }

                throw new NotSupportedException(
                    $"RecordingChannel was asked for {targetMethod.Name}, which it does not model. " +
                    "RecordedConsumer.RecoverAsync should only issue basic.consume.");
            }
        }
#endif
    }
}
