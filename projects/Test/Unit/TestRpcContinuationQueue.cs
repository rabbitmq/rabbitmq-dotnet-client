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
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Impl;
using Xunit;

namespace Test.Unit
{
    public class TestRpcContinuationQueue
    {
        private class TestSimpleAsyncRpcContinuation : SimpleAsyncRpcContinuation
        {
            public TestSimpleAsyncRpcContinuation()
                : base(ProtocolCommandId.BasicGet, TimeSpan.FromSeconds(10), CancellationToken.None)
            {
            }
        }

        [Fact]
        public void TestRpcContinuationQueueEnqueueAndRelease()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            var inputContinuation = new TestSimpleAsyncRpcContinuation();
            queue.Enqueue(inputContinuation);
            IRpcContinuation outputContinuation = queue.Next();
            Assert.Equal(outputContinuation, inputContinuation);
        }

        [Fact]
        public void TestRpcContinuationQueueEnqueueAndRelease2()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            var inputContinuation = new TestSimpleAsyncRpcContinuation();
            queue.Enqueue(inputContinuation);
            IRpcContinuation outputContinuation = queue.Next();
            Assert.Equal(outputContinuation, inputContinuation);
            IRpcContinuation outputContinuation1 = queue.Next();
            Assert.NotEqual(outputContinuation1, inputContinuation);
        }

        [Fact]
        public void TestRpcContinuationQueueEnqueue2()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            var inputContinuation = new TestSimpleAsyncRpcContinuation();
            var inputContinuation1 = new TestSimpleAsyncRpcContinuation();
            queue.Enqueue(inputContinuation);
            Assert.Throws<NotSupportedException>(() =>
            {
                queue.Enqueue(inputContinuation1);
            });
        }

        [Fact]
        public void TestShouldIgnoreCommand_NoTimedOutCommands_ReturnsFalse()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            Assert.False(queue.ShouldIgnoreCommand(ProtocolCommandId.BasicAck));
        }

        [Fact]
        public void TestShouldIgnoreCommand_SingleTimedOutCommand_MatchesFirst()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            queue.RpcCanceled(false, [ProtocolCommandId.QueueDeclareOk]);
            Assert.True(queue.ShouldIgnoreCommand(ProtocolCommandId.QueueDeclareOk));
        }

        [Fact]
        public void TestShouldIgnoreCommand_SingleTimedOutCommand_NoMatch()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            queue.RpcCanceled(false, [ProtocolCommandId.QueueDeclareOk]);
            Assert.False(queue.ShouldIgnoreCommand(ProtocolCommandId.BasicAck));
        }

        [Fact]
        public void TestShouldIgnoreCommand_TwoTimedOutCommands_MatchesFirst()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            queue.RpcCanceled(false, [ProtocolCommandId.BasicGetOk, ProtocolCommandId.BasicGetEmpty]);
            Assert.True(queue.ShouldIgnoreCommand(ProtocolCommandId.BasicGetOk));
        }

        [Fact]
        public void TestShouldIgnoreCommand_TwoTimedOutCommands_MatchesSecond()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            queue.RpcCanceled(false, [ProtocolCommandId.BasicGetOk, ProtocolCommandId.BasicGetEmpty]);
            Assert.True(queue.ShouldIgnoreCommand(ProtocolCommandId.BasicGetEmpty));
        }

        [Fact]
        public void TestShouldIgnoreCommand_TwoTimedOutCommands_NoMatch()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            queue.RpcCanceled(false, [ProtocolCommandId.BasicGetOk, ProtocolCommandId.BasicGetEmpty]);
            Assert.False(queue.ShouldIgnoreCommand(ProtocolCommandId.BasicAck));
        }

        [Fact]
        public void TestShouldIgnoreCommand_ConsumesTimedOutState()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            queue.RpcCanceled(false, [ProtocolCommandId.QueueDeclareOk]);
            Assert.True(queue.ShouldIgnoreCommand(ProtocolCommandId.QueueDeclareOk));
            // Second call should return false — the timed-out state was consumed
            Assert.False(queue.ShouldIgnoreCommand(ProtocolCommandId.QueueDeclareOk));
        }

        [Fact]
        public void TestShouldIgnoreCommand_EmptyAfterConsume()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            queue.RpcCanceled(false, [ProtocolCommandId.QueueDeclareOk]);
            // Consume with a non-matching command ID
            Assert.False(queue.ShouldIgnoreCommand(ProtocolCommandId.BasicAck));
            // Now the timed-out state is consumed, any check returns false
            Assert.False(queue.ShouldIgnoreCommand(ProtocolCommandId.QueueDeclareOk));
        }

        [Fact]
        public void TestRpcCanceled_ResponseReceivedTrue_DoesNotRecord()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            queue.RpcCanceled(true, [ProtocolCommandId.QueueDeclareOk]);
            // Since responseReceived=true, no command IDs should be recorded
            Assert.False(queue.ShouldIgnoreCommand(ProtocolCommandId.QueueDeclareOk));
        }
    }
}
