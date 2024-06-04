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
using System.Threading;

namespace RabbitMQ.Client.Framing.Impl
{
#nullable enable
    internal sealed partial class Connection
    {
        private TimeSpan _heartbeat;
        private TimeSpan _heartbeatWriteTimeSpan;
        private int _missedHeartbeats;
        private bool _heartbeatDetected;

        private Timer? _heartbeatWriteTimer;
        private Timer? _heartbeatReadTimer;

        public TimeSpan Heartbeat
        {
            get => _heartbeat;
            set
            {
                _heartbeat = value;
                // timers fire at slightly below half the interval to avoid race
                // conditions
                _heartbeatWriteTimeSpan = TimeSpan.FromMilliseconds(_heartbeat.TotalMilliseconds / 2);
                _frameHandler.ReadTimeout = TimeSpan.FromMilliseconds(_heartbeat.TotalMilliseconds * 2);
            }
        }

        private void MaybeStartHeartbeatTimers()
        {
            if (Heartbeat != TimeSpan.Zero)
            {
                _heartbeatWriteTimer ??= new Timer(HeartbeatWriteTimerCallback, null, 200, Timeout.Infinite);
                _heartbeatReadTimer ??= new Timer(HeartbeatReadTimerCallback, null, 300, Timeout.Infinite);
            }
        }

        private void MaybeStopHeartbeatTimers()
        {
            NotifyHeartbeatListener();
            _heartbeatReadTimer?.Dispose();
            _heartbeatWriteTimer?.Dispose();
        }

        private void NotifyHeartbeatListener()
        {
            _heartbeatDetected = true;
        }

        private async void HeartbeatReadTimerCallback(object? state)
        {
            if (_heartbeatReadTimer is null)
            {
                return;
            }

            bool shouldTerminate = false;

            try
            {
                if (false == _closed)
                {
                    if (_heartbeatDetected)
                    {
                        _heartbeatDetected = false;
                        _missedHeartbeats = 0;
                    }
                    else
                    {
                        _missedHeartbeats++;
                    }

                    // We need to wait for at least two complete heartbeat setting
                    // intervals before complaining
                    if (_missedHeartbeats > 2)
                    {
                        var eose = new EndOfStreamException($"Heartbeat missing with heartbeat == {_heartbeat} seconds");
                        LogCloseError(eose.Message, eose);
                        HandleMainLoopException(new ShutdownEventArgs(ShutdownInitiator.Library, 0, "End of stream", eose));
                        shouldTerminate = true;
                    }
                }

                if (shouldTerminate)
                {
                    MaybeTerminateMainloopAndStopHeartbeatTimers();
                    /*
                     * Note: do NOT use the main loop cancellation token,
                     * because FininshCloseAsync immediately cancels it
                     */
                    using var cts = new CancellationTokenSource(InternalConstants.DefaultConnectionAbortTimeout);
                    await FinishCloseAsync(cts.Token)
                        .ConfigureAwait(false);
                }
                else
                {
                    _heartbeatReadTimer?.Change((int)Heartbeat.TotalMilliseconds, Timeout.Infinite);
                }
            }
            catch (OperationCanceledException)
            {
                if (false == _mainLoopCts.IsCancellationRequested)
                {
                    throw;
                }
            }
            catch (ObjectDisposedException)
            {
                // timer is already disposed,
                // e.g. due to shutdown
            }
            catch (NullReferenceException)
            {
                // timer has already been disposed from a different thread after null check
                // this event should be rare
            }
        }

        private async void HeartbeatWriteTimerCallback(object? state)
        {
            if (_heartbeatWriteTimer is null)
            {
                return;
            }

            try
            {
                if (false == _closed)
                {
                    await WriteAsync(Client.Impl.Framing.Heartbeat.GetHeartbeatFrame(), _mainLoopCts.Token)
                        .ConfigureAwait(false);
                    _heartbeatWriteTimer?.Change((int)_heartbeatWriteTimeSpan.TotalMilliseconds, Timeout.Infinite);
                }
            }
            catch (ObjectDisposedException)
            {
                // timer is already disposed,
                // e.g. due to shutdown
            }
            catch (Exception)
            {
                // ignore, let the read callback detect
                // peer unavailability. See rabbitmq/rabbitmq-dotnet-client#638 for details.
            }
        }
    }
}
