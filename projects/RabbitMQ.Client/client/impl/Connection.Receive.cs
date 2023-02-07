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
using System.Threading.Tasks;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing.Impl
{
#nullable enable
    internal sealed partial class Connection
    {
        private readonly IFrameHandler _frameHandler;
        private readonly Task _mainLoopTask;

        private async Task MainLoop()
        {
            try
            {
                await ReceiveLoop().ConfigureAwait(false);
            }
            catch (EndOfStreamException eose)
            {
                // Possible heartbeat exception
                HandleMainLoopException(new ShutdownEventArgs(ShutdownInitiator.Library, 0, "End of stream", eose));
            }
            catch (HardProtocolException hpe)
            {
                await HardProtocolExceptionHandler(hpe).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                HandleMainLoopException(new ShutdownEventArgs(ShutdownInitiator.Library, Constants.InternalError, "Unexpected Exception", ex));
            }

            FinishClose();
        }

        private async Task ReceiveLoop()
        {
            while (!_closed)
            {
                while (_frameHandler.TryReadFrame(out InboundFrame frame))
                {
                    NotifyHeartbeatListener();
                    ProcessFrame(frame);
                }

                // Done reading frames synchronously, go async
                InboundFrame asyncFrame = await _frameHandler.ReadFrameAsync()
                   .ConfigureAwait(false);
                NotifyHeartbeatListener();
                ProcessFrame(asyncFrame);
            }
        }

        private void ProcessFrame(InboundFrame frame)
        {
            bool shallReturn = true;
            if (frame.Channel == 0)
            {
                if (frame.Type == FrameType.FrameHeartbeat)
                {
                    // Ignore it: we've already just reset the heartbeat
                }
                else
                {
                    // In theory, we could get non-connection.close-ok
                    // frames here while we're quiescing (m_closeReason !=
                    // null). In practice, there's a limited number of
                    // things the server can ask of us on channel 0 -
                    // essentially, just connection.close. That, combined
                    // with the restrictions on pipelining, mean that
                    // we're OK here to handle channel 0 traffic in a
                    // quiescing situation, even though technically we
                    // should be ignoring everything except
                    // connection.close-ok.
                    shallReturn = _session0.HandleFrame(in frame);
                }
            }
            else
            {
                // If we're still m_running, but have a m_closeReason,
                // then we must be quiescing, which means any inbound
                // frames for non-zero channels (and any inbound
                // commands on channel zero that aren't
                // Connection.CloseOk) must be discarded.
                if (_closeReason is null)
                {
                    // No close reason, not quiescing the
                    // connection. Handle the frame. (Of course, the
                    // Session itself may be quiescing this particular
                    // channel, but that's none of our concern.)
                    shallReturn = _sessionManager.Lookup(frame.Channel).HandleFrame(in frame);
                }
            }

            if (shallReturn)
            {
                frame.ReturnPayload();
            }
        }

        ///<remarks>
        /// May be called more than once. Should therefore be idempotent.
        ///</remarks>
        private void TerminateMainloop()
        {
            MaybeStopHeartbeatTimers();
        }

        private void HandleMainLoopException(ShutdownEventArgs reason)
        {
            if (!SetCloseReason(reason))
            {
                LogCloseError("Unexpected Main Loop Exception while closing: " + reason, new Exception(reason.ToString()));
                return;
            }

            OnShutdown(reason);
            LogCloseError($"Unexpected connection closure: {reason}", new Exception(reason.ToString()));
        }

        private async Task HardProtocolExceptionHandler(HardProtocolException hpe)
        {
            if (SetCloseReason(hpe.ShutdownReason))
            {
                OnShutdown(hpe.ShutdownReason);
                _session0.SetSessionClosing(false);
                try
                {
                    var cmd = new ConnectionClose(hpe.ShutdownReason.ReplyCode, hpe.ShutdownReason.ReplyText, 0, 0);
                    _session0.Transmit(in cmd);
                    if (hpe.CanShutdownCleanly)
                    {
                        await ClosingLoop().ConfigureAwait(false);
                    }
                }
                catch (IOException ioe)
                {
                    LogCloseError("Broker closed socket unexpectedly", ioe);
                }
            }
            else
            {
                LogCloseError("Hard Protocol Exception occurred while closing the connection", hpe);
            }
        }

        ///<remarks>
        /// Loop only used while quiescing. Use only to cleanly close connection
        ///</remarks>
        private async Task ClosingLoop()
        {
            try
            {
                _frameHandler.ReadTimeout = TimeSpan.Zero;
                // Wait for response/socket closure or timeout
                await ReceiveLoop().ConfigureAwait(false);
            }
            catch (ObjectDisposedException ode)
            {
                if (!_closed)
                {
                    LogCloseError("Connection didn't close cleanly", ode);
                }
            }
            catch (EndOfStreamException eose)
            {
                if (_model0.CloseReason is null)
                {
                    LogCloseError("Connection didn't close cleanly. Socket closed unexpectedly", eose);
                }
            }
            catch (IOException ioe)
            {
                LogCloseError("Connection didn't close cleanly. Socket closed unexpectedly", ioe);
            }
            catch (Exception e)
            {
                LogCloseError("Unexpected exception while closing: ", e);
            }
        }
    }
}
