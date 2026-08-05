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
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
using Xunit;

namespace Test.Unit
{
    /// <summary>
    /// Covers how topology recovery classifies a failure to recover a single entity: skip it and
    /// carry on, or abandon the whole recovery attempt and retry it.
    /// </summary>
    /// <remarks>
    /// See https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1993
    /// </remarks>
    public class TestTopologyRecoveryExceptionClassification
    {
        private static readonly ShutdownEventArgs s_shutdownArgs =
            new ShutdownEventArgs(ShutdownInitiator.Peer, Constants.ConnectionForced, "test");

        [Fact]
        public void TestConnectivityExceptionsAreRetried()
        {
            var notCancelled = CancellationToken.None;

            Assert.True(AutorecoveringConnection.ShouldRetryRecoveryAfter(
                new AlreadyClosedException(s_shutdownArgs), notCancelled));
            Assert.True(AutorecoveringConnection.ShouldRetryRecoveryAfter(
                new OperationInterruptedException(s_shutdownArgs), notCancelled));
            Assert.True(AutorecoveringConnection.ShouldRetryRecoveryAfter(
                new TimeoutException(), notCancelled));
        }

        // A protocol operation that ran past ContinuationTimeout completes its continuation as
        // cancelled, so it arrives here as an OperationCanceledException rather than a
        // TimeoutException. It must still be retried: the frame is already on the wire and the
        // broker can apply it after the client gave up. Getting this wrong leaves a consumer
        // registered on the broker that the client never wired up, and the queue silently stops
        // being consumed. See #1993.
        [Fact]
        public void TestOperationCanceledIsRetriedWhenRecoveryWasNotCancelled_GH1993()
        {
            Assert.True(AutorecoveringConnection.ShouldRetryRecoveryAfter(
                new OperationCanceledException(), CancellationToken.None));
            Assert.True(AutorecoveringConnection.ShouldRetryRecoveryAfter(
                new TaskCanceledException(), CancellationToken.None));
        }

        [Fact]
        public void TestOperationCanceledIsNotRetriedWhenRecoveryWasCancelled_GH1993()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Recovery itself is being torn down (the connection is closing). Retrying would be
            // pointless and would fight the shutdown.
            Assert.False(AutorecoveringConnection.ShouldRetryRecoveryAfter(
                new OperationCanceledException(), cts.Token));
            Assert.False(AutorecoveringConnection.ShouldRetryRecoveryAfter(
                new TaskCanceledException(), cts.Token));
        }

        [Fact]
        public void TestUnrelatedExceptionsAreNotRetried()
        {
            // e.g. a queue that now fails an equivalence check, or a bug in a user callback. Retrying
            // cannot help, so recovery skips the entity and carries on, as it does today.
            Assert.False(AutorecoveringConnection.ShouldRetryRecoveryAfter(
                new ArgumentException("nope"), CancellationToken.None));
            Assert.False(AutorecoveringConnection.ShouldRetryRecoveryAfter(
                null, CancellationToken.None));
        }
    }
}
