// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing.Impl
{
    internal sealed partial class Connection
    {
        public Task UpdateSecretAsync(string newSecret, string reason,
            CancellationToken cancellationToken)
        {
            return _channel0.UpdateSecretAsync(newSecret, reason, cancellationToken);
        }

        internal void NotifyReceivedCloseOk()
        {
            MaybeTerminateMainloopAndStopHeartbeatTimers(cancelMainLoop: true);
            _closed = true;
        }

        internal void HandleConnectionBlocked(string reason)
        {
            if (!_connectionBlockedWrapper.IsEmpty)
            {
                _connectionBlockedWrapper.Invoke(this, new ConnectionBlockedEventArgs(reason));
            }
        }

        internal void HandleConnectionUnblocked()
        {
            if (!_connectionUnblockedWrapper.IsEmpty)
            {
                _connectionUnblockedWrapper.Invoke(this, EventArgs.Empty);
            }
        }

        private async ValueTask StartAndTuneAsync(CancellationToken cancellationToken)
        {
            var connectionStartCell = new TaskCompletionSource<ConnectionStartDetails?>(TaskCreationOptions.RunContinuationsAsynchronously);

#if NET6_0_OR_GREATER
            using CancellationTokenRegistration ctr = cancellationToken.UnsafeRegister((object? state) =>
            {
                if (state != null)
                {
                    var csc = (TaskCompletionSource<ConnectionStartDetails>)state;
                    csc.TrySetCanceled(cancellationToken);
                }
            }, connectionStartCell);
#else
            using CancellationTokenRegistration ctr = cancellationToken.Register((object state) =>
            {
                var csc = (TaskCompletionSource<ConnectionStartDetails>)state;
                csc.TrySetCanceled(cancellationToken);
            }, state: connectionStartCell, useSynchronizationContext: false);
#endif

            _channel0.m_connectionStartCell = connectionStartCell;
            _channel0.HandshakeContinuationTimeout = _config.HandshakeContinuationTimeout;
            _frameHandler.ReadTimeout = _config.HandshakeContinuationTimeout;

            await _frameHandler.SendProtocolHeaderAsync(cancellationToken)
                .ConfigureAwait(false);

            Task<ConnectionStartDetails?> csct = connectionStartCell.Task;
            ConnectionStartDetails? connectionStart = await csct.ConfigureAwait(false);

            if (connectionStart is null || csct.IsCanceled)
            {
                const string msg = "connection.start was never received, likely due to a network timeout";
                throw new IOException(msg, _channel0.ConnectionStartException);
            }

            ServerProperties = connectionStart.m_serverProperties;

            var serverVersion = new AmqpVersion(connectionStart.m_versionMajor, connectionStart.m_versionMinor);
            if (!serverVersion.Equals(Protocol.Version))
            {
                /*
                 * Note:
                 * FinishCloseAsync will cancel the main loop
                 */
                MaybeTerminateMainloopAndStopHeartbeatTimers();
                await FinishCloseAsync(cancellationToken)
                    .ConfigureAwait(false);
                throw new ProtocolVersionMismatchException(Protocol.MajorVersion, Protocol.MinorVersion, serverVersion.Major, serverVersion.Minor);
            }

            // FIXME: parse out locales properly!
            ConnectionTuneDetails connectionTune = default;
            bool tuned = false;
            try
            {
                string mechanismsString = Encoding.UTF8.GetString(connectionStart.m_mechanisms);
                IAuthMechanismFactory mechanismFactory = GetAuthMechanismFactory(mechanismsString);
                IAuthMechanism mechanism = mechanismFactory.GetInstance();
                byte[]? challenge = null;
                do
                {
                    byte[] response = mechanism.handleChallenge(challenge, _config);
                    ConnectionSecureOrTune res;
                    if (challenge is null)
                    {
                        res = await _channel0.ConnectionStartOkAsync(ClientProperties,
                            mechanismFactory.Name, response, "en_US", cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        res = await _channel0.ConnectionSecureOkAsync(response, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    if (res.m_challenge is null)
                    {
                        connectionTune = res.m_tuneDetails.GetValueOrDefault();
                        tuned = true;
                    }
                    else
                    {
                        challenge = res.m_challenge;
                    }
                }
                while (!tuned);
            }
            catch (OperationInterruptedException e)
            {
                if (e.ShutdownReason != null && e.ShutdownReason.ReplyCode == Constants.AccessRefused)
                {
                    throw new AuthenticationFailureException(e.ShutdownReason.ReplyText);
                }
                throw new PossibleAuthenticationFailureException(
                    "Possibly caused by authentication failure", e);
            }

            ushort channelMax = (ushort)NegotiatedMaxValue(_config.MaxChannelCount, connectionTune.m_channelMax);
            _sessionManager = new SessionManager(this, channelMax, _config.MaxInboundMessageBodySize);

            uint frameMax = NegotiatedMaxValue(_config.MaxFrameSize, connectionTune.m_frameMax);
            FrameMax = frameMax;
            MaxPayloadSize = frameMax == 0 ? int.MaxValue : (int)frameMax - Client.Impl.Framing.BaseFrameSize;

            uint heartbeatInSeconds = NegotiatedMaxValue((uint)_config.HeartbeatInterval.TotalSeconds, (uint)connectionTune.m_heartbeatInSeconds);
            Heartbeat = TimeSpan.FromSeconds(heartbeatInSeconds);

            await _channel0.ConnectionTuneOkAsync(channelMax, frameMax, (ushort)Heartbeat.TotalSeconds, cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            MaybeStartCredentialRefresher();

            // now we can start heartbeat timers
            cancellationToken.ThrowIfCancellationRequested();
            MaybeStartHeartbeatTimers();
        }

        private void MaybeStartCredentialRefresher()
        {
            if (_config.CredentialsProvider.ValidUntil != null)
            {
                _config.CredentialsRefresher.Register(_config.CredentialsProvider, NotifyCredentialRefreshedAsync);
            }
        }

        private async Task NotifyCredentialRefreshedAsync(bool succesfully)
        {
            if (succesfully)
            {
                using var cts = new CancellationTokenSource(InternalConstants.DefaultConnectionCloseTimeout);
                await UpdateSecretAsync(_config.CredentialsProvider.Password, "Token refresh", cts.Token)
                    .ConfigureAwait(false);
            }
        }

        private IAuthMechanismFactory GetAuthMechanismFactory(string supportedMechanismNames)
        {
            // Our list is in order of preference, the server one is not.
            foreach (IAuthMechanismFactory factory in _config.AuthMechanisms)
            {
#if NET6_0_OR_GREATER
                if (supportedMechanismNames.Contains(factory.Name, StringComparison.OrdinalIgnoreCase))
#else
                if (supportedMechanismNames.IndexOf(factory.Name, StringComparison.OrdinalIgnoreCase) >= 0)
#endif
                {
                    return factory;
                }
            }

            throw new IOException($"No compatible authentication mechanism found - server offered [{supportedMechanismNames}]");
        }

        private static uint NegotiatedMaxValue(uint clientValue, uint serverValue)
        {
            return (clientValue == 0 || serverValue == 0) ?
                Math.Max(clientValue, serverValue) :
                Math.Min(clientValue, serverValue);
        }
    }
}
