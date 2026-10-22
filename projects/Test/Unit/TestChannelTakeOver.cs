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
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;
using Xunit;

namespace Test.Unit
{
    public class TestChannelTakeOver
    {
        [Fact]
        public async Task AHandlerExceptionOnARecoveredChannelReachesCallbackException_GH2092()
        {
            using var oldSession = new TestSession(new TestSessionOptions { ChannelNumber = 1 });
            using var newSession = new TestSession(new TestSessionOptions { ChannelNumber = 1 });
            var oldChannel = new RecoveryAwareChannel(oldSession, CreateOptions());
            var newChannel = new RecoveryAwareChannel(newSession, CreateOptions());

            object reportedBy = null;
            var reported = new TaskCompletionSource<CallbackExceptionEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thrown = new InvalidOperationException("from the application's RecoveryAsync handler");
            oldChannel.CallbackExceptionAsync += (sender, ea) =>
            {
                reportedBy = sender;
                reported.TrySetResult(ea);
                return Task.CompletedTask;
            };
            oldChannel.RecoveryAsync += (sender, ea) => throw thrown;

            try
            {
                // What AutorecoveringChannel.AutomaticallyRecoverAsync does to the channel it replaces.
                newChannel.TakeOver(oldChannel);
                oldChannel.DropTakenOverHandlers();

                await newChannel.RunRecoveryEventHandlers(newChannel, CancellationToken.None);

                Assert.True(reported.Task.IsCompleted, "the handler's exception was not reported");
                Assert.Same(thrown, (await reported.Task).Exception);
                Assert.Same(newChannel, reportedBy);
            }
            finally
            {
                await DisposeChannelAsync(oldSession, oldChannel);
                await DisposeChannelAsync(newSession, newChannel);
            }
        }

        private static async Task DisposeChannelAsync(TestSession session, RecoveryAwareChannel channel)
        {
            await session.CloseAsync(new ShutdownEventArgs(ShutdownInitiator.Library,
                Constants.ReplySuccess, "test teardown"));
            await channel.DisposeAsync();
        }

        private static CreateChannelOptions CreateOptions()
            => new CreateChannelOptions(publisherConfirmationsEnabled: false, publisherConfirmationTrackingEnabled: false);
    }
}
