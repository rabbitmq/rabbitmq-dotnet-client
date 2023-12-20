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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test
{
    public abstract class IntegrationFixtureBase : IDisposable
    {
        private static bool s_isRunningInCI = false;
        private static bool s_isWindows = false;
        private static bool s_isVerbose = false;
        private static int _connectionIdx = 0;

        protected readonly RabbitMQCtl _rabbitMQCtl;

        protected ConnectionFactory _connFactory;
        protected IConnection _conn;
        protected IChannel _channel;

        protected static readonly Encoding _encoding = new UTF8Encoding();
        protected static readonly int _processorCount = Environment.ProcessorCount;

        protected readonly ITestOutputHelper _output;
        protected readonly string _testDisplayName;

        public static readonly TimeSpan WaitSpan;
        public static readonly TimeSpan LongWaitSpan;
        public static readonly TimeSpan RecoveryInterval = TimeSpan.FromSeconds(2);
        public static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan RequestedConnectionTimeout = TimeSpan.FromSeconds(1);
        public static readonly Random S_Random;

        static IntegrationFixtureBase()
        {
            S_Random = new Random();
            InitIsRunningInCI();
            InitIsWindows();
            InitIsVerbose();

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

        public IntegrationFixtureBase(ITestOutputHelper output)
        {
            _output = output;
            _rabbitMQCtl = new RabbitMQCtl(_output);

            Type type = _output.GetType();
            FieldInfo testMember = type.GetField("test", BindingFlags.Instance | BindingFlags.NonPublic);
            ITest test = (ITest)testMember.GetValue(output);
            _testDisplayName = test.DisplayName
                .Replace("Test.", string.Empty)
                .Replace("AsyncIntegration.", "AI.")
                .Replace("Integration.", "I.")
                .Replace("SequentialI.", "SI.");

            SetUp();
        }

        protected virtual void SetUp()
        {
            if (_connFactory == null)
            {
                /*
                 * https://github.com/rabbitmq/rabbitmq-dotnet-client/commit/120f9bfce627f704956e1008d095b853b459d45b#r135400345
                 * 
                 * Integration tests must use CreateConnectionFactory so that ClientProvidedName is set for the connection.
                 * Tests that close connections via `rabbitmqctl` depend on finding the connection PID via its name.
                 */
                _connFactory = CreateConnectionFactory();
            }

            if (_conn == null)
            {
                _conn = CreateConnectionWithRetries(_connFactory);
                _channel = _conn.CreateChannel();
                AddCallbackHandlers();
            }
        }

        public virtual void Dispose()
        {
            if (_channel != null)
            {
                _channel.Dispose();
            }

            if (_conn != null)
            {
                _conn.Dispose();
            }

            TearDown();
        }

        protected virtual void TearDown()
        {
        }

        protected virtual void AddCallbackHandlers()
        {
            if (IntegrationFixtureBase.IsVerbose)
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
        }

        protected static bool IsRunningInCI
        {
            get { return s_isRunningInCI; }
        }

        protected static bool IsWindows
        {
            get { return s_isWindows; }
        }

        protected static bool IsVerbose
        {
            get { return s_isVerbose; }
        }

        internal AutorecoveringConnection CreateAutorecoveringConnection(IEnumerable<string> hostnames, bool expectException = false)
        {

            return CreateAutorecoveringConnection(hostnames, RequestedConnectionTimeout, RecoveryInterval, expectException);
        }

        internal AutorecoveringConnection CreateAutorecoveringConnection(IEnumerable<string> hostnames,
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

            return (AutorecoveringConnection)CreateConnectionWithRetries(cf, hostnames, expectException);
        }

        protected IConnection CreateConnectionWithRetries(ConnectionFactory connectionFactory,
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
                        return connectionFactory.CreateConnection();
                    }
                    else
                    {
                        return connectionFactory.CreateConnection(hostnames);
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
                            ioex = agex1.InnerExceptions.Where(ex => ex is IOException).FirstOrDefault() as IOException;
                        }

                        ioex ??= ex.InnerException as IOException;

                        if (ioex is null)
                        {
                            throw;
                        }
                        else
                        {
                            if (ioex.InnerException is SocketException)
                            {
                                tries++;
                                _output.WriteLine($"WARNING: {nameof(CreateConnectionWithRetries)} retrying ({tries}), caught exception: {ioex.InnerException}");
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

            Assert.Fail($"FAIL: {nameof(CreateConnectionWithRetries)} could not open connection");
            return null;
        }

        protected void WithTemporaryChannel(Action<IChannel> action)
        {
            IChannel channel = _conn.CreateChannel();

            try
            {
                action(channel);
            }
            finally
            {
                channel.Abort();
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

        protected void WithTemporaryNonExclusiveQueue(Action<IChannel, string> action)
        {
            WithTemporaryNonExclusiveQueue(_channel, action);
        }

        protected void WithTemporaryNonExclusiveQueue(IChannel channel, Action<IChannel, string> action)
        {
            WithTemporaryNonExclusiveQueue(channel, action, GenerateQueueName());
        }

        protected void WithTemporaryNonExclusiveQueue(IChannel channel, Action<IChannel, string> action, string queue)
        {
            try
            {
                channel.QueueDeclare(queue, false, false, false, null);
                action(channel, queue);
            }
            finally
            {
                WithTemporaryChannel(tm => tm.QueueDelete(queue));
            }
        }

        protected void AssertMessageCount(string q, uint count)
        {
            WithTemporaryChannel((m) =>
            {
                RabbitMQ.Client.QueueDeclareOk ok = m.QueueDeclarePassive(q);
                Assert.Equal(count, ok.MessageCount);
            });
        }

        protected void AssertShutdownError(ShutdownEventArgs args, int code)
        {
            Assert.Equal(args.ReplyCode, code);
        }

        protected void AssertPreconditionFailed(ShutdownEventArgs args)
        {
            AssertShutdownError(args, Constants.PreconditionFailed);
        }

        protected void Wait(ManualResetEventSlim latch, string desc)
        {
            Assert.True(latch.Wait(WaitSpan),
                $"waiting {WaitSpan.TotalSeconds} seconds on a latch for '{desc}' timed out");
        }

        protected void Wait(ManualResetEventSlim latch, TimeSpan timeSpan, string desc)
        {
            Assert.True(latch.Wait(timeSpan),
                $"waiting {timeSpan.TotalSeconds} seconds on a latch for '{desc}' timed out");
        }

        protected ConnectionFactory CreateConnectionFactory()
        {
            string now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            return new ConnectionFactory
            {
                ClientProvidedName = $"{_testDisplayName}:{now}:{GetConnectionIdx()}",
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

        private static void InitIsRunningInCI()
        {
            bool ci;
            if (bool.TryParse(Environment.GetEnvironmentVariable("CI"), out ci))
            {
                if (ci == true)
                {
                    s_isRunningInCI = true;
                }
            }
            else if (bool.TryParse(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), out ci))
            {
                if (ci == true)
                {
                    s_isRunningInCI = true;
                }
            }
            else
            {
                s_isRunningInCI = false;
            }
        }

        private static void InitIsWindows()
        {
            PlatformID platform = Environment.OSVersion.Platform;
            if (platform == PlatformID.Win32NT)
            {
                s_isWindows = true;
                return;
            }

            string os = Environment.GetEnvironmentVariable("OS");
            if (os != null)
            {
                os = os.Trim();
                s_isWindows = os == "Windows_NT";
                return;
            }
        }

        private static void InitIsVerbose()
        {
            if (bool.TryParse(
                Environment.GetEnvironmentVariable("RABBITMQ_CLIENT_TESTS_VERBOSE"), out _))
            {
                s_isVerbose = true;
            }
        }

        private static int GetConnectionIdx()
        {
            return Interlocked.Increment(ref _connectionIdx);
        }

        protected static string GetUniqueString(ushort length)
        {
            byte[] bytes = GetRandomBody(length);
            return Convert.ToBase64String(bytes);
        }

        protected static byte[] GetRandomBody(ushort size)
        {
            var body = new byte[size];
#if NET6_0_OR_GREATER
            Random.Shared.NextBytes(body);
#else
            S_Random.NextBytes(body);
#endif
            return body;
        }
    }
}
