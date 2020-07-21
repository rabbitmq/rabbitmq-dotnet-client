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

using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    ///<summary>Small ISession implementation used only for channel 0.</summary>
    internal sealed class MainSession : Session
    {
        private readonly object _closingLock = new object();

        private readonly ushort _closeClassId;
        private readonly ushort _closeMethodId;
        private readonly ushort _closeOkClassId;
        private readonly ushort _closeOkMethodId;

        private bool _closeServerInitiated;
        private bool _closing;

        public MainSession(Connection connection) : base(connection, 0)
        {
            connection.Protocol.CreateConnectionClose(0, string.Empty, out OutgoingCommand request, out _closeOkClassId, out _closeOkMethodId);
            _closeClassId = request.Method.ProtocolClassId;
            _closeMethodId = request.Method.ProtocolMethodId;
        }

        public Action Handler { get; set; }

        public override bool HandleFrame(in InboundFrame frame)
        {
            lock (_closingLock)
            {
                if (!_closing)
                {
                    return base.HandleFrame(in frame);
                }
            }

            if (!_closeServerInitiated && frame.Type == FrameType.FrameMethod)
            {
                MethodBase method = Connection.Protocol.DecodeMethodFrom(frame.Payload.Span);
                if (method.ProtocolClassId == _closeClassId && method.ProtocolMethodId == _closeMethodId)
                {
                    return base.HandleFrame(in frame);
                }

                if (method.ProtocolClassId == _closeOkClassId && method.ProtocolMethodId == _closeOkMethodId)
                {
                    // This is the reply (CloseOk) we were looking for
                    // Call any listener attached to this session
                    Handler();
                }
            }

            // Either a non-method frame, or not what we were looking
            // for. Ignore it - we're quiescing.
            return true;
        }

        ///<summary> Set channel 0 as quiescing </summary>
        ///<remarks>
        /// Method should be idempotent. Cannot use base.Close
        /// method call because that would prevent us from
        /// sending/receiving Close/CloseOk commands
        ///</remarks>
        public void SetSessionClosing(bool closeServerInitiated)
        {
            lock (_closingLock)
            {
                if (!_closing)
                {
                    _closing = true;
                    _closeServerInitiated = closeServerInitiated;
                }
            }
        }

        public override void Transmit(in OutgoingCommand cmd)
        {
            lock (_closingLock)
            {
                if (!_closing)
                {
                    base.Transmit(in cmd);
                    return;
                }
            }

            // Allow always for sending close ok
            // Or if application initiated, allow also for sending close
            MethodBase method = cmd.Method;
            if (((method.ProtocolClassId == _closeOkClassId)
                 && (method.ProtocolMethodId == _closeOkMethodId))
                || (!_closeServerInitiated &&
                    (method.ProtocolClassId == _closeClassId) &&
                    (method.ProtocolMethodId == _closeMethodId)
                    ))
            {
                base.Transmit(cmd);
            }
        }
    }
}
