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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using Xunit;

namespace Test.Unit
{
    public class TestTimeoutClamping
    {
        [Fact]
        public void InfiniteIsLeftAlone_GH2029()
        {
            // The source accepts it, and it means "no deadline" rather than a large delay.
            Assert.Equal(Timeout.InfiniteTimeSpan,
                InternalConstants.ClampToCancellationTokenSourceDelay(Timeout.InfiniteTimeSpan));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(20)]
        [InlineData(30 * 60)]
        public void AValueTheSourceAcceptsPassesThrough_GH2029(int seconds)
        {
            TimeSpan timeout = TimeSpan.FromSeconds(seconds);
            Assert.Equal(timeout, InternalConstants.ClampToCancellationTokenSourceDelay(timeout));
        }

        [Theory]
        [InlineData(60)]
        [InlineData(365)]
        public void AValueAboveTheLimitIsClamped_GH2029(int days)
        {
            Assert.Equal(InternalConstants.MaxCancellationTokenSourceDelay,
                InternalConstants.ClampToCancellationTokenSourceDelay(TimeSpan.FromDays(days)));
        }

        [Fact]
        public void TimeSpanMaxValueIsClamped_GH2029()
        {
            Assert.Equal(InternalConstants.MaxCancellationTokenSourceDelay,
                InternalConstants.ClampToCancellationTokenSourceDelay(TimeSpan.MaxValue));
        }

        [Fact]
        public void TheClampedValueIsOneTheSourceAccepts_GH2029()
        {
            // Fails if the constant is raised past what the running framework accepts.
            TimeSpan clamped = InternalConstants.ClampToCancellationTokenSourceDelay(TimeSpan.MaxValue);

            using var cts = new CancellationTokenSource(clamped);
            cts.CancelAfter(clamped);

            Assert.False(cts.IsCancellationRequested);
        }

        [Fact]
        public async Task AnOverLargeConnectionTimeoutStillConnects_GH2029()
        {
            var client = new ConnectedTcpClient();

            ITcpClient connected = await SocketFactory.OpenAsync(new AmqpTcpEndpoint("127.0.0.1"),
                _ => client, TimeSpan.MaxValue, CancellationToken.None);

            Assert.Same(client, connected);
        }

        private sealed class ConnectedTcpClient : ITcpClient
        {
            public bool Connected => true;
            public TimeSpan ReceiveTimeout { get; set; }
            public Socket Client => throw new NotSupportedException();
            public Task ConnectAsync(IPAddress host, int port, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public NetworkStream GetStream() => throw new NotSupportedException();
            public void Close() { }
            public void Dispose() { }
        }
    }
}
