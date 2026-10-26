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
using System.Reflection;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;
using Xunit;

namespace Test.Unit
{
    public class TestAutorecoveringChannelBodyOwner
    {
        public static TheoryData<int> Overloads => new TheoryData<int> { 0, 1, 2, 3 };

        [Theory]
        [MemberData(nameof(Overloads))]
        public async Task PublishingOnADisposedChannelDisposesTheBodyOwner_GH2099(int overload)
        {
            using var session = new TestSession(new TestSessionOptions { ChannelNumber = 1 });
            var options = new CreateChannelOptions(publisherConfirmationsEnabled: false, publisherConfirmationTrackingEnabled: false);
            var inner = new RecoveryAwareChannel(session, options);
            try
            {
                var channel = new AutorecoveringChannel(null!, inner, options);

                // DisposeAsync dereferences the connection, null here; the flag is what publishing reads.
                typeof(AutorecoveringChannel).GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(channel, true);

                var owner = new CountingOwner();
                var memory = new ReadOnlyMemory<byte>(new byte[8]);
                var sequence = new ReadOnlySequence<byte>(new byte[8]);
                var exchange = new CachedString("");
                var routingKey = new CachedString("q");

                await Assert.ThrowsAsync<ObjectDisposedException>(() => (overload switch
                {
                    0 => channel.BasicPublishAsync("", "q", false, new BasicProperties(), memory, owner),
                    1 => channel.BasicPublishAsync("", "q", false, new BasicProperties(), sequence, owner),
                    2 => channel.BasicPublishAsync(exchange, routingKey, false, new BasicProperties(), memory, owner),
                    _ => channel.BasicPublishAsync(exchange, routingKey, false, new BasicProperties(), sequence, owner),
                }).AsTask());

                Assert.Equal(1, owner.DisposeCount);
            }
            finally
            {
                await session.CloseAsync(new ShutdownEventArgs(ShutdownInitiator.Library, Constants.ReplySuccess, "test teardown"));
                await inner.DisposeAsync();
            }
        }

        private sealed class CountingOwner : IDisposable
        {
            public int DisposeCount { get; private set; }

            public void Dispose() => DisposeCount++;
        }
    }
}
