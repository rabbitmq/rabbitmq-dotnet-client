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
                /*
                 * Both are legitimate for a socket closed out of band, since the type
                 * depends on whether the close beat the main loop's next read. .NET
                 * always wraps the ObjectDisposedException in an IOException, but .NET
                 * Framework does not, so on net472 the bare one surfaces. See
                 * ClosingLoopAsync, which likewise catches both.
                 * https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1974
                 */
                Assert.True(ex.InnerException is IOException or ObjectDisposedException,
                    $"unexpected inner exception: {ex.InnerException}");
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
        public async Task TestConcurrentFrameHandlerCloseDoesNotHang_GH1968()
        {
            /*
             * rabbitmq/rabbitmq-dotnet-client#1968
             *
             * SocketFrameHandler.CloseAsync used to dispose _closingSemaphore in its
             * finally block without ever releasing it. A second closer already parked
             * in WaitAsync then stayed pending forever -- a disposed SemaphoreSlim
             * does not fault or cancel its pending async waiters, not even via their
             * own cancellation token.
             *
             * That is reachable in normal operation because MainLoop's FinishCloseAsync
             * is itself a closer. When it lost this race it never returned, so
             * _mainLoopTask never completed and Connection.CloseAsync waited out its
             * full 30s DefaultConnectionCloseTimeout before throwing a bare
             * OperationCanceledException -- the 30-second net472 CI failure in #1968.
             *
             * Two direct closes are the minimal deterministic form of that race.
             */
            var c = (AutorecoveringConnection)_conn;

            ValueTask first = c.CloseFrameHandlerAsync();
            ValueTask second = c.CloseFrameHandlerAsync();

            try
            {
                Task both = Task.WhenAll(first.AsTask(), second.AsTask());
                await both.WaitAsync(_waitSpan);
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
        public async Task TestAbortCancellationWhenMainLoopWinsCloseReasonRace_GH1960()
        {
            /*
             * rabbitmq/rabbitmq-dotnet-client#1960
             *
             * Deterministic version of TestAbortWithSocketClosedOutOfBandAndCancellation.
             * That test only fails when the abort path loses the SetCloseReason race to
             * MainLoop, which in practice needed a cold net472 process (the abort path is
             * not yet JIT-compiled, and that latency is the whole race window). Delaying
             * before AbortAsync makes MainLoop win every time, on every platform and TFM.
             *
             * MainLoop then mints a Library reason carrying _mainLoopCts.Token and invokes
             * the shutdown handlers sequentially. Before the fix, a handler awaiting
             * args.CancellationToken parked on a token that only FinishCloseAsync cancels
             * -- and MainLoop reaches FinishCloseAsync only after the handlers return, so
             * the handler waited out its full delay while MainLoop waited on the handler.
             */
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool completedViaConnectionShutdown = false;

            _channel.ChannelShutdownAsync += async (channel, args) =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), args.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    tcs.TrySetResult(true);
                }
            };

            _conn.ConnectionShutdownAsync += (c, args) =>
            {
                if (tcs.TrySetResult(true))
                {
                    completedViaConnectionShutdown = true;
                }
                return Task.CompletedTask;
            };

            var conn = (AutorecoveringConnection)_conn;
            ValueTask frameHandlerCloseTask = conn.CloseFrameHandlerAsync();

            try
            {
                // Let MainLoop observe the dead socket and win SetCloseReason.
                await Task.Delay(TimeSpan.FromMilliseconds(250));

                await _conn.AbortAsync(cts.Token);
                await WaitAllAsync(tcs, frameHandlerCloseTask);

                /*
                 * The channel handler's own cancellation must be what releases it. If the
                 * ConnectionShutdownAsync fallback completed the TCS instead, the parked
                 * handler was never cancelled and the deadlock is still present.
                 */
                Assert.False(completedViaConnectionShutdown,
                    "channel shutdown handler must be released by its own cancellation token, " +
                    "not by the ConnectionShutdownAsync fallback");
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

        [Fact]
        public async Task TestChannelShutdownFiresOnceWhenChannelClosedBeforeConnection_GH2005()
        {
            /*
             * rabbitmq/rabbitmq-dotnet-client#2005
             *
             * A channel's SessionBase subscribes to Connection.PreConnectionShutdownAsync in
             * its constructor and must unsubscribe in OnSessionShutdownAsync when the channel
             * closes. A regression subscribed to PreConnectionShutdownAsync but unsubscribed
             * from the public ConnectionShutdownAsync, so the unsubscribe was a no-op: a channel
             * closed while its connection stayed open leaked its session handler onto the
             * connection, and that handler re-fired the channel's shutdown when the connection
             * later closed.
             *
             * A non-recovering connection is used so the channel's ChannelShutdownAsync maps
             * directly to the underlying session shutdown, with no recovery wrapper in between.
             * The connection close broadcasts PreConnectionShutdownAsync synchronously (awaited)
             * inside CloseAsync, so any second fire has already happened once CloseAsync returns.
             */
            ConnectionFactory cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = false;

            IConnection conn = await CreateConnectionAsyncWithRetries(cf);
            try
            {
                IChannel channel = await conn.CreateChannelAsync();

                int shutdownCount = 0;
                var firstShutdownTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                channel.ChannelShutdownAsync += (_, _) =>
                {
                    Interlocked.Increment(ref shutdownCount);
                    firstShutdownTcs.TrySetResult(true);
                    return Task.CompletedTask;
                };

                // Close the channel while the connection stays open. This fires the channel
                // shutdown once and must unsubscribe the session from PreConnectionShutdownAsync.
                await channel.CloseAsync();
                await WaitAsync(firstShutdownTcs, "channel shutdown");
                Assert.Equal(1, Volatile.Read(ref shutdownCount));

                // Closing the connection must not re-fire the already-closed channel's shutdown.
                await conn.CloseAsync();
                Assert.Equal(1, Volatile.Read(ref shutdownCount));
            }
            finally
            {
                await conn.DisposeAsync();
            }
        }

        private async Task WaitAllAsync(TaskCompletionSource<bool> tcs, ValueTask frameHandlerCloseTask)
        {
            await WaitAsync(tcs, _waitSpan, "channel shutdown");
            await frameHandlerCloseTask.AsTask().WaitAsync(_waitSpan);
        }
    }
}
