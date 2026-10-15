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

#nullable enable

using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Impl;
using Xunit;

using ImplChannel = RabbitMQ.Client.Impl.Channel;

namespace Test.Unit
{
    public class TestConnectionSecure
    {
        [Fact]
        public async Task TestEachConnectionSecureChallengeReachesTheMechanism_GH2068()
        {
            using var session = new TestSession();
            var channel = CreateChannel(session);
            byte[] firstChallenge = Encoding.UTF8.GetBytes("Please tell me your password");
            byte[] response = Encoding.UTF8.GetBytes("My password is guest");
            byte[] secondChallenge = Encoding.UTF8.GetBytes("And once more");

            try
            {
                Task<ConnectionSecureOrTune> startOkTask = channel.ConnectionStartOkAsync(
                    new Dictionary<string, object?>(), "RABBIT-CR-DEMO", Encoding.UTF8.GetBytes("guest"),
                    "en_US", CancellationToken.None).AsTask();
                Assert.Equal(ProtocolCommandId.ConnectionStartOk,
                    await session.ReadTransmittedCommandAsync());

                await session.DeliverConnectionSecureAsync(firstChallenge);
                await startOkTask.WaitAsync(TimingFixture.TestTimeout);
                Assert.Equal(firstChallenge, (await startOkTask).m_challenge);

                Task<ConnectionSecureOrTune> secureOkTask =
                    channel.ConnectionSecureOkAsync(response, CancellationToken.None).AsTask();
                Assert.Equal(ProtocolCommandId.ConnectionSecureOk,
                    await session.ReadTransmittedCommandAsync());
                TransmittedCommand secureOk = Assert.Single(session.TransmittedCommands,
                    c => c.CommandId == ProtocolCommandId.ConnectionSecureOk);
                WireFormatting.ReadLongstr(secureOk.Method, out byte[] sentResponse);
                Assert.Equal(response, sentResponse);

                await session.DeliverConnectionSecureAsync(secondChallenge);
                await secureOkTask.WaitAsync(TimingFixture.TestTimeout);
                Assert.Equal(secondChallenge, (await secureOkTask).m_challenge);
            }
            finally
            {
                await DisposeChannelAsync(session, channel);
            }
        }

        // Without a ConnectionConfig, ContinuationTimeout is zero and every RPC cancels at once.
        private static ImplChannel CreateChannel(TestSession session)
            => new ImplChannel(session, new CreateChannelOptions(
                publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false))
            {
                ContinuationTimeout = TimingFixture.TestTimeout
            };

        private static async Task DisposeChannelAsync(TestSession session, ImplChannel channel)
        {
            await session.CloseAsync(new ShutdownEventArgs(ShutdownInitiator.Library,
                Constants.ReplySuccess, "test teardown"));
            await channel.DisposeAsync();
        }
    }
}
