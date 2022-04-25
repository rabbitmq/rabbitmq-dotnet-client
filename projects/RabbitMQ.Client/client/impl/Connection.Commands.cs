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
using System.IO;
using System.Text;

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Logging;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Framing.Impl
{
#nullable enable
    internal sealed partial class Connection
    {
        public void UpdateSecret(string newSecret, string reason)
        {
            _model0.UpdateSecret(newSecret, reason);
        }

        internal void NotifyReceivedCloseOk()
        {
            TerminateMainloop();
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

        private void Open()
        {
            RabbitMqClientEventSource.Log.ConnectionOpened();
            StartAndTune();
            _model0.ConnectionOpen(_config.VirtualHost);
        }

        private void StartAndTune()
        {
            var connectionStartCell = new BlockingCell<ConnectionStartDetails>();
            _model0.m_connectionStartCell = connectionStartCell;
            _model0.HandshakeContinuationTimeout = _config.HandshakeContinuationTimeout;
            _frameHandler.ReadTimeout = _config.HandshakeContinuationTimeout;
            Write(Protocol.Header);

            ConnectionStartDetails connectionStart = connectionStartCell.WaitForValue();

            if (connectionStart is null)
            {
                throw new IOException("connection.start was never received, likely due to a network timeout");
            }

            ServerProperties = connectionStart.m_serverProperties;

            var serverVersion = new AmqpVersion(connectionStart.m_versionMajor, connectionStart.m_versionMinor);
            if (!serverVersion.Equals(Protocol.Version))
            {
                TerminateMainloop();
                FinishClose();
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
                        res = _model0.ConnectionStartOk(ClientProperties,
                            mechanismFactory.Name,
                            response,
                            "en_US");
                    }
                    else
                    {
                        res = _model0.ConnectionSecureOk(response);
                    }

                    if (res.m_challenge is null)
                    {
                        connectionTune = res.m_tuneDetails;
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
            _sessionManager = new SessionManager(this, channelMax);

            uint frameMax = NegotiatedMaxValue(_config.MaxFrameSize, connectionTune.m_frameMax);
            FrameMax = frameMax;
            MaxPayloadSize = frameMax == 0 ? int.MaxValue : (int)frameMax - Client.Impl.Framing.BaseFrameSize;

            uint heartbeatInSeconds = NegotiatedMaxValue((uint)_config.HeartbeatInterval.TotalSeconds, (uint)connectionTune.m_heartbeatInSeconds);
            Heartbeat = TimeSpan.FromSeconds(heartbeatInSeconds);

            _model0.ConnectionTuneOk(channelMax, frameMax, (ushort)Heartbeat.TotalSeconds);

            // now we can start heartbeat timers
            MaybeStartHeartbeatTimers();
        }

        private IAuthMechanismFactory GetAuthMechanismFactory(string supportedMechanismNames)
        {
            // Our list is in order of preference, the server one is not.
            foreach (var factory in _config.AuthMechanisms)
            {
                if (supportedMechanismNames.IndexOf(factory.Name, StringComparison.OrdinalIgnoreCase) >= 0)
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
