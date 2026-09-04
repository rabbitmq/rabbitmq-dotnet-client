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
using RabbitMQ.Client;
using Xunit;

namespace Test.Unit
{
    /// <summary>
    /// rabbitmq/rabbitmq-dotnet-client#1997
    ///
    /// Every recorded topology entity recovers itself by issuing a protocol operation on
    /// the recovering channel, and each of those has to receive the recovery cancellation
    /// token. Without it the operation falls back to <see cref="CancellationToken.None"/>,
    /// so when recovery is being torn down the in-flight request is not cancelled promptly
    /// and shutdown waits out the full <c>ContinuationTimeout</c> instead.
    ///
    /// <see cref="RabbitMQ.Client.Impl.RecordedConsumer"/> was the one entity missing it.
    /// Note what this test does and does not protect. The consumer fix itself is enforced by
    /// the compiler, because dropping the parameter again breaks its only call site in
    /// <c>RecoverConsumersAsync</c>. What is not enforced anywhere is a *newly added* entity
    /// repeating the mistake, which is what this guards, in the same
    /// reflection-over-the-assembly style as
    /// <c>TestNoSemaphoreSlimFieldIsDisposedAnywhere_GH1976</c> in the Integration project: the set of recovery methods
    /// is pinned so that adding one is a deliberate act, and each is required to accept the
    /// token. It cannot detect a method that accepts the token and then ignores it; that
    /// remains a matter for review of the one-line body.
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
                    "the recovery token reaches the protocol operation; without it a torn-down recovery " +
                    "waits out the full ContinuationTimeout. See #1997." + Environment.NewLine +
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
    }
}
