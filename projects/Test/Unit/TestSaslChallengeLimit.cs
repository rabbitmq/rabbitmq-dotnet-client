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


#nullable enable

using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
using Xunit;

using ProtocolViolationException = RabbitMQ.Client.Exceptions.ProtocolViolationException;

namespace Test.Unit
{
    public class TestSaslChallengeLimit
    {
        private static readonly byte[] s_challenge = Encoding.UTF8.GetBytes("challenge");

        [Fact]
        public async Task APlainHandshakeFailsOnTheFirstChallenge_GH2077()
        {
            (int challengesSent, int responses) = await AssertTheHandshakeFailsAsync(
                new PlainMechanismFactory(), s_challenge);

            Assert.Equal(1, challengesSent);
            Assert.Equal(0, responses);
        }

        [Fact]
        public async Task AnExternalHandshakeFailsOnANonEmptyChallenge_GH2077()
        {
            (int challengesSent, int responses) = await AssertTheHandshakeFailsAsync(
                new ExternalMechanismFactory(), s_challenge);

            Assert.Equal(1, challengesSent);
            Assert.Equal(0, responses);
        }

        // RFC 4422 lets a server that reads EXTERNAL's empty initial response as absent send an
        // empty challenge for it.
        [Fact]
        public async Task AnExternalHandshakeAnswersAnEmptyChallenge_GH2077()
        {
            int responses = await AssertTheHandshakeConnectsAsync(
                new ExternalMechanismFactory(), Array.Empty<byte>(), challengesBeforeTune: 1);

            Assert.Equal(1, responses);
        }

        [Fact]
        public async Task AMechanismThatAlwaysAnswersStopsAfterTenChallenges_GH2077()
        {
            (int challengesSent, int responses) = await AssertTheHandshakeFailsAsync(
                new AlwaysAnswersMechanismFactory(), s_challenge);

            Assert.Equal(10, responses);
            Assert.Equal(11, challengesSent);
        }

        [Fact]
        public async Task AMechanismAnsweringTenChallengesConnects_GH2077()
        {
            int responses = await AssertTheHandshakeConnectsAsync(
                new AlwaysAnswersMechanismFactory(), s_challenge, challengesBeforeTune: 10);

            Assert.Equal(10, responses);
        }

        private static async Task<(int ChallengesSent, int Responses)> AssertTheHandshakeFailsAsync(
            IAuthMechanismFactory factory, byte[] challenge)
        {
            return await WithPeerAsync(factory, challenge, challengesBeforeTune: null, async connect =>
            {
                BrokerUnreachableException e = await Assert.ThrowsAsync<BrokerUnreachableException>(() => connect);
                Assert.IsType<ProtocolViolationException>(e.InnerException);
            });
        }

        private static async Task<int> AssertTheHandshakeConnectsAsync(
            IAuthMechanismFactory factory, byte[] challenge, int challengesBeforeTune)
        {
            (int challengesSent, int responses) = await WithPeerAsync(factory, challenge, challengesBeforeTune,
                async connect =>
                {
                    await using IConnection connection = await connect;
                    Assert.True(connection.IsOpen);
                    await connection.CloseAsync();
                });

            Assert.Equal(challengesBeforeTune, challengesSent);
            return responses;
        }

        private static async Task<(int, int)> WithPeerAsync(IAuthMechanismFactory factory, byte[] challenge,
            int? challengesBeforeTune, Func<Task<IConnection>, Task> assertOutcome)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var peerCts = new CancellationTokenSource();
            Task<(int, int)> peer = RunPeerAsync(listener, factory.Name, challenge, challengesBeforeTune, peerCts.Token);
            try
            {
                var cf = new ConnectionFactory
                {
                    HostName = IPAddress.Loopback.ToString(),
                    Port = ((IPEndPoint)listener.LocalEndpoint).Port,
                    AuthMechanisms = new[] { factory }
                };

                Task<IConnection> connect = cf.CreateConnectionAsync();

                Task finished = await Task.WhenAny(connect, Task.Delay(TimeSpan.FromSeconds(15)));
                Assert.True(ReferenceEquals(connect, finished), "the handshake is still answering challenges");

                await assertOutcome(connect);
            }
            finally
            {
                peerCts.Cancel();
                listener.Stop();
            }

            return await peer;
        }

        // Answers start-ok and each secure-ok with connection.secure, then with connection.tune once
        // challengesBeforeTune challenges have gone out; null means never.
        private static async Task<(int, int)> RunPeerAsync(TcpListener listener, string mechanism, byte[] challenge,
            int? challengesBeforeTune, CancellationToken cancellationToken)
        {
            int challengesSent = 0;
            int responses = 0;
            try
            {
                using TcpClient client = await listener.AcceptTcpClientAsync();
                using CancellationTokenRegistration r = cancellationToken.Register(client.Dispose);
                NetworkStream stream = client.GetStream();
                await ReadExactlyAsync(stream, new byte[8]);
                await WriteMethodAsync(stream, ConnectionMethodConstants.Start, ConnectionStartArguments(mechanism));
                while (true)
                {
                    byte[]? method = await ReadMethodAsync(stream);
                    if (method is null)
                    {
                        break;
                    }

                    if (method.Length < 4
                        || BinaryPrimitives.ReadUInt16BigEndian(method.AsSpan(0, 2)) != ClassConstants.Connection)
                    {
                        continue;
                    }

                    ushort methodId = BinaryPrimitives.ReadUInt16BigEndian(method.AsSpan(2, 2));
                    switch (methodId)
                    {
                        case ConnectionMethodConstants.StartOk:
                        case ConnectionMethodConstants.SecureOk:
                            if (methodId == ConnectionMethodConstants.SecureOk)
                            {
                                responses++;
                            }

                            if (challengesSent == challengesBeforeTune)
                            {
                                await WriteMethodAsync(stream, ConnectionMethodConstants.Tune, new byte[8]);
                            }
                            else
                            {
                                // Counted before the write, which can fault after the bytes are sent.
                                challengesSent++;
                                await WriteMethodAsync(stream, ConnectionMethodConstants.Secure, LongString(challenge));
                            }

                            break;
                        case ConnectionMethodConstants.Open:
                            await WriteMethodAsync(stream, ConnectionMethodConstants.OpenOk, new byte[1]);
                            break;
                        case ConnectionMethodConstants.Close:
                            await WriteMethodAsync(stream, ConnectionMethodConstants.CloseOk, Array.Empty<byte>());
                            break;
                    }
                }
            }
            catch (Exception e) when (e is IOException or InvalidOperationException or SocketException)
            {
            }

            return (challengesSent, responses);
        }

        private static byte[] ConnectionStartArguments(string mechanism)
        {
            using var ms = new MemoryStream();
            ms.WriteByte(0);
            ms.WriteByte(9);
            byte[] emptyTable = new byte[4];
            ms.Write(emptyTable, 0, emptyTable.Length);
            byte[] mechanisms = LongString(Encoding.UTF8.GetBytes(mechanism));
            ms.Write(mechanisms, 0, mechanisms.Length);
            byte[] locales = LongString(Encoding.UTF8.GetBytes("en_US"));
            ms.Write(locales, 0, locales.Length);
            return ms.ToArray();
        }

        private static byte[] LongString(byte[] value)
        {
            byte[] result = new byte[4 + value.Length];
            BinaryPrimitives.WriteUInt32BigEndian(result, (uint)value.Length);
            Buffer.BlockCopy(value, 0, result, 4, value.Length);
            return result;
        }

        private static async Task WriteMethodAsync(Stream stream, ushort methodId, byte[] arguments)
        {
            int payloadLength = 4 + arguments.Length;
            byte[] frame = new byte[7 + payloadLength + 1];
            frame[0] = Constants.FrameMethod;
            BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(3), (uint)payloadLength);
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(7), ClassConstants.Connection);
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(9), methodId);
            Buffer.BlockCopy(arguments, 0, frame, 11, arguments.Length);
            frame[frame.Length - 1] = Constants.FrameEnd;
            await stream.WriteAsync(frame, 0, frame.Length);
        }

        private static async Task<byte[]?> ReadMethodAsync(Stream stream)
        {
            byte[] header = new byte[7];
            if (!await ReadExactlyAsync(stream, header))
            {
                return null;
            }

            byte[] payload = new byte[BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(3)) + 1];
            if (!await ReadExactlyAsync(stream, payload))
            {
                return null;
            }

            return header[0] == Constants.FrameMethod ? payload : Array.Empty<byte>();
        }

        private static async Task<bool> ReadExactlyAsync(Stream stream, byte[] buffer)
        {
            int read = 0;
            while (read < buffer.Length)
            {
                int n = await stream.ReadAsync(buffer, read, buffer.Length - read);
                if (n == 0)
                {
                    return false;
                }

                read += n;
            }

            return true;
        }

        private sealed class AlwaysAnswersMechanismFactory : IAuthMechanismFactory
        {
            public string Name => "X-ALWAYS-ANSWERS";

            public IAuthMechanism GetInstance() => new AlwaysAnswersMechanism();
        }

        private sealed class AlwaysAnswersMechanism : IAuthMechanism
        {
            public Task<byte[]> HandleChallengeAsync(byte[]? challenge, ConnectionConfig config,
                CancellationToken cancellationToken = default)
                => Task.FromResult(Array.Empty<byte>());
        }
    }
}
