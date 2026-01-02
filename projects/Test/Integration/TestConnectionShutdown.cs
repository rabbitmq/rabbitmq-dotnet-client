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
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestConnectionShutdown : IntegrationFixture
    {
        // default Connection.Abort() timeout and then some
        private readonly TimeSpan _waitSpan = TimeSpan.FromSeconds(6);

        public TestConnectionShutdown(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestCleanClosureWithSocketClosedOutOfBand()
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _channel.ChannelShutdownAsync += (channel, args) =>
            {
                tcs.SetResult(true);
                return Task.CompletedTask;
            };

            var c = (AutorecoveringConnection)_conn;
            ValueTask frameHandlerCloseTask = c.CloseFrameHandlerAsync();
            try
            {
                await _conn.CloseAsync(_waitSpan);
            }
            catch (AlreadyClosedException ex)
            {
                Assert.IsAssignableFrom<IOException>(ex.InnerException);
            }
            catch (ChannelClosedException)
            {
                /*
                 * TODO: ideally we'd not see this exception!
                 */
            }

            try
            {
                await WaitAllAsync(tcs, frameHandlerCloseTask);
            }
            finally
            {
                _conn = null;
                _channel = null;
            }
        }

        [Fact]
        public async Task TestAbortWithSocketClosedOutOfBand()
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _channel.ChannelShutdownAsync += (channel, args) =>
            {
                tcs.SetResult(true);
                return Task.CompletedTask;
            };

            var c = (AutorecoveringConnection)_conn;
            ValueTask frameHandlerCloseTask = c.CloseFrameHandlerAsync();
            try
            {
                await _conn.AbortAsync();
                await WaitAllAsync(tcs, frameHandlerCloseTask);
            }
            finally
            {
                _conn = null;
                _channel = null;
            }
        }

        [Fact]
        public async Task TestAbortWithSocketClosedOutOfBandAndCancellation()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _channel.ChannelShutdownAsync += async (channel, args) =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), args.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    tcs.SetResult(true);
                }
            };

            _conn.ConnectionShutdownAsync += (c, args) =>
            {
                if (tcs.TrySetResult(true))
                {
                    _output.WriteLine("[ERROR] {0}: completed tcs via ConnectionShutdownAsync", _testDisplayName);
                }
                return Task.CompletedTask;
            };

            var c = (AutorecoveringConnection)_conn;
            ValueTask frameHandlerCloseTask = c.CloseFrameHandlerAsync();

            try
            {
                await _conn.AbortAsync(cts.Token);
                await WaitAllAsync(tcs, frameHandlerCloseTask);
            }
            finally
            {
                _conn = null;
                _channel = null;
            }
        }

        [Fact]
        public async Task TestDisposedWithSocketClosedOutOfBand()
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _channel.ChannelShutdownAsync += (channel, args) =>
            {
                tcs.SetResult(true);
                return Task.CompletedTask;
            };

            var c = (AutorecoveringConnection)_conn;
            ValueTask frameHandlerCloseTask = c.CloseFrameHandlerAsync();

            try
            {
                await _conn.DisposeAsync();
                await WaitAsync(tcs, WaitSpan, "channel shutdown");
                await frameHandlerCloseTask.AsTask().WaitAsync(WaitSpan);
            }
            finally
            {
                _conn = null;
                _channel = null;
            }
        }

        [Fact]
        public async Task TestShutdownSignalPropagationToChannels()
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _channel.ChannelShutdownAsync += (channel, args) =>
            {
                tcs.SetResult(true);
                return Task.CompletedTask;
            };

            await _conn.CloseAsync();

            await WaitAsync(tcs, TimeSpan.FromSeconds(3), "channel shutdown");
        }

        [Fact]
        public async Task TestShutdownCancellation()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _conn.ConnectionShutdownAsync += async (channel, args) =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), args.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    tcs.SetResult(true);
                }
            };

            await _conn.CloseAsync(cancellationToken: cts.Token);

            await WaitAsync(tcs, TimeSpan.FromSeconds(3), "connection shutdown");
        }

        [Fact]
        public async Task TestShutdownSignalPropagationWithCancellationToChannels()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _channel.ChannelShutdownAsync += async (channel, args) =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), args.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    tcs.SetResult(true);
                }
            };

            await _conn.CloseAsync(cts.Token);

            await WaitAsync(tcs, TimeSpan.FromSeconds(3), "channel shutdown");
        }

        [Fact]
        public async Task TestShutdownSignalPropagationToChannelsUsingDispose()
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _channel.ChannelShutdownAsync += (channel, args) =>
            {
                tcs.SetResult(true);
                return Task.CompletedTask;
            };

            await _conn.DisposeAsync();
            _conn = null;

            await WaitAsync(tcs, TimeSpan.FromSeconds(3), "channel shutdown");
        }

        [Fact]
        public async Task TestConsumerDispatcherShutdown()
        {
            var m = (AutorecoveringChannel)_channel;
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _channel.ChannelShutdownAsync += (channel, args) =>
            {
                tcs.SetResult(true);
                return Task.CompletedTask;
            };
            Assert.False(m.ConsumerDispatcher.IsShutdown, "dispatcher should NOT be shut down before CloseAsync");
            await _conn.CloseAsync();
            await WaitAsync(tcs, TimeSpan.FromSeconds(3), "channel shutdown");
            Assert.True(m.ConsumerDispatcher.IsShutdown, "dispatcher should be shut down after CloseAsync");
        }

        [Fact]
        public async Task TestDisposeAfterAbort_GH825()
        {
            await _channel.AbortAsync();
            await _channel.DisposeAsync();
        }

        private async Task WaitAllAsync(TaskCompletionSource<bool> tcs, ValueTask frameHandlerCloseTask)
        {
            await WaitAsync(tcs, _waitSpan, "channel shutdown");
            await frameHandlerCloseTask.AsTask().WaitAsync(_waitSpan);
        }
    }
}
