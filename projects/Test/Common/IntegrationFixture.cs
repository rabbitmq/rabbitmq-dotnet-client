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
using System.Reflection;
using System.Text;
using System.Threading;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test
{
    public class IntegrationFixture : IDisposable
    {
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
        public static readonly Random S_Random;

        static IntegrationFixture()
        {
            S_Random = new Random();

            int threadCount = _processorCount * 16;
            ThreadPool.SetMinThreads(threadCount, threadCount);

            if (IsRunningInCI())
            {
                WaitSpan = TimeSpan.FromSeconds(30);
                LongWaitSpan = TimeSpan.FromSeconds(60);
            }
            else
            {
                WaitSpan = TimeSpan.FromSeconds(15);
                LongWaitSpan = TimeSpan.FromSeconds(30);
            }
        }

        public IntegrationFixture(ITestOutputHelper output)
        {
            _output = output;
            _rabbitMQCtl = new RabbitMQCtl(_output);

            var type = _output.GetType();
            var testMember = type.GetField("test", BindingFlags.Instance | BindingFlags.NonPublic);
            var test = (ITest)testMember.GetValue(output);
            _testDisplayName = test.DisplayName;

            SetUp();
        }

        protected virtual void SetUp()
        {
            if (_connFactory == null)
            {
                _connFactory = CreateConnectionFactory();
            }

            if (_conn == null)
            {
                _conn = _connFactory.CreateConnection();
                _channel = _conn.CreateChannel();
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

        internal AutorecoveringConnection CreateAutorecoveringConnection(IList<string> hostnames)
        {
            return CreateAutorecoveringConnection(RecoveryInterval, hostnames);
        }

        internal AutorecoveringConnection CreateAutorecoveringConnection(TimeSpan interval, IList<string> hostnames)
        {
            var cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            // tests that use this helper will likely list unreachable hosts;
            // make sure we time out quickly on those
            cf.RequestedConnectionTimeout = TimeSpan.FromSeconds(1);
            cf.NetworkRecoveryInterval = interval;
            return (AutorecoveringConnection)cf.CreateConnection(hostnames);
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

        protected void WaitOn(object o)
        {
            lock (o)
            {
                Monitor.Wait(o, TimingFixture.TestTimeout);
            }
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

        private static int GetConnectionIdx()
        {
            return Interlocked.Increment(ref _connectionIdx);
        }

        private static bool IsRunningInCI()
        {
            bool ci = false;

            if (bool.TryParse(Environment.GetEnvironmentVariable("CI"), out ci))
            {
                if (ci == true)
                {
                    return ci;
                }
            }
            else if (bool.TryParse(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), out ci))
            {
                if (ci == true)
                {
                    return ci;
                }
            }

            return ci;
        }
    }
}
