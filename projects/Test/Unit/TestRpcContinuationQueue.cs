// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2020 VMware, Inc.
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
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Threading;
using RabbitMQ.Client.client.framing;
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
    }
}
