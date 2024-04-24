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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test
{
    public abstract class IntegrationFixture : IAsyncLifetime
    {
        private static readonly bool s_isRunningInCI = false;
        private static readonly bool s_isVerbose = false;
        private static int _connectionIdx = 0;

        protected readonly RabbitMQCtl _rabbitMQCtl;

        protected ConnectionFactory _connFactory;
        protected IConnection _conn;
        protected IChannel _channel;

        protected static readonly Encoding _encoding = new UTF8Encoding();
        protected static readonly int _processorCount = Environment.ProcessorCount;

        protected readonly ITestOutputHelper _output;
        protected readonly string _testDisplayName;

        protected readonly bool _dispatchConsumersAsync = false;
        protected readonly ushort _consumerDispatchConcurrency = 1;
        protected readonly bool _openChannel = true;

        public static readonly TimeSpan WaitSpan;
        public static readonly TimeSpan LongWaitSpan;
        public static readonly TimeSpan RecoveryInterval = TimeSpan.FromSeconds(2);
        public static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan RequestedConnectionTimeout = TimeSpan.FromSeconds(1);
        public static readonly Random S_Random;

        static IntegrationFixture()
        {
            S_Random = new Random();
            s_isRunningInCI = InitIsRunningInCI();
            s_isVerbose = InitIsVerbose();

            if (s_isRunningInCI)
            {
                WaitSpan = TimeSpan.FromSeconds(60);
                LongWaitSpan = TimeSpan.FromSeconds(120);
                RequestedConnectionTimeout = TimeSpan.FromSeconds(4);
            }
            else
            {
                WaitSpan = TimeSpan.FromSeconds(30);
                LongWaitSpan = TimeSpan.FromSeconds(60);
            }
        }

        public IntegrationFixture(ITestOutputHelper output,
            bool dispatchConsumersAsync = false,
            ushort consumerDispatchConcurrency = 1,
            bool openChannel = true)
        {
            _dispatchConsumersAsync = dispatchConsumersAsync;
            _consumerDispatchConcurrency = consumerDispatchConcurrency;
            _openChannel = openChannel;
            _output = output;

            _rabbitMQCtl = new RabbitMQCtl(_output);

            Type type = _output.GetType();
            FieldInfo testMember = type.GetField("test", BindingFlags.Instance | BindingFlags.NonPublic);
            ITest test = (ITest)testMember.GetValue(output);
            _testDisplayName = test.DisplayName
                .Replace("Test.", string.Empty)
                .Replace("Integration.", "I.")
                .Replace("SequentialI.", "SI.");

            if (IsVerbose)
            {
                Console.SetOut(new TestOutputWriter(output, _testDisplayName));
            }
        }

        public virtual async Task InitializeAsync()
        {
            /*
             * https://github.com/rabbitmq/rabbitmq-dotnet-client/commit/120f9bfce627f704956e1008d095b853b459d45b#r135400345
             * 
             * Integration tests must use CreateConnectionFactory so that ClientProvidedName is set for the connection.
             * Tests that close connections via `rabbitmqctl` depend on finding the connection PID via its name.
             */
            if (_connFactory == null)
            {
                _connFactory = CreateConnectionFactory();
                _connFactory.DispatchConsumersAsync = _dispatchConsumersAsync;
                _connFactory.ConsumerDispatchConcurrency = _consumerDispatchConcurrency;
            }

            if (_conn == null)
            {
                _conn = await CreateConnectionAsyncWithRetries(_connFactory);

                if (_openChannel)
                {
                    _channel = await _conn.CreateChannelAsync();
                }

                if (IsVerbose)
                {
                    AddCallbackHandlers();
                }
            }

            if (_connFactory.AutomaticRecoveryEnabled)
            {
                Assert.IsType<AutorecoveringConnection>(_conn);
            }
            else
            {
                Assert.IsType<Connection>(_conn);
            }
        }

        public virtual async Task DisposeAsync()
        {
            try
            {
                if (_channel != null && _channel.IsOpen)
                {
                    await _channel.CloseAsync();
                }

                if (_conn != null && _conn.IsOpen)
                {
                    await _conn.CloseAsync();
                }
            }
            finally
            {
                _channel?.Dispose();
                _conn?.Dispose();
                _channel = null;
                _conn = null;
            }
        }

        protected virtual void AddCallbackHandlers()
        {
            if (_conn != null)
            {
                _conn.CallbackException += (o, ea) =>
                {
                    _output.WriteLine("{0} connection callback exception: {1}",
                        _testDisplayName, ea.Exception);
                };

                _conn.ConnectionShutdown += (o, ea) =>
                {
                    HandleConnectionShutdown(_conn, ea, (args) =>
                    {
                        _output.WriteLine("{0} connection shutdown, args: {1}",
                            _testDisplayName, args);
                    });
                };
            }

            if (_channel != null)
            {
                _channel.CallbackException += (o, ea) =>
                {
                    _output.WriteLine("{0} channel callback exception: {1}",
                        _testDisplayName, ea.Exception);
                };

                _channel.ChannelShutdown += (o, ea) =>
                {
                    HandleChannelShutdown(_channel, ea, (args) =>
                    {
                        _output.WriteLine("{0} channel shutdown, args: {1}",
                            _testDisplayName, args);
                    });
                };
            }
        }

        protected static bool IsRunningInCI
        {
            get { return s_isRunningInCI; }
        }

        protected static bool IsWindows
        {
            get { return Util.IsWindows; }
        }

        protected static bool IsVerbose
        {
            get { return s_isVerbose; }
        }

        internal Task<AutorecoveringConnection> CreateAutorecoveringConnectionAsync(IEnumerable<string> hostnames, bool expectException = false)
        {
            return CreateAutorecoveringConnectionAsync(hostnames, RequestedConnectionTimeout, RecoveryInterval, expectException);
        }

        internal async Task<AutorecoveringConnection> CreateAutorecoveringConnectionAsync(IEnumerable<string> hostnames,
            TimeSpan requestedConnectionTimeout, TimeSpan networkRecoveryInterval, bool expectException = false)
        {
            if (hostnames is null)
            {
                throw new ArgumentNullException(nameof(hostnames));
            }

            ConnectionFactory cf = CreateConnectionFactory();

            cf.AutomaticRecoveryEnabled = true;
            // tests that use this helper will likely list unreachable hosts;
            // make sure we time out quickly on those
            cf.RequestedConnectionTimeout = requestedConnectionTimeout;
            cf.NetworkRecoveryInterval = networkRecoveryInterval;

            IConnection conn = await CreateConnectionAsyncWithRetries(cf, hostnames, expectException);
            return (AutorecoveringConnection)conn;
        }

        protected async Task<IConnection> CreateConnectionAsyncWithRetries(ConnectionFactory connectionFactory,
            IEnumerable<string> hostnames = null, bool expectException = false)
        {
            bool shouldRetry = IsWindows;
            ushort tries = 0;

            do
            {
                try
                {
                    if (hostnames is null)
                    {
                        return await connectionFactory.CreateConnectionAsync();
                    }
                    else
                    {
                        return await connectionFactory.CreateConnectionAsync(hostnames);
                    }
                }
                catch (BrokerUnreachableException ex)
                {
                    if (expectException)
                    {
                        throw;
                    }
                    else
                    {
                        IOException ioex = null;

                        if (ex.InnerException is AggregateException agex0)
                        {
                            AggregateException agex1 = agex0.Flatten();
                            ioex = agex1.InnerExceptions.Where(iex => iex is IOException).FirstOrDefault() as IOException;
                        }

                        if (ioex == null)
                        {
                            ioex = ex.InnerException as IOException;
                        }

                        if (ioex is null)
                        {
                            throw;
                        }
                        else
                        {
                            if (ioex.InnerException is SocketException)
                            {
                                tries++;
                                _output.WriteLine($"WARNING: {nameof(CreateConnectionAsyncWithRetries)} retrying ({tries}), caught exception: {ioex.InnerException}");
                            }
                            else
                            {
                                throw;
                            }
                        }
                    }
                }
            }
            while (shouldRetry && tries < 5);

            Assert.Fail($"FAIL: {nameof(CreateConnectionAsyncWithRetries)} could not open connection");
            return null;
        }

        protected async Task WithTemporaryChannelAsync(Func<IChannel, Task> action)
        {
            IChannel channel = await _conn.CreateChannelAsync();
            try
            {
                await action(channel);
            }
            finally
            {
                await channel.AbortAsync();
            }
        }

        protected string GenerateExchangeName()
        {
            return $"{_testDisplayName}-exchange-{Guid.NewGuid()}";
        }

        protected string GenerateQueueName()
        {
            return $"{_testDisplayName}-queue-{Guid.NewGuid()}";
        }

        protected Task WithTemporaryNonExclusiveQueueAsync(Func<IChannel, string, Task> action)
        {
            return WithTemporaryNonExclusiveQueueAsync(_channel, action);
        }

        protected Task WithTemporaryNonExclusiveQueueAsync(IChannel channel, Func<IChannel, string, Task> action)
        {
            return WithTemporaryNonExclusiveQueueAsync(channel, action, GenerateQueueName());
        }

        protected async Task WithTemporaryNonExclusiveQueueAsync(IChannel channel, Func<IChannel, string, Task> action, string queue)
        {
            try
            {
                await channel.QueueDeclareAsync(queue, false, false, false);
                await action(channel, queue);
            }
            finally
            {
                await WithTemporaryChannelAsync(ch =>
                {
                    return ch.QueueDeleteAsync(queue);
                });
            }
        }

        protected Task AssertMessageCountAsync(string q, uint count)
        {
            return WithTemporaryChannelAsync(async ch =>
            {
                RabbitMQ.Client.QueueDeclareOk ok = await ch.QueueDeclarePassiveAsync(q);
                Assert.Equal(count, ok.MessageCount);
            });
        }

        protected static void AssertShutdownError(ShutdownEventArgs args, int code)
        {
            Assert.Equal(args.ReplyCode, code);
        }

        protected static void AssertPreconditionFailed(ShutdownEventArgs args)
        {
            AssertShutdownError(args, Constants.PreconditionFailed);
        }

        protected static Task AssertRanToCompletion(params Task[] tasks)
        {
            return DoAssertRanToCompletion(tasks);
        }

        protected static Task AssertRanToCompletion(IEnumerable<Task> tasks)
        {
            return DoAssertRanToCompletion(tasks);
        }

        protected static Task WaitAsync(TaskCompletionSource<bool> tcs, string desc)
        {
            return WaitAsync(tcs, WaitSpan, desc);
        }

        protected static async Task WaitAsync(TaskCompletionSource<bool> tcs, TimeSpan timeSpan, string desc)
        {
            try
            {
                await tcs.Task.WaitAsync(timeSpan);
                bool result = await tcs.Task;
                Assert.True((true == result) && (tcs.Task.IsCompletedSuccessfully()),
                    $"waiting {timeSpan.TotalSeconds} seconds on a tcs for '{desc}' failed");
            }
            catch (TimeoutException ex)
            {
                Assert.Fail($"waiting {timeSpan.TotalSeconds} seconds on a tcs for '{desc}' timed out, ex: {ex}");
            }
        }

        protected ConnectionFactory CreateConnectionFactory()
        {
            return new ConnectionFactory
            {
                ClientProvidedName = $"{_testDisplayName}:{Util.Now}:{GetConnectionIdx()}",
                ContinuationTimeout = WaitSpan,
                HandshakeContinuationTimeout = WaitSpan,
            };
        }

        protected void HandleConnectionShutdown(object sender, ShutdownEventArgs args)
        {
            if (args.Initiator == ShutdownInitiator.Peer)
            {
                IConnection conn = (IConnection)sender;
                _output.WriteLine($"{_testDisplayName} connection {conn.ClientProvidedName} shut down: {args}");
            }
        }

        protected void HandleConnectionShutdown(IConnection conn, ShutdownEventArgs args, Action<ShutdownEventArgs> a)
        {
            if (args.Initiator == ShutdownInitiator.Peer)
            {
                _output.WriteLine($"{_testDisplayName} connection {conn.ClientProvidedName} shut down: {args}");
            }
            a(args);
        }

        protected void HandleChannelShutdown(object sender, ShutdownEventArgs args)
        {
            if (args.Initiator == ShutdownInitiator.Peer)
            {
                IChannel ch = (IChannel)sender;
                _output.WriteLine($"{_testDisplayName} channel {ch.ChannelNumber} shut down: {args}");
            }
        }

        protected void HandleChannelShutdown(IChannel ch, ShutdownEventArgs args, Action<ShutdownEventArgs> a)
        {
            if (args.Initiator == ShutdownInitiator.Peer)
            {
                _output.WriteLine($"{_testDisplayName} channel {ch.ChannelNumber} shut down: {args}");
            }
            a(args);
        }

        private static bool InitIsRunningInCI()
        {
            bool ci;
            if (bool.TryParse(Environment.GetEnvironmentVariable("CI"), out ci))
            {
                if (ci == true)
                {
                    return true;
                }
            }
            else if (bool.TryParse(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), out ci))
            {
                if (ci == true)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool InitIsVerbose()
        {
            if (bool.TryParse(
                Environment.GetEnvironmentVariable("RABBITMQ_CLIENT_TESTS_VERBOSE"), out _))
            {
                return true;
            }

            return false;
        }

        private static int GetConnectionIdx()
        {
            return Interlocked.Increment(ref _connectionIdx);
        }

        private static async Task DoAssertRanToCompletion(IEnumerable<Task> tasks)
        {
            Task whenAllTask = Task.WhenAll(tasks);
            await whenAllTask;
            Assert.True(whenAllTask.IsCompletedSuccessfully());
        }

        protected static string GetUniqueString(ushort length)
        {
            byte[] bytes = GetRandomBody(length);
            return Convert.ToBase64String(bytes);
        }

        protected static byte[] GetRandomBody(ushort size = 1024)
        {
            var body = new byte[size];
#if NET6_0_OR_GREATER
            Random.Shared.NextBytes(body);
#else
            S_Random.NextBytes(body);
#endif
            return body;
        }

        protected static Task WaitForRecoveryAsync(IConnection conn)
        {
            TaskCompletionSource<bool> tcs = PrepareForRecovery((AutorecoveringConnection)conn);
            return WaitAsync(tcs, "recovery succeded");
        }

        protected static TaskCompletionSource<bool> PrepareForRecovery(IConnection conn)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            AutorecoveringConnection aconn = conn as AutorecoveringConnection;
            aconn.RecoverySucceeded += (source, ea) => tcs.SetResult(true);

            return tcs;
        }
    }
}
