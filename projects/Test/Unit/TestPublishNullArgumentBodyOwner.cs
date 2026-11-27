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
using System.Buffers;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;
using Xunit;

namespace Test.Unit
{
    public class TestPublishNullArgumentBodyOwner
    {
        public static TheoryData<int, string, bool> Cases()
        {
            var cases = new TheoryData<int, string, bool>();
            string[] paramNames = { "exchange", "routingKey", "exchange", "routingKey", "addr", "addr" };
            for (int overload = 0; overload < paramNames.Length; overload++)
            {
                cases.Add(overload, paramNames[overload], false);
                cases.Add(overload, paramNames[overload], true);
            }

            return cases;
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public async Task PublishingToANullAddressDisposesTheBodyOwner_GH2101(int overload, string paramName, bool throwingOwner)
        {
            using var session = new TestSession(new TestSessionOptions { ChannelNumber = 1 });
            var options = new CreateChannelOptions(publisherConfirmationsEnabled: false, publisherConfirmationTrackingEnabled: false);
            var inner = new RecoveryAwareChannel(session, options);
            try
            {
                // CreateChannelAsync returns the recovery wrapper, so publish through it.
                var channel = new AutorecoveringChannel(null!, inner, options);
                var owner = new CountingOwner(throwingOwner);
                var memory = new ReadOnlyMemory<byte>(new byte[8]);
                var sequence = new ReadOnlySequence<byte>(new byte[8]);
                var name = new CachedString("q");

                ArgumentNullException ex = await Assert.ThrowsAsync<ArgumentNullException>(() => (overload switch
                {
                    0 => channel.BasicPublishAsync(null!, name, false, new BasicProperties(), memory, owner),
                    1 => channel.BasicPublishAsync(name, null!, false, new BasicProperties(), memory, owner),
                    2 => channel.BasicPublishAsync(null!, name, false, new BasicProperties(), sequence, owner),
                    3 => channel.BasicPublishAsync(name, null!, false, new BasicProperties(), sequence, owner),
                    4 => channel.BasicPublishAsync((PublicationAddress)null!, new BasicProperties(), memory, owner),
                    _ => channel.BasicPublishAsync((PublicationAddress)null!, new BasicProperties(), sequence, owner),
                }).AsTask());

                Assert.Equal(paramName, ex.ParamName);
                Assert.Equal(1, owner.DisposeCount);
            }
            finally
            {
                await session.CloseAsync(new ShutdownEventArgs(ShutdownInitiator.Library, Constants.ReplySuccess, "test teardown"));
                await inner.DisposeAsync();
            }
        }

        [Theory]
        [InlineData(0, "exchange")]
        [InlineData(1, "routingKey")]
        [InlineData(2, "addr")]
        public async Task PublishingToANullAddressWithoutABodyOwnerThrowsArgumentNullException_GH2101(int overload, string paramName)
        {
            using var session = new TestSession(new TestSessionOptions { ChannelNumber = 1 });
            var options = new CreateChannelOptions(publisherConfirmationsEnabled: false, publisherConfirmationTrackingEnabled: false);
            var channel = new RecoveryAwareChannel(session, options);
            try
            {
                var memory = new ReadOnlyMemory<byte>(new byte[8]);
                var name = new CachedString("q");

                ArgumentNullException ex = await Assert.ThrowsAsync<ArgumentNullException>(() => (overload switch
                {
                    0 => channel.BasicPublishAsync(null!, name, false, new BasicProperties(), memory),
                    1 => channel.BasicPublishAsync(name, null!, false, new BasicProperties(), memory),
                    _ => channel.BasicPublishAsync((PublicationAddress)null!, new BasicProperties(), memory),
                }).AsTask());

                Assert.Equal(paramName, ex.ParamName);
            }
            finally
            {
                await session.CloseAsync(new ShutdownEventArgs(ShutdownInitiator.Library, Constants.ReplySuccess, "test teardown"));
                await channel.DisposeAsync();
            }
        }

        public static TheoryData<string, bool> PublishExtensions()
        {
            var data = new TheoryData<string, bool>();
            foreach (MethodInfo method in PublishExtensionMethods())
            {
                data.Add(Describe(method), false);
                data.Add(Describe(method), true);
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(PublishExtensions))]
        public void PublishingThroughANullChannelThrowsAndDisposesAnyBodyOwner_GH2170(string extension, bool throwingOwner)
        {
            MethodInfo method = PublishExtensionMethods().Single(m => Describe(m) == extension);
            var owner = new CountingOwner(throwingOwner);
            object[] args = method.GetParameters().Select(p => ArgumentFor(p.ParameterType, owner)).ToArray();

            TargetInvocationException ex = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, args));

            ArgumentNullException inner = Assert.IsType<ArgumentNullException>(ex.InnerException);
            Assert.Equal("channel", inner.ParamName);
            bool takesOwner = method.GetParameters().Any(p => p.ParameterType == typeof(IDisposable));
            Assert.Equal(takesOwner ? 1 : 0, owner.DisposeCount);
        }

        private static MethodInfo[] PublishExtensionMethods()
        {
            MethodInfo[] methods = typeof(IChannelExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == nameof(IChannelExtensions.BasicPublishAsync))
                .Select(m => m.IsGenericMethodDefinition ? m.MakeGenericMethod(typeof(BasicProperties)) : m)
                .ToArray();
            Assert.NotEmpty(methods);
            return methods;
        }

        // Short labels: xUnit truncates a string argument at 50 characters in the test name.
        private static string Describe(MethodInfo method)
            => string.Concat(method.GetParameters().Skip(1).Select(p => Abbreviate(p.ParameterType)));

        private static string Abbreviate(Type type)
        {
            if (type == typeof(string)) return "s";
            if (type == typeof(CachedString)) return "C";
            if (type == typeof(PublicationAddress)) return "A";
            if (type == typeof(ReadOnlyMemory<byte>)) return "M";
            if (type == typeof(ReadOnlySequence<byte>)) return "Q";
            if (type == typeof(IDisposable)) return "+owner";
            if (type == typeof(bool)) return "b";
            if (type == typeof(BasicProperties)) return "P";
            if (type == typeof(CancellationToken)) return "";
            throw new InvalidOperationException($"No label for {type}; extend Abbreviate.");
        }

        private static object ArgumentFor(Type type, IDisposable owner)
        {
            if (type == typeof(IChannel)) return null;
            if (type == typeof(string)) return "";
            if (type == typeof(CachedString)) return new CachedString("q");
            if (type == typeof(PublicationAddress)) return new PublicationAddress(ExchangeType.Direct, "", "q");
            if (type == typeof(ReadOnlyMemory<byte>)) return new ReadOnlyMemory<byte>(new byte[8]);
            if (type == typeof(ReadOnlySequence<byte>)) return new ReadOnlySequence<byte>(new byte[8]);
            if (type == typeof(IDisposable)) return owner;
            if (type == typeof(bool)) return false;
            if (type == typeof(BasicProperties)) return new BasicProperties();
            if (type == typeof(CancellationToken)) return CancellationToken.None;
            throw new InvalidOperationException($"No test argument for {type}; extend ArgumentFor.");
        }

        private sealed class CountingOwner : IDisposable
        {
            public int DisposeCount { get; private set; }

            private readonly bool _throwing;

            public CountingOwner(bool throwing) => _throwing = throwing;

            public void Dispose()
            {
                DisposeCount++;
                if (_throwing)
                {
                    throw new InvalidOperationException("owner bug");
                }
            }
        }
    }
}
