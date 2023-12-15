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
//  Copyright (c) 2011-2020 VMware, Inc. or its affiliates.  All rights reserved.
//---------------------------------------------------------------------------

using System.Threading.Tasks;
using RabbitMQ.Client;
using Xunit;
using Xunit.Abstractions;

namespace Test.AsyncIntegration
{
    public class TestBasicPublishAsync : AsyncIntegrationFixture
    {
        public TestBasicPublishAsync(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestQueuePurgeAsync()
        {
            const int messageCount = 1024;

            var publishSyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            await _channel.ConfirmSelectAsync();

            QueueDeclareOk q = await _channel.QueueDeclareAsync(string.Empty, false, false, true, false, null);

            var publishTask = Task.Run(async () =>
            {
                byte[] body = GetRandomBody(512);
                for (int i = 0; i < messageCount; i++)
                {
                    await _channel.BasicPublishAsync(string.Empty, q, body);
                }
                await _channel.WaitForConfirmsOrDieAsync();
                publishSyncSource.SetResult(true);
            });

            Assert.True(await publishSyncSource.Task);
            Assert.Equal((uint)messageCount, await _channel.QueuePurgeAsync(q));
        }

        [Fact]
        public async Task TestNonCopyingBody()
        {
            const int size = 1024;

            QueueDeclareOk q = await _channel.QueueDeclareAsync(string.Empty, false, false, true, false, null);
            byte[] body = GetRandomBody(size);

            uint rentedBytes;

            using (var result = await TrackRentedBytes())
            {
                await _channel.BasicPublishAsync(string.Empty, q, body, copyBody: false);
                rentedBytes = result.RentedBytes;
            }

            Assert.Equal((uint)1, await _channel.QueuePurgeAsync(q));

            // It is expected that the rented bytes is smaller than the size of the body
            // since we're not copying the body. Only the frame headers are rented.
            Assert.True(rentedBytes < size);
        }

        [Fact]
        public async Task TestCopyingBody()
        {
            const int size = 1024;

            QueueDeclareOk q = await _channel.QueueDeclareAsync(string.Empty, false, false, true, false, null);
            byte[] body = GetRandomBody(size);

            uint rentedBytes;

            using (var result = await TrackRentedBytes())
            {
                await _channel.BasicPublishAsync(string.Empty, q, body, copyBody: true);
                rentedBytes = result.RentedBytes;
            }

            Assert.Equal((uint)1, await _channel.QueuePurgeAsync(q));

            // It is expected that the rented bytes is larger than the size of the body
            // since the body is copied with the frame headers.
            Assert.True(rentedBytes >= size);
        }

        [Fact]
        public async Task TestDefaultCopyingBody()
        {
            Assert.Equal(int.MaxValue, _conn.CopyBodyToMemoryThreshold);

            const int size = 1024;

            QueueDeclareOk q = await _channel.QueueDeclareAsync(string.Empty, false, false, true, false, null);
            byte[] body = GetRandomBody(size);

            uint rentedBytes;

            using (var result = await TrackRentedBytes())
            {
                await _channel.BasicPublishAsync(string.Empty, q, body, copyBody: true);
                rentedBytes = result.RentedBytes;
            }

            Assert.Equal((uint)1, await _channel.QueuePurgeAsync(q));

            // It is expected that the rented bytes is larger than the size of the body
            // since the body is copied with the frame headers.
            Assert.True(rentedBytes >= size);
        }
    }
}
