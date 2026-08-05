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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Integration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
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

        // Regression test for https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1993
        //
        // The bug: when the basic.consume issued during consumer recovery runs past
        // ContinuationTimeout, its continuation completes as *cancelled*, so it reaches
        // HandleTopologyRecoveryException as an OperationCanceledException rather than a
        // TimeoutException. That type was not in the retry list, so the exception was swallowed and
        // recovery reported success. Meanwhile the broker had registered the consumer, but the
        // client never added it to the new channel's ConsumerDispatcher, so every subsequent
        // delivery resolved to the FallbackConsumer and was dropped unacked. The queue silently
        // stopped being consumed, on a connection and channel that both still report IsOpen.
        //
        // The fix: classify OperationCanceledException as retryable unless recovery itself was
        // cancelled, so the recovery attempt fails, the inner connection is aborted (clearing the
        // broker-side consumer), and the recovery loop tries again.
        //
        // Test strategy: stall only the recovery basic.consume, not the initial connect.
        //   1. Connect and consume normally through the proxy.
        //   2. Disable the proxy to sever the connection, then re-enable it so recovery can
        //      reconnect and get as far as recovering the consumer.
        //   3. The RecoveringConsumerAsync event fires immediately before
        //      RecordedConsumer.RecoverAsync, so the handler adds a Rate=0 downstream
        //      BandwidthToxic there. The recovery basic.consume goes out but its consume-ok can
        //      never come back, so it hits the 2 s ContinuationTimeout: exactly the #1993 trigger.
        //   4. Remove the toxic so the *next* recovery attempt can succeed.
        //   5. Assert deliveries resume. Before the fix, recovery reported success with the
        //      consumer never wired up and no message was ever delivered.
        [SkippableFact]
        [Trait("Category", "Toxiproxy")]
        public async Task TestConsumerRecoveryContinuationTimeoutIsRetried_GH1993()
        {
            Skip.IfNot(AreToxiproxyTestsEnabled, "RABBITMQ_TOXIPROXY_TESTS is not set, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Endpoint = new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), _proxyPort);
            cf.AutomaticRecoveryEnabled = true;
            cf.TopologyRecoveryEnabled = true;
            cf.NetworkRecoveryInterval = TimeSpan.FromSeconds(1);
            // Long enough that the initial connect and the recovery handshake complete
            // comfortably, short enough that the stalled recovery basic.consume gives up
            // well inside WaitSpan.
            cf.ContinuationTimeout = TimeSpan.FromSeconds(2);
            // Keep the broker from closing the connection while the toxic is in place.
            cf.RequestedHeartbeat = TimeSpan.FromSeconds(600);

            await using IConnection conn = await cf.CreateConnectionAsync();
            await using IChannel ch = await conn.CreateChannelAsync();

            // A non-zero prefetch is what makes the original bug permanent: the broker waits
            // forever for acks that the dropped deliveries can never produce.
            await ch.BasicQosAsync(0, 1, false);

            // Durable and non-exclusive: this broker rejects transient non-exclusive queues
            // (the deprecated transient_nonexcl_queues feature), and an exclusive queue would be
            // deleted and re-declared underneath the two recovery attempts this test drives.
            QueueDeclareOk q = await ch.QueueDeclareAsync(GenerateQueueName(), true, false, false);

            try
            {
                await RunConsumerRecoveryContinuationTimeoutBodyAsync(cf, conn, ch, q);
            }
            finally
            {
                // The queue is durable, so it outlives the connection. Delete it even when the
                // test fails, otherwise a failing run leaves a queue behind on the broker.
                try
                {
                    await ch.QueueDeleteAsync(q.QueueName);
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"[WARNING] could not delete queue {q.QueueName}: {ex}");
                }
            }

            await ch.CloseAsync();
            await conn.CloseAsync();
        }

        private async Task RunConsumerRecoveryContinuationTimeoutBodyAsync(ConnectionFactory cf,
            IConnection conn, IChannel ch, QueueDeclareOk q)
        {
            var deliveredAfterRecoveryTcs =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var recoverySucceededTcs =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var consumer = new AsyncEventingBasicConsumer(ch);
            consumer.ReceivedAsync += async (o, ea) =>
            {
                await ch.BasicAckAsync(ea.DeliveryTag, false);
                deliveredAfterRecoveryTcs.TrySetResult(true);
            };
            await ch.BasicConsumeAsync(q.QueueName, false, consumer);

            conn.RecoverySucceededAsync += (o, ea) =>
            {
                recoverySucceededTcs.TrySetResult(true);
                return Task.CompletedTask;
            };

            // Stall the recovery basic.consume the first time a consumer is recovered. Rate=0
            // downstream means the consume frame reaches the broker but the consume-ok never
            // reaches the client, so the continuation times out.
            string toxicName = $"rmq-recovery-consume-bandwidth-{Now}-{GenerateShortUuid()}";
            int toxicAdded = 0;
            int consumerRecoveryAttempts = 0;
            ((RabbitMQ.Client.Framing.AutorecoveringConnection)conn).RecoveringConsumerAsync += async (o, ea) =>
            {
                Interlocked.Increment(ref consumerRecoveryAttempts);

                if (Interlocked.CompareExchange(ref toxicAdded, 1, 0) != 0)
                {
                    // Only the first recovery attempt is sabotaged; later attempts must succeed.
                    return;
                }

                var bandwidthToxic = new BandwidthToxic
                {
                    Name = toxicName,
                    Toxicity = 1.0,
                    Stream = ToxicDirection.DownStream
                };
                bandwidthToxic.Attributes.Rate = 0;
                await _toxiproxyManager.AddToxicAsync(bandwidthToxic);
            };

            // Sever the connection, then restore it so recovery can proceed.
            await _toxiproxyManager.DisableAsync();
            await Task.Delay(TimeSpan.FromSeconds(1));
            await _toxiproxyManager.EnableAsync();

            // Wait until the sabotaged recovery attempt has run, then clear the toxic so the
            // retry can complete. Without the fix, no retry happens: recovery reports success
            // after swallowing the cancellation.
            while (Volatile.Read(ref toxicAdded) == 0)
            {
                await Task.Delay(100);
            }

            // The stalled basic.consume must be given time to hit ContinuationTimeout before the
            // toxic is removed, otherwise the consume-ok arrives in time and the test is vacuous.
            await Task.Delay(cf.ContinuationTimeout + TimeSpan.FromSeconds(1));
            await _toxiproxyManager.RemoveToxicAsync(toxicName);

            await recoverySucceededTcs.Task.WaitAsync(WaitSpan);

            // The real assertion: the recovered consumer is actually wired into the new channel's
            // ConsumerDispatcher. Before the fix this publish was delivered to the
            // FallbackConsumer and dropped, so this wait timed out.
            await ch.BasicPublishAsync("", q.QueueName, GetRandomBody(64));
            await deliveredAfterRecoveryTcs.Task.WaitAsync(WaitSpan);

            Assert.True(await deliveredAfterRecoveryTcs.Task);

            // Guard against a vacuous pass. Delivery could also resume if the first recovery
            // attempt had simply succeeded (toxic added too late to stall the consume), in which
            // case this test would prove nothing about the retry. Consumer recovery must have been
            // attempted at least twice: once sabotaged, then again after the retry.
            Assert.True(Volatile.Read(ref consumerRecoveryAttempts) >= 2,
                $"expected at least 2 consumer recovery attempts, saw {Volatile.Read(ref consumerRecoveryAttempts)}: " +
                "the first attempt was not stalled, so the retry path was never exercised");
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
