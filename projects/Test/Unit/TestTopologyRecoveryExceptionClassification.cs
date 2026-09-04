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
using RabbitMQ.Client.Impl;
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

        /*
         * https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1995
         *
         * A custom TopologyRecoveryExceptionHandler used to swallow the exception outright, so the
         * classification above was never consulted and #1993 stayed open for anyone who installed
         * one. The handler now runs first and the attempt is failed afterwards, but only for a
         * failure the broker may still act on.
         */

        [Fact]
        public void TestHandledPreconditionFailedIsNotRetried_GH1995()
        {
            /*
             * precondition-failed is the one refusal the broker will give again: the usual case is
             * redeclaring an entity with different arguments, which is what these handlers are
             * installed to repair. Once the handler has dealt with it there is nothing to retry.
             *
             * Note the deliberate difference from the classification used when no handler is
             * configured, asserted here too: that one treats every OperationInterruptedException as a
             * connectivity problem.
             */
            var preconditionFailed = new OperationInterruptedException(
                new ShutdownEventArgs(ShutdownInitiator.Peer, Constants.PreconditionFailed, "test"));

            Assert.False(AutorecoveringConnection.ShouldRetryAfterHandledRecoveryException(
                preconditionFailed, connectionIsOpen: true, CancellationToken.None));

            Assert.True(AutorecoveringConnection.ShouldRetryRecoveryAfter(
                preconditionFailed, CancellationToken.None));
        }

        [Theory]
        [InlineData(Constants.AccessRefused)]
        [InlineData(Constants.NotFound)]
        [InlineData(Constants.ResourceLocked)]
        public void TestHandledTransientRefusalIsRetried_GH1995(int replyCode)
        {
            /*
             * These channel-level refusals are transient in this client, so they must NOT be treated
             * as final. resource-locked and access-refused are what the broker answers while an
             * exclusive queue is still owned by the connection that just died, and NetworkRecoveryInterval
             * defaults to 5 seconds while the broker needs roughly two missed heartbeat intervals to
             * reap that owner, so a prompt reconnect hits them routinely and a retry succeeds. not-found
             * is transient when the missing entity is one this same pass has yet to declare, or skipped.
             *
             * Classifying any of them final permanently dropped the entity, and everything bound to it,
             * while recovery reported success, making a connection with a handler installed strictly
             * worse than one without. It also livelocked consumer recovery, because consumers share one
             * channel: a refusal classified final there closes the channel, and the consumers after it
             * then fail with AlreadyClosedException, which forces a retry that reproduces the same
             * refusal forever.
             */
            var transient = new OperationInterruptedException(
                new ShutdownEventArgs(ShutdownInitiator.Peer, (ushort)replyCode, "test"));

            Assert.True(AutorecoveringConnection.ShouldRetryAfterHandledRecoveryException(
                transient, connectionIsOpen: true, CancellationToken.None));
        }

        [Fact]
        public void TestHandledNeverSentOperationIsRetriedDespitePreconditionFailed_GH1995()
        {
            /*
             * AlreadyClosedException derives from OperationInterruptedException and carries the
             * channel's close reason, so it can present precondition-failed even though the operation
             * was never transmitted. The entity is therefore definitely un-recovered and only a retry
             * can fix it. Without the AlreadyClosedException guard this would be classified final.
             */
            var neverSent = new AlreadyClosedException(
                new ShutdownEventArgs(ShutdownInitiator.Peer, Constants.PreconditionFailed, "test"));

            Assert.True(AutorecoveringConnection.ShouldRetryAfterHandledRecoveryException(
                neverSent, connectionIsOpen: true, CancellationToken.None));
        }

        [Fact]
        public void TestHandledPreconditionFailedIsRetriedWhenTheConnectionIsGone_GH1995()
        {
            /*
             * The same reply codes appear on connection-level closes, where nothing was applied, so
             * the connection-open requirement is what separates a channel refusal from a dead
             * connection. Without it this would be classified final.
             */
            var whileClosed = new OperationInterruptedException(
                new ShutdownEventArgs(ShutdownInitiator.Peer, Constants.PreconditionFailed, "test"));

            Assert.True(AutorecoveringConnection.ShouldRetryAfterHandledRecoveryException(
                whileClosed, connectionIsOpen: false, CancellationToken.None));
        }

        [Fact]
        public void TestClassifierRequiresTheInnerExceptionNotTheWrapper_GH1995()
        {
            /*
             * The call sites pass e.InnerException, not the TopologyRecoveryException wrapper. Passing
             * the wrapper would fall to the default arm and report "do not retry" for every failure,
             * silently reopening #1993 for handler users, so pin the distinction here.
             */
            var preconditionFailed = new OperationInterruptedException(
                new ShutdownEventArgs(ShutdownInitiator.Peer, Constants.PreconditionFailed, "test"));
            var wrapper = new TopologyRecoveryException("wrapped", preconditionFailed);

            Assert.False(AutorecoveringConnection.ShouldRetryAfterHandledRecoveryException(
                wrapper, connectionIsOpen: true, CancellationToken.None));
            Assert.True(AutorecoveringConnection.ShouldRetryAfterHandledRecoveryException(
                wrapper.InnerException, connectionIsOpen: false, CancellationToken.None));
        }


        [Fact]
        public void TestHandledFailureTheBrokerMayStillActOnIsRetried_GH1995()
        {
            var notCancelled = CancellationToken.None;

            // A hard error closes the connection, so the client's view of it is no longer
            // trustworthy however well the handler behaved.
            Assert.True(AutorecoveringConnection.ShouldRetryAfterHandledRecoveryException(
                new OperationInterruptedException(s_shutdownArgs), connectionIsOpen: true, notCancelled));

            // No reply code to reason about, so fall back to the coarse classification.
            Assert.True(AutorecoveringConnection.ShouldRetryAfterHandledRecoveryException(
                new OperationInterruptedException(), connectionIsOpen: true, notCancelled));

            Assert.True(AutorecoveringConnection.ShouldRetryAfterHandledRecoveryException(
                new AlreadyClosedException(s_shutdownArgs), connectionIsOpen: true, notCancelled));
            Assert.True(AutorecoveringConnection.ShouldRetryAfterHandledRecoveryException(
                new TimeoutException(), connectionIsOpen: true, notCancelled));

            // An operation that outran ContinuationTimeout: its frame is on the wire and the broker
            // can still apply it, which is the #1993 failure a handler cannot resolve.
            Assert.True(AutorecoveringConnection.ShouldRetryAfterHandledRecoveryException(
                new OperationCanceledException(), connectionIsOpen: true, notCancelled));
        }

        [Fact]
        public void TestHandledExceptionIsNotRetriedWhenRecoveryWasCancelled_GH1995()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.False(AutorecoveringConnection.ShouldRetryAfterHandledRecoveryException(
                new OperationCanceledException(), connectionIsOpen: true, cts.Token));
        }

        [Fact]
        public void TestHandledUnrelatedExceptionIsNotRetried_GH1995()
        {
            Assert.False(AutorecoveringConnection.ShouldRetryAfterHandledRecoveryException(
                new ArgumentException("nope"), connectionIsOpen: true, CancellationToken.None));
            Assert.False(AutorecoveringConnection.ShouldRetryAfterHandledRecoveryException(
                null, connectionIsOpen: true, CancellationToken.None));
        }
    }
}
