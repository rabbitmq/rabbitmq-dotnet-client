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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    /// <summary>
    /// rabbitmq/rabbitmq-dotnet-client#1976
    ///
    /// Disposing a <see cref="SemaphoreSlim"/> while another task is parked in
    /// <c>WaitAsync</c> leaves that waiter pending forever: it does not fault, it
    /// does not cancel, and neither the waiter's own cancellation token nor its
    /// wait timeout releases it. Issue #1968 is the confirmed instance - the same
    /// dispose-without-release on <c>SocketFrameHandler</c>'s semaphore stranded
    /// MainLoop and cost a full 30s connection-close timeout on net472 CI.
    ///
    /// The client's remaining semaphores all have waiters that can still be
    /// running when disposal begins, so none of them may be disposed. The race is
    /// not deterministically forceable from the public API for every site, so
    /// these tests assert the invariant that makes the race impossible: after a
    /// full dispose, every semaphore is still usable. <c>Wait(0)</c> throws
    /// <see cref="ObjectDisposedException"/> on a disposed instance and returns
    /// normally otherwise, so it detects disposal without blocking. Note that
    /// <c>CurrentCount</c> does not throw, so it cannot be used for this.
    /// </summary>
    public class TestSemaphoreDisposal : IntegrationFixture
    {
        public TestSemaphoreDisposal(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestChannelSemaphoresAreNotDisposed_GH1976()
        {
            /*
             * _rpcSemaphore is awaited by every RPC on the channel under the
             * continuation's linked token, so disposing it during an in-flight RPC
             * strands that RPC permanently - the ContinuationTimeout does not shake
             * it loose. _confirmSemaphore is awaited on the publish path and, during
             * shutdown cleanup, with a 5s timeout added specifically so a stuck
             * semaphore cannot block shutdown, which is reasoning that does not
             * survive the semaphore being disposed rather than merely held.
             *
             * Note that AutorecoveringChannel.DisposeAsync does not dispose its inner
             * channel, so disposing the IChannel handed back by CreateChannelAsync
             * would never reach Channel's dispose path. Dispose the inner channel
             * directly, which is what a non-recovering connection does.
             */
            IChannel channel = await _conn.CreateChannelAsync(_createChannelOptions);
            RecoveryAwareChannel inner = ((AutorecoveringChannel)channel).InnerChannel;

            await channel.CloseAsync();
            await inner.DisposeAsync();
            await channel.DisposeAsync();

            AssertSemaphoresUsable(inner, "_rpcSemaphore", "_confirmSemaphore");
        }

        [Fact]
        public async Task TestConnectionSemaphoresAreNotDisposed_GH1976()
        {
            /*
             * _recordedEntitiesSemaphore and _channelsSemaphore are awaited from the
             * recording and recovery paths, which run concurrently with disposal. A
             * recovery attempt parked on either one would never return.
             *
             * MainSession._closingSemaphore is the closest analogue of #1968:
             * Connection.DisposeAsync disposes the session after AbortAsync, while
             * MainLoop's FinishCloseAsync independently calls SetSessionClosingAsync.
             * If the latter is parked on the semaphore when disposal runs, MainLoop
             * never returns.
             *
             * SocketFrameHandler._closingSemaphore is checked here too: that is the
             * one #1968 actually fixed, so this keeps a regression test on it.
             */
            ConnectionFactory cf = CreateConnectionFactory();
            var conn = (AutorecoveringConnection)await CreateConnectionAsyncWithRetries(cf);
            IChannel channel = await conn.CreateChannelAsync(_createChannelOptions);

            Connection innerConnection = GetFieldValue<Connection>(conn, "_innerConnection");
            MainSession session0 = GetFieldValue<MainSession>(innerConnection, "_session0");
            var channel0 = GetFieldValue<Channel>(innerConnection, "_channel0");
            IFrameHandler frameHandler = innerConnection.FrameHandler;

            await channel.CloseAsync();
            await channel.DisposeAsync();
            await conn.CloseAsync();
            await conn.DisposeAsync();

            AssertSemaphoresUsable(new Dictionary<object, string[]>
            {
                { conn, new[] { "_recordedEntitiesSemaphore", "_channelsSemaphore" } },
                { session0, new[] { "_closingSemaphore" } },
                { channel0, new[] { "_rpcSemaphore", "_confirmSemaphore" } },
                { frameHandler, new[] { "_closingSemaphore" } }
            });
        }

        [Fact]
        public void TestNoSemaphoreSlimFieldIsDisposedAnywhere_GH1976()
        {
            /*
             * A guard against the anti-pattern coming back somewhere the two tests
             * above do not reach. Every SemaphoreSlim field in the client is listed
             * here so that adding one is a deliberate act; SemaphoreSlim only needs
             * disposal once AvailableWaitHandle has been read, and no site in the
             * client ever exposes it.
             */
            var expected = new SortedSet<string>
            {
                "RabbitMQ.Client.Impl.AutorecoveringConnection._channelsSemaphore",
                "RabbitMQ.Client.Impl.AutorecoveringConnection._recordedEntitiesSemaphore",
                "RabbitMQ.Client.Impl.Channel._confirmSemaphore",
                "RabbitMQ.Client.Impl.Channel._rpcSemaphore",
                "RabbitMQ.Client.Impl.MainSession._closingSemaphore",
                "RabbitMQ.Client.Impl.SocketFrameHandler._closingSemaphore"
            };

            var actual = new SortedSet<string>();
            foreach (Type type in typeof(IChannel).Assembly.GetTypes())
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.DeclaredOnly |
                    BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (field.FieldType == typeof(SemaphoreSlim))
                    {
                        actual.Add($"{type.FullName}.{field.Name}");
                    }
                }
            }

            var added = new SortedSet<string>(actual);
            added.ExceptWith(expected);
            var removed = new SortedSet<string>(expected);
            removed.ExceptWith(actual);

            if (added.Count > 0 || removed.Count > 0)
            {
                Assert.Fail("the set of SemaphoreSlim fields in RabbitMQ.Client changed. " +
                    "Confirm the new field is never disposed while a waiter can be parked " +
                    "in WaitAsync (see #1976), add it to the assertions in this file, then " +
                    $"update this list.{Environment.NewLine}" +
                    $"added: {string.Join(", ", added)}{Environment.NewLine}" +
                    $"removed: {string.Join(", ", removed)}");
            }
        }

        private static void AssertSemaphoresUsable(object target, params string[] fieldNames)
        {
            AssertSemaphoresUsable(new Dictionary<object, string[]> { { target, fieldNames } });
        }

        /// <summary>
        /// Every field is probed before asserting so that a failure names all of the
        /// disposed semaphores, not just the first one.
        /// </summary>
        private static void AssertSemaphoresUsable(IDictionary<object, string[]> targets)
        {
            var disposedFields = new List<string>();

            foreach (KeyValuePair<object, string[]> target in targets)
            {
                foreach (string fieldName in target.Value)
                {
                    SemaphoreSlim semaphore = GetFieldValue<SemaphoreSlim>(target.Key, fieldName);

                    bool entered = false;
                    try
                    {
                        entered = semaphore.Wait(0);
                    }
                    catch (ObjectDisposedException)
                    {
                        disposedFields.Add($"{target.Key.GetType().Name}.{fieldName}");
                    }
                    finally
                    {
                        if (entered)
                        {
                            semaphore.Release();
                        }
                    }
                }
            }

            if (disposedFields.Count > 0)
            {
                Assert.Fail($"disposed after a full dispose: {string.Join(", ", disposedFields)}. " +
                    "Disposing a SemaphoreSlim strands any waiter parked in WaitAsync forever. " +
                    "See #1976.");
            }
        }

        private static T GetFieldValue<T>(object target, string fieldName)
        {
            Type type = target.GetType();
            FieldInfo field = null;

            while (type is not null && field is null)
            {
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }

            Assert.NotNull(field);
            return Assert.IsType<T>(field.GetValue(target));
        }
    }
}
