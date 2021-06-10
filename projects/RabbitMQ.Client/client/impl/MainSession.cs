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

using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    ///<summary>Small ISession implementation used only for channel 0.</summary>
    internal sealed class MainSession : Session
    {
        private volatile bool _closeServerInitiated;
        private volatile bool _closing;
        private readonly object _lock = new object();

        public MainSession(Connection connection) : base(connection, 0)
        {
        }

        public override bool HandleFrame(in InboundFrame frame)
        {
            if (_closing)
            {
                // We are closing
                if (!_closeServerInitiated && frame.Type == FrameType.FrameMethod)
                {
                    // This isn't a server initiated close and we have a method frame
                    switch (Connection.Protocol.DecodeCommandIdFrom(frame.Payload.Span))
                    {
                        case ProtocolCommandId.ConnectionClose:
                            return base.HandleFrame(in frame);
                        case ProtocolCommandId.ConnectionCloseOk:
                            // This is the reply (CloseOk) we were looking for
                            // Call any listener attached to this session
                            Connection.NotifyReceivedCloseOk();
                            break;
                    }
                }

                // Either a non-method frame, or not what we were looking
                // for. Ignore it - we're quiescing.
                return true;
            }

            return base.HandleFrame(in frame);
        }

        ///<summary> Set channel 0 as quiescing </summary>
        ///<remarks>
        /// Method should be idempotent. Cannot use base.Close
        /// method call because that would prevent us from
        /// sending/receiving Close/CloseOk commands
        ///</remarks>
        public void SetSessionClosing(bool closeServerInitiated)
        {
            if (!_closing)
            {
                lock (_lock)
                {
                    if (!_closing)
                    {
                        _closing = true;
                        _closeServerInitiated = closeServerInitiated;
                    }
                }
            }
        }

        public override void Transmit<T>(in T cmd)
        {
            if (_closing && // Are we closing?
                cmd.ProtocolCommandId != ProtocolCommandId.ConnectionCloseOk && // is this not a close-ok?
                (_closeServerInitiated || cmd.ProtocolCommandId != ProtocolCommandId.ConnectionClose)) // is this either server initiated or not a close?
            {
                // We shouldn't do anything since we are closing, not sending a connection-close-ok command
                // and this is either a server-initiated close or not a connection-close command.
                return;
            }

            base.Transmit(in cmd);
        }
    }
}
