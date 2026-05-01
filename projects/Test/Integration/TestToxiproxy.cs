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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Integration;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Toxiproxy.Net.Toxics;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestToxiproxy : IntegrationFixture
    {
        private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(1);
        private ToxiproxyManager _toxiproxyManager;
        private int _proxyPort;
        private string _proxyHost;

        public TestToxiproxy(ITestOutputHelper output) : base(output)
        {
        }

        public override Task InitializeAsync()
        {
            // NB: nothing to do here since each test creates its own factory,
            // connections and channels
            Assert.Null(_connFactory);
            Assert.Null(_conn);
            Assert.Null(_channel);

            if (AreToxiproxyTestsEnabled)
            {
                _toxiproxyManager = new ToxiproxyManager(_testDisplayName, IsRunningInCI, IsWindows);
                _proxyPort = ToxiproxyManager.ProxyPort;
                _proxyHost = _toxiproxyManager.ProxyHost;
                return _toxiproxyManager.InitializeAsync();
            }
            else
            {
                return Task.CompletedTask;
            }
        }

        public override async Task DisposeAsync()
        {
            if (AreToxiproxyTestsEnabled)
            {
                await _toxiproxyManager.DisposeAsync();
            }

            await base.DisposeAsync();
        }

        [SkippableFact]
        [Trait("Category", "Toxiproxy")]
        public async Task TestCloseConnection()
        {
            Skip.IfNot(AreToxiproxyTestsEnabled, "RABBITMQ_TOXIPROXY_TESTS is not set, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Port = _proxyPort;
            cf.AutomaticRecoveryEnabled = true;
            cf.NetworkRecoveryInterval = TimeSpan.FromSeconds(1);
            cf.RequestedHeartbeat = TimeSpan.FromSeconds(1);

            var messagePublishedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var connectionShutdownTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var recoverySucceededTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var testSucceededTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            Task pubTask = Task.Run(async () =>
            {
                await using IConnection conn = await cf.CreateConnectionAsync();
                conn.CallbackExceptionAsync += (s, ea) =>
                {
                    _output.WriteLine($"[ERROR] unexpected callback exception {ea.Detail} {ea.Exception}");
                    recoverySucceededTcs.SetResult(false);
                    return Task.CompletedTask;
                };

                conn.ConnectionRecoveryErrorAsync += (s, ea) =>
                {
                    _output.WriteLine($"[ERROR] connection recovery error {ea.Exception}");
                    recoverySucceededTcs.SetResult(false);
                    return Task.CompletedTask;
                };

                conn.ConnectionShutdownAsync += (s, ea) =>
                {
                    if (IsVerbose)
                    {
                        _output.WriteLine($"[INFO] connection shutdown");
                    }

                    /*
                     * Note: using TrySetResult because this callback will be called when the
                     * test exits, and connectionShutdownTcs will have already been set
                     */
                    connectionShutdownTcs.TrySetResult(true);
                    return Task.CompletedTask;
                };

                conn.RecoverySucceededAsync += (s, ea) =>
                {
                    if (IsVerbose)
                    {
                        _output.WriteLine($"[INFO] connection recovery succeeded");
                    }

                    recoverySucceededTcs.SetResult(true);
                    return Task.CompletedTask;
                };

                async Task PublishLoop()
                {
                    await using IChannel ch = await conn.CreateChannelAsync(_createChannelOptions);
                    QueueDeclareOk q = await ch.QueueDeclareAsync();
                    while (conn.IsOpen)
                    {
                        /*
                         * Note:
                         * In this test, it is possible that the connection
                         * will be closed before the ack is returned,
                         * and this await will throw an exception
                         */
                        try
                        {
                            await ch.BasicPublishAsync("", q.QueueName, GetRandomBody());
                            messagePublishedTcs.TrySetResult(true);
                        }
                        catch (AlreadyClosedException ex)
                        {
                            if (IsVerbose)
                            {
                                _output.WriteLine($"[WARNING] BasicPublishAsync ex: {ex}");
                            }
                        }
                    }

                    await ch.CloseAsync();
                }

                try
                {
                    await PublishLoop();
                }
                catch (Exception ex)
                {
                    if (IsVerbose)
                    {
                        _output.WriteLine($"[WARNING] PublishLoop ex: {ex}");
                    }
                }

                Assert.True(await testSucceededTcs.Task);
                await conn.CloseAsync();
            });

            Assert.True(await messagePublishedTcs.Task);

            Task disableProxyTask = _toxiproxyManager.DisableAsync();

            await Task.WhenAll(disableProxyTask, connectionShutdownTcs.Task);

            Task enableProxyTask = _toxiproxyManager.EnableAsync();

            Task whenAllTask = Task.WhenAll(enableProxyTask, recoverySucceededTcs.Task);
            await whenAllTask.WaitAsync(TimeSpan.FromSeconds(15));

            Assert.True(await recoverySucceededTcs.Task);

            testSucceededTcs.SetResult(true);
            await pubTask;
        }

        [SkippableFact]
        [Trait("Category", "Toxiproxy")]
        public async Task TestThatStoppedSocketResultsInHeartbeatTimeout()
        {
            Skip.IfNot(AreToxiproxyTestsEnabled, "RABBITMQ_TOXIPROXY_TESTS is not set, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Port = _proxyPort;
            cf.RequestedHeartbeat = _heartbeatTimeout;
            cf.AutomaticRecoveryEnabled = false;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            Task pubTask = Task.Run(async () =>
            {
                await using IConnection conn = await cf.CreateConnectionAsync();
                await using IChannel ch = await conn.CreateChannelAsync(_createChannelOptions);
                QueueDeclareOk q = await ch.QueueDeclareAsync();
                while (conn.IsOpen)
                {
                    await ch.BasicPublishAsync("", q.QueueName, GetRandomBody());
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    tcs.TrySetResult(true);
                }

                await ch.CloseAsync();
                await conn.CloseAsync();
            });

            Assert.True(await tcs.Task);

            string toxicName = $"rmq-localhost-timeout-{Now}-{GenerateShortUuid()}";
            var timeoutToxic = new TimeoutToxic
            {
                Name = toxicName
            };
            timeoutToxic.Attributes.Timeout = 0;
            timeoutToxic.Toxicity = 1.0;

            Task<TimeoutToxic> addToxicTask = _toxiproxyManager.AddToxicAsync(timeoutToxic);

            await Assert.ThrowsAsync<AlreadyClosedException>(() =>
            {
                return Task.WhenAll(addToxicTask, pubTask);
            });
        }

        [SkippableFact]
        [Trait("Category", "Toxiproxy")]
        public async Task TestTcpReset_GH1464()
        {
            Skip.IfNot(AreToxiproxyTestsEnabled, "RABBITMQ_TOXIPROXY_TESTS is not set, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Endpoint = new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), _proxyPort);
            cf.RequestedHeartbeat = TimeSpan.FromSeconds(5);
            cf.AutomaticRecoveryEnabled = true;

            var channelCreatedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var connectionShutdownTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            Task recoveryTask = Task.Run(async () =>
            {
                await using IConnection conn = await cf.CreateConnectionAsync();
                conn.ConnectionShutdownAsync += (o, ea) =>
                {
                    connectionShutdownTcs.SetResult(true);
                    return Task.CompletedTask;
                };

                await using (IChannel ch = await conn.CreateChannelAsync())
                {
                    channelCreatedTcs.SetResult(true);
                    await WaitForRecoveryAsync(conn);
                    await ch.CloseAsync();
                }

                await conn.CloseAsync();
            });

            Assert.True(await channelCreatedTcs.Task);

            string toxicName = $"rmq-localhost-reset_peer-{Now}-{GenerateShortUuid()}";
            var resetPeerToxic = new ResetPeerToxic
            {
                Name = toxicName
            };
            resetPeerToxic.Attributes.Timeout = 500;
            resetPeerToxic.Toxicity = 1.0;

            Task<ResetPeerToxic> addToxicTask = _toxiproxyManager.AddToxicAsync(resetPeerToxic);

            await Task.WhenAll(addToxicTask, connectionShutdownTcs.Task);

            await _toxiproxyManager.RemoveToxicAsync(toxicName);

            await recoveryTask;
        }

        [SkippableFact]
        [Trait("Category", "Toxiproxy")]
        public async Task TestPublisherConfirmationThrottling()
        {
            Skip.IfNot(AreToxiproxyTestsEnabled, "RABBITMQ_TOXIPROXY_TESTS is not set, skipping test");

            const int TotalMessageCount = 64;
            const int MaxOutstandingConfirms = 8;
            const int BatchSize = MaxOutstandingConfirms * 2;

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Endpoint = new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), _proxyPort);
            cf.RequestedHeartbeat = TimeSpan.FromSeconds(5);
            cf.AutomaticRecoveryEnabled = true;

            var channelOpts = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true,
                outstandingPublisherConfirmationsRateLimiter: new ThrottlingRateLimiter(MaxOutstandingConfirms)
            );

            var channelCreatedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var messagesPublishedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            long publishCount = 0;
            Task publishTask = Task.Run(async () =>
            {
                await using (IConnection conn = await cf.CreateConnectionAsync())
                {
                    await using (IChannel ch = await conn.CreateChannelAsync(channelOpts))
                    {
                        QueueDeclareOk q = await ch.QueueDeclareAsync();

                        channelCreatedTcs.SetResult(true);

                        try
                        {
                            var publishBatch = new List<ValueTask>();
                            while (publishCount < TotalMessageCount)
                            {
                                for (int i = 0; i < BatchSize; i++)
                                {
                                    publishBatch.Add(ch.BasicPublishAsync("", q.QueueName, GetRandomBody()));
                                }

                                foreach (ValueTask pt in publishBatch)
                                {
                                    await pt;
                                    Interlocked.Increment(ref publishCount);
                                }

                                publishBatch.Clear();
                            }

                            messagesPublishedTcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            messagesPublishedTcs.SetException(ex);
                        }
                    }
                }
            });

            await channelCreatedTcs.Task;

            string toxicName = $"rmq-localhost-bandwidth-{Now}-{GenerateShortUuid()}";
            var bandwidthToxic = new BandwidthToxic
            {
                Name = toxicName
            };
            bandwidthToxic.Attributes.Rate = 0;
            bandwidthToxic.Toxicity = 1.0;
            bandwidthToxic.Stream = ToxicDirection.DownStream;

            await Task.Delay(TimeSpan.FromSeconds(1));

            Task<BandwidthToxic> addToxicTask = _toxiproxyManager.AddToxicAsync(bandwidthToxic);

            while (true)
            {
                long publishCount0 = Interlocked.Read(ref publishCount);
                await Task.Delay(TimeSpan.FromSeconds(5));
                long publishCount1 = Interlocked.Read(ref publishCount);

                if (publishCount0 == publishCount1)
                {
                    // Publishing has "settled" due to being blocked
                    break;
                }
            }

            await addToxicTask.WaitAsync(WaitSpan);
            await _toxiproxyManager.RemoveToxicAsync(toxicName).WaitAsync(WaitSpan);

            await messagesPublishedTcs.Task.WaitAsync(WaitSpan);
            await publishTask.WaitAsync(WaitSpan);

            Assert.Equal(TotalMessageCount, publishCount);
        }

        [SkippableFact]
        [Trait("Category", "Toxiproxy")]
        public async Task TestRpcContinuationTimeout_GH1802()
        {
            Skip.IfNot(AreToxiproxyTestsEnabled, "RABBITMQ_TOXIPROXY_TESTS is not set, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Endpoint = new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), _proxyPort);
            cf.ContinuationTimeout = TimeSpan.FromSeconds(1);
            cf.AutomaticRecoveryEnabled = false;
            cf.TopologyRecoveryEnabled = false;

            await using IConnection conn = await cf.CreateConnectionAsync();
            await using IChannel ch = await conn.CreateChannelAsync();

            string toxicName = $"rmq-localhost-bandwidth-{Now}-{GenerateShortUuid()}";
            var bandwidthToxic = new BandwidthToxic
            {
                Name = toxicName
            };
            bandwidthToxic.Attributes.Rate = 0;
            bandwidthToxic.Toxicity = 1.0;
            bandwidthToxic.Stream = ToxicDirection.DownStream;

            Task<BandwidthToxic> addToxicTask = _toxiproxyManager.AddToxicAsync(bandwidthToxic);

            await Task.Delay(TimeSpan.FromSeconds(1));

            bool sawContinuationTimeout = false;
            try
            {
                ch.ContinuationTimeout = TimeSpan.FromMilliseconds(5);
                QueueDeclareOk q = await ch.QueueDeclareAsync();
            }
            catch (OperationCanceledException)
            {
                sawContinuationTimeout = true;
            }

            await _toxiproxyManager.RemoveToxicAsync(toxicName);

            await ch.CloseAsync();

            Assert.True(sawContinuationTimeout);
        }

        // Regression test for https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1930
        //
        // The bug: when WriteLoopAsync crashes (socket error) without calling
        // _channelWriter.TryComplete(ex), publishers blocked on a full
        // Channel<OutgoingFrame>(128) hold _confirmSemaphore(1,1)
        // indefinitely.  The connection-shutdown handler
        // (MaybeHandlePublisherConfirmationTcsOnChannelShutdownAsync) needs
        // the same semaphore and deadlocks, so BasicPublishAsync never throws
        // or returns — the publisher hangs forever.
        //
        // The fix: _channelWriter.TryComplete(ex) in the write-loop catch →
        // blocked WriteAsync callers receive ChannelClosedException →
        // semaphore released → shutdown handler proceeds → all publishers complete.
        //
        // Test strategy (Toxiproxy):
        //   1. BandwidthToxic(Rate=1, UpStream) throttles the write path to
        //      1 KB/s.  The OS TCP send buffer fills, FlushAsync stalls, the
        //      128-slot outgoing channel fills, and one publisher blocks on
        //      WriteAsync while holding _confirmSemaphore — the exact
        //      pre-condition for the deadlock.
        //   2. cf.SocketFlushTimeout = 5 s causes the stalled
        //      PipeWriter.FlushAsync to throw OperationCanceledException
        //      after 5 s, crashing the write loop while the read loop remains alive.
        //      Because _closeReason is not set at crash time, publishers 130+
        //      can re-fill the channel — the exact condition that triggers the deadlock
        //      without the fix.
        //   3. Task.WhenAll(publishTasks).WaitAsync(15 s) must complete within
        //      the timeout.
        //
        // Why this approach is reliable on all platforms:
        //   A CancellationToken passed to PipeWriter.FlushAsync fires
        //   deterministically after 5 s on Windows and Linux alike, regardless of how
        //   the OS handles RST/FIN signals.  This makes the test a true regression guard
        //   on every CI platform.
        [SkippableFact]
        [Trait("Category", "Toxiproxy")]
        public async Task TestPublishDoesNotHangAfterConnectionLoss_WithConfirms_GH1930()
        {
            Skip.IfNot(AreToxiproxyTestsEnabled, "RABBITMQ_TOXIPROXY_TESTS is not set, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.HostName = _proxyHost;
            cf.Port = _proxyPort;
            cf.AutomaticRecoveryEnabled = false;
            // RabbitMQ negotiates heartbeat = min(client, server_default=60s). 600 s keeps
            // the effective value at 60 s which is well above the 15 s WaitAsync timeout,
            // so the server will not close the connection while the upstream bandwidth toxic
            // is throttling writes.
            cf.RequestedHeartbeat = TimeSpan.FromSeconds(600);
            // Short continuation timeout so that channel/connection disposal completes
            // quickly once we close the proxy (the server will never send close-ok).
            cf.ContinuationTimeout = TimeSpan.FromSeconds(5);
            // Crash the write loop after 5 s of stall instead of sending a TCP RST.
            // PipeWriter.FlushAsync(CancellationToken) cancels on all platforms
            // (Windows and Linux), making this a deterministic regression guard.
            cf.SocketFlushTimeout = TimeSpan.FromSeconds(5);

            // publisherConfirmationTrackingEnabled: false matches the exact scenario
            // described in the issue where the semaphore deadlock was observed.
            var channelOpts = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: false);

            await using IConnection conn = await cf.CreateConnectionAsync();
            await using IChannel ch = await conn.CreateChannelAsync(channelOpts);
            QueueDeclareOk q = await ch.QueueDeclareAsync();

            // No timeout yet – we start the cleanup timer only after the connection is
            // severed so that it is always strictly longer than the WaitAsync timeout.
            using var cts = new CancellationTokenSource();

            // Throttle upstream writes to 1 KB/s.
            //
            // Toxiproxy reads client data at 1 KB/s so its receive window shrinks to zero
            // quickly.  Once the OS TCP send buffer is full, the write loop's
            // PipeWriter.FlushAsync stalls.  This guarantees the bounded
            // Channel<OutgoingFrame> (128 slots) fills up and a publisher holds
            // _confirmSemaphore(1,1) while blocked on WriteAsync – the exact deadlock
            // pre-condition.
            string bandwidthToxicName = $"rmq-upstream-bandwidth-{Now}-{GenerateShortUuid()}";
            var bandwidthToxic = new BandwidthToxic
            {
                Name = bandwidthToxicName,
                Toxicity = 1.0,
                Stream = ToxicDirection.UpStream,
            };
            bandwidthToxic.Attributes.Rate = 1; // 1 KB/s – reliably stalls the write loop
            await _toxiproxyManager.AddToxicAsync(bandwidthToxic);

            // With confirms enabled, _confirmSemaphore(1,1) serialises publishes one
            // at a time. Publishers fill the 128-slot channel almost instantly while the
            // stalled write loop cannot drain it. The publisher that encounters a full
            // channel blocks on WriteAsync while holding the semaphore; every subsequent
            // publisher queues behind that semaphore.
            const int PublishCount = 512;
            byte[] body = GetRandomBody(ushort.MaxValue); // 65 KB – fills OS socket buffer in a few frames
            var publishTasks = new List<Task>(PublishCount);
            for (int i = 0; i < PublishCount; i++)
            {
                publishTasks.Add(PublishIgnoringErrorsAsync(ch, q.QueueName, body, cts.Token));
            }

            // Wait until the completed-task count stabilises for at least 5 consecutive
            // 100 ms polls.  Once it is stable we know:
            //  - the write loop's FlushAsync is blocked (Rate=1 exhausted OS buffers), and
            //  - a publisher is blocked on _channelWriter.WriteAsync while holding the
            //    semaphore (the exact deadlock pre-condition).
            // A pure fixed-delay would be weaker: tasks could all complete before we
            // trigger the RST, making the test vacuous.
            int prevDone = -1, stablePolls = 0;
            while (stablePolls < 5)
            {
                await Task.Delay(100);
                int done = publishTasks.Count(t => t.IsCompleted);
                if (done == prevDone)
                {
                    stablePolls++;
                }
                else
                {
                    stablePolls = 0;
                    prevDone = done;
                }
            }

            // Guard: publishers must still be blocked. If all completed the test is vacuous.
            // With confirms enabled _confirmSemaphore(1,1) serialises publishes one at a
            // time. Once the write loop is stalled and the 128-slot channel is full, the
            // publisher holding the semaphore is blocked on WriteAsync and every subsequent
            // publisher is blocked on semaphore acquisition. With PublishCount=512 we
            // therefore expect at least PublishCount/2 tasks to be blocked at this point.
            int blockedCount = publishTasks.Count(t => !t.IsCompleted);
            Assert.True(blockedCount >= PublishCount / 2,
                $"{blockedCount}/{PublishCount} tasks are blocked before flush timeout – the write loop " +
                $"may not have stalled. Increase PublishCount or body size.");

            // Use a test-local timeout that is short enough to keep CI fast but long
            // enough for the flush timeout + fix propagation to complete (5 s flush
            // timeout → write-loop crash → TryComplete → publishers complete), typically
            // < 8 s in practice.
            var publishCompletionTimeout = TimeSpan.FromSeconds(15);

            // The cleanup CTS fires strictly after WaitAsync times out, ensuring the
            // deadlock (without fix) is observable as TimeoutException, not silently
            // broken by cancellation.
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            try
            {
                // With the fix: TryComplete(ex) is called on write-loop crash, the
                // blocked WriteAsync throws ChannelClosedException immediately, the
                // semaphore is released, and all remaining publishers complete within
                // milliseconds.
                //
                // Without the fix: the semaphore is never released, the shutdown handler
                // deadlocks, and Task.WhenAll never completes → TimeoutException.
                await Task.WhenAll(publishTasks).WaitAsync(publishCompletionTimeout);
            }
            finally
            {
                // Cancel immediately so that any stuck publishers are unblocked before
                // channel and connection disposal, keeping teardown fast.
                cts.Cancel();
            }
        }

        private static async Task PublishIgnoringErrorsAsync(IChannel ch, string queueName, byte[] body,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await ch.BasicPublishAsync("", queueName, body, cancellationToken);
            }
            catch
            {
                // Exception is expected when the connection is cut or the token fires.
            }
        }

        private bool AreToxiproxyTestsEnabled
        {
            get
            {
                string s = Environment.GetEnvironmentVariable("RABBITMQ_TOXIPROXY_TESTS");

                if (string.IsNullOrEmpty(s))
                {
                    return false;
                }

                if (bool.TryParse(s, out bool enabled))
                {
                    return enabled;
                }

                return false;
            }
        }
    }
}
