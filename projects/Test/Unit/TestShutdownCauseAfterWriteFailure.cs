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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using Xunit;

namespace Test.Unit
{
    public class TestShutdownCauseAfterWriteFailure
    {
        private static readonly Exception s_writeFailure = new ObjectDisposedException("body");

        public static IEnumerable<object[]> ReadsCutOffByTheWriteLoop()
        {
            // What a read reports when the write loop's own socket close stops it, pending or not.
            yield return new object[] { new IOException("aborted", new SocketException((int)SocketError.OperationAborted)) };
            yield return new object[] { new IOException("disposed", new ObjectDisposedException("socket")) };
            yield return new object[] { new ObjectDisposedException("socket") };
        }

        [Theory]
        [MemberData(nameof(ReadsCutOffByTheWriteLoop))]
        public async Task AReadCutOffByTheWriteLoopReportsTheWriteFailure_GH2108(Exception readException)
        {
            Connection connection = await RunMainLoopAsync(readException);

            Assert.Equal(Constants.InternalError, connection.CloseReason!.ReplyCode);
            Assert.Same(s_writeFailure, connection.CloseReason.Exception);
            Assert.Contains(connection.ShutdownReport, entry => ReferenceEquals(entry.Exception, readException));
        }

        [Fact]
        public async Task AnEndOfStreamAfterAWriteFailureStaysTheCause_GH2108()
        {
            var endOfStream = new EndOfStreamException("Pipe is completed.");

            Connection connection = await RunMainLoopAsync(endOfStream);

            Assert.Equal(0, connection.CloseReason!.ReplyCode);
            Assert.Same(endOfStream, connection.CloseReason.Exception);
            Assert.Contains(connection.ShutdownReport, entry => ReferenceEquals(entry.Exception, s_writeFailure));
        }

        [Fact]
        public async Task AFileLoadFailureAfterAWriteFailureStaysTheCause_GH2108()
        {
            var fileLoad = new FileLoadException("binding");

            Connection connection = await RunMainLoopAsync(fileLoad);

            Assert.Same(fileLoad, connection.CloseReason!.Exception);
            Assert.Contains(connection.ShutdownReport, entry => ReferenceEquals(entry.Exception, s_writeFailure));
        }

        private static async Task<Connection> RunMainLoopAsync(Exception readException)
        {
            var connection = new Connection(TestConnectionConfig(), new FailedWriteLoopFrameHandler(readException));
            MethodInfo mainLoop = typeof(Connection).GetMethod("MainLoop", BindingFlags.NonPublic | BindingFlags.Instance)!;
            Task running = (Task)mainLoop.Invoke(connection, null)!;
            Task finished = await Task.WhenAny(running, Task.Delay(TimeSpan.FromSeconds(30)));
            Assert.True(ReferenceEquals(running, finished), "the main loop did not finish");
            await running;
            return connection;
        }

        // Only the frame handler is exercised; the rest are the least surprising values the constructor accepts.
        private static ConnectionConfig TestConnectionConfig()
            => new ConnectionConfig(
                virtualHost: "/",
                userName: "guest",
                password: "guest",
                credentialsProvider: null,
                authMechanisms: Array.Empty<IAuthMechanismFactory>(),
                clientProperties: new Dictionary<string, object>(),
                clientProvidedName: null,
                maxChannelCount: ConnectionFactory.DefaultChannelMax,
                maxFrameSize: 0,
                maxInboundMessageBodySize: ConnectionFactory.DefaultMaxInboundMessageBodySize,
                topologyRecoveryEnabled: false,
                topologyRecoveryFilter: new TopologyRecoveryFilter(),
                topologyRecoveryExceptionHandler: new TopologyRecoveryExceptionHandler(),
                networkRecoveryInterval: TimeSpan.FromSeconds(5),
                heartbeatInterval: TimeSpan.Zero,
                continuationTimeout: TimeSpan.FromSeconds(5),
                handshakeContinuationTimeout: TimeSpan.FromSeconds(5),
                requestedConnectionTimeout: TimeSpan.FromSeconds(5),
                consumerDispatchConcurrency: 1,
                tracingOptions: null,
                frameHandlerFactoryAsync: (_, __) => throw new NotSupportedException("not connected in this test"));

        private sealed class FailedWriteLoopFrameHandler : IFrameHandler
        {
            private readonly Exception _readException;

            internal FailedWriteLoopFrameHandler(Exception readException)
            {
                _readException = readException;
            }

            public AmqpTcpEndpoint Endpoint { get; } = new AmqpTcpEndpoint("localhost");
            public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 1);
            public int LocalPort => 1;
            public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 5672);
            public int RemotePort => 5672;
            public TimeSpan ReadTimeout { set { } }
            public TimeSpan WriteTimeout { set { } }
            public Exception WriteLoopException => s_writeFailure;

            public ValueTask CloseAsync(CancellationToken cancellationToken) => default;
            public void CloseSocket() { }
            public ValueTask ReadFrameAsync(InboundFrame frame, CancellationToken cancellationToken) => throw _readException;
            public bool TryReadFrame(InboundFrame frame) => false;
            public ValueTask SendProtocolHeaderAsync(CancellationToken cancellationToken) => default;

            public ValueTask WriteAsync(OutgoingFrame frames, CancellationToken cancellationToken)
            {
                frames.Dispose();
                return default;
            }
        }
    }
}
