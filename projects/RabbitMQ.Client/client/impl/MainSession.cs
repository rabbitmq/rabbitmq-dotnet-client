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

// We use spec version 0-9 for common constants such as frame types,
// error codes, and the frame end byte, since they don't vary *within
// the versions we support*. Obviously we may need to revisit this if
// that ever changes.

using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    ///<summary>Small ISession implementation used only for channel 0.</summary>
    internal sealed class MainSession : Session, IDisposable
    {
        private volatile bool _closeIsServerInitiated;
        private volatile bool _closing;
        private readonly SemaphoreSlim _closingSemaphore = new SemaphoreSlim(1, 1);

        public MainSession(Connection connection, uint maxBodyLength)
            : base(connection, 0, maxBodyLength)
        {
        }

        public override Task<bool> HandleFrameAsync(InboundFrame frame, CancellationToken cancellationToken)
        {
            if (_closing)
            {
                // We are closing
                if ((false == _closeIsServerInitiated) && (frame.Type == FrameType.FrameMethod))
                {
                    // This isn't a server initiated close and we have a method frame
                    switch (Connection.Protocol.DecodeCommandIdFrom(frame.Payload.Span))
                    {
                        case ProtocolCommandId.ConnectionClose:
                            return base.HandleFrameAsync(frame, cancellationToken);
                        case ProtocolCommandId.ConnectionCloseOk:
                            // This is the reply (CloseOk) we were looking for
                            // Call any listener attached to this session
                            Connection.NotifyReceivedCloseOk();
                            break;
                    }
                }

                // Either a non-method frame, or not what we were looking
                // for. Ignore it - we're quiescing.
                return Task.FromResult(true);
            }

            return base.HandleFrameAsync(frame, cancellationToken);
        }

        ///<summary> Set channel 0 as quiescing </summary>
        ///<remarks>
        /// Method should be idempotent. Cannot use base.Close
        /// method call because that would prevent us from
        /// sending/receiving Close/CloseOk commands
        ///</remarks>
        public void SetSessionClosing(bool closeIsServerInitiated)
        {
            if (_closingSemaphore.Wait(InternalConstants.DefaultConnectionAbortTimeout))
            {
                try
                {
                    if (false == _closing)
                    {
                        _closing = true;
                        _closeIsServerInitiated = closeIsServerInitiated;
                    }
                }
                finally
                {
                    _closingSemaphore.Release();
                }
            }
            else
            {
                throw new InvalidOperationException("couldn't enter semaphore");
            }
        }

        public async Task SetSessionClosingAsync(bool closeIsServerInitiated)
        {
            if (await _closingSemaphore.WaitAsync(InternalConstants.DefaultConnectionAbortTimeout).ConfigureAwait(false))
            {
                try
                {
                    if (false == _closing)
                    {
                        _closing = true;
                        _closeIsServerInitiated = closeIsServerInitiated;
                    }
                }
                finally
                {
                    _closingSemaphore.Release();
                }
            }
            else
            {
                throw new InvalidOperationException("couldn't async enter semaphore");
            }
        }

        public override ValueTask TransmitAsync<T>(in T cmd, CancellationToken cancellationToken)
        {
            // Are we closing?
            if (_closing)
            {
                if ((cmd.ProtocolCommandId != ProtocolCommandId.ConnectionCloseOk) && // is this not a close-ok?
                    (_closeIsServerInitiated || cmd.ProtocolCommandId != ProtocolCommandId.ConnectionClose)) // is this either server initiated or not a close?
                {
                    // We shouldn't do anything since we are closing, not sending a connection-close-ok command
                    // and this is either a server-initiated close or not a connection-close command.
                    return default;
                }
            }

            return base.TransmitAsync(in cmd, cancellationToken);
        }

        public void Dispose() => ((IDisposable)_closingSemaphore).Dispose();
    }
}
