// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing
{
    internal sealed partial class Connection
    {
        private readonly CancellationTokenSource _mainLoopCts = new CancellationTokenSource();
        private readonly IFrameHandler _frameHandler;
        private Task _mainLoopTask;

        private async Task MainLoop()
        {
            CancellationToken mainLoopToken = _mainLoopCts.Token;
            try
            {
                await ReceiveLoopAsync(mainLoopToken)
                    .ConfigureAwait(false);
            }
#if NETSTANDARD
            catch (ThreadAbortException taex)
            {
                /*
                 * https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/826
                 */
                var ea = new ShutdownEventArgs(ShutdownInitiator.Library,
                    Constants.InternalError,
                    "Thread aborted (AppDomain unloaded?)",
                    exception: taex);
                await HandleMainLoopExceptionAsync(ea)
                    .ConfigureAwait(false);
            }
#endif
            catch (EndOfStreamException eose)
            {
                // Possible heartbeat exception
                var ea = new ShutdownEventArgs(ShutdownInitiator.Library,
                    0,
                    "End of stream",
                    exception: eose);
                await HandleMainLoopExceptionAsync(ea)
                    .ConfigureAwait(false);
            }
            catch (HardProtocolException hpe)
            {
                await HardProtocolExceptionHandlerAsync(hpe, mainLoopToken)
                    .ConfigureAwait(false);
            }
            catch (FileLoadException fileLoadException)
            {
                /*
                 * https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1434
                 * Ensure that these exceptions eventually make it to application code
                 */
                var ea = new ShutdownEventArgs(ShutdownInitiator.Library,
                    Constants.InternalError,
                    fileLoadException.Message,
                    exception: fileLoadException);
                await HandleMainLoopExceptionAsync(ea)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException ocex)
            {
                if (mainLoopToken.IsCancellationRequested)
                {
                    // ignore
                }
                else
                {
                    var ea = new ShutdownEventArgs(ShutdownInitiator.Library,
                        Constants.InternalError,
                        ocex.Message,
                        exception: ocex);
                    await HandleMainLoopExceptionAsync(ea)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                var ea = new ShutdownEventArgs(ShutdownInitiator.Library,
                    Constants.InternalError,
                    ex.Message,
                    exception: ex);
                await HandleMainLoopExceptionAsync(ea)
                    .ConfigureAwait(false);
            }

            using var cts = new CancellationTokenSource(InternalConstants.DefaultConnectionCloseTimeout);
            await FinishCloseAsync(cts.Token)
                .ConfigureAwait(false);
        }

        private async Task ReceiveLoopAsync(CancellationToken mainLoopCancellationToken)
        {
            InboundFrame frame = new InboundFrame();

            while (false == _closed)
            {
                mainLoopCancellationToken.ThrowIfCancellationRequested();

                while (_frameHandler.TryReadFrame(frame))
                {
                    NotifyHeartbeatListener();
                    await ProcessFrameAsync(frame, mainLoopCancellationToken)
                        .ConfigureAwait(false);
                }

                // Done reading frames synchronously, go async
                await _frameHandler.ReadFrameAsync(frame, mainLoopCancellationToken)
                    .ConfigureAwait(false);
                NotifyHeartbeatListener();
                await ProcessFrameAsync(frame, mainLoopCancellationToken)
                    .ConfigureAwait(false);
            }
        }

        private async Task ProcessFrameAsync(InboundFrame frame, CancellationToken cancellationToken)
        {
            if (frame.Channel == 0)
            {
                if (frame.Type == FrameType.FrameHeartbeat)
                {
                    // Ignore it: we've already recently reset the heartbeat
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
                    await _session0.HandleFrameAsync(frame, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            else
            {
                // If we're still m_running, but have a m_closeReason,
                // then we must be quiescing, which means any inbound
                // frames for non-zero channels (and any inbound
                // commands on channel zero that aren't
                // Connection.CloseOk) must be discarded.
                if (CloseReason is null)
                {
                    // No close reason, not quiescing the
                    // connection. Handle the frame. (Of course, the
                    // Session itself may be quiescing this particular
                    // channel, but that's none of our concern.)
                    ISession session = _sessionManager.Lookup(frame.Channel);
                    await session.HandleFrameAsync(frame, cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            frame.TryReturnPayload();
        }

        ///<remarks>
        /// May be called more than once. Should therefore be idempotent.
        ///</remarks>
        private void MaybeTerminateMainloopAndStopHeartbeatTimers(bool cancelMainLoop = false)
        {
            if (cancelMainLoop)
            {
                _mainLoopCts.Cancel();
            }
            MaybeStopHeartbeatTimers();
        }

        private async Task HandleMainLoopExceptionAsync(ShutdownEventArgs reason)
        {
            string message = reason.GetLogMessage();
            if (false == SetCloseReason(reason))
            {
                LogCloseError($"unexpected main loop exception while closing: {message}", reason.Exception!);
                return;
            }

            _channel0.MaybeSetConnectionStartException(reason.Exception!);

            await OnShutdownAsync(reason).ConfigureAwait(false);
            LogCloseError($"unexpected connection closure: {message}", reason.Exception!);
        }

        private async Task HardProtocolExceptionHandlerAsync(HardProtocolException hpe,
            CancellationToken mainLoopCancellationToken)
        {
            if (SetCloseReason(hpe.ShutdownReason))
            {
                await OnShutdownAsync(hpe.ShutdownReason).ConfigureAwait(false);
                await _session0.SetSessionClosingAsync(false, mainLoopCancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    var cmd = new ConnectionClose(hpe.ShutdownReason.ReplyCode, hpe.ShutdownReason.ReplyText, 0, 0);
                    await _session0.TransmitAsync(in cmd, mainLoopCancellationToken)
                        .ConfigureAwait(false);
                    if (hpe.CanShutdownCleanly)
                    {
                        await ClosingLoopAsync(mainLoopCancellationToken)
                           .ConfigureAwait(false);
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
        private async Task ClosingLoopAsync(CancellationToken mainLoopCancellationToken)
        {
            try
            {
                _frameHandler.ReadTimeout = default;
                // Wait for response/socket closure or timeout
                await ReceiveLoopAsync(mainLoopCancellationToken)
                   .ConfigureAwait(false);
            }
            catch (ObjectDisposedException ode)
            {
                if (false == _closed)
                {
                    LogCloseError("Connection didn't close cleanly", ode);
                }
            }
            catch (EndOfStreamException eose)
            {
                if (_channel0.CloseReason is null)
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
