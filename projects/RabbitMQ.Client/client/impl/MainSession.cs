// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at https://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

// We use spec version 0-9 for common constants such as frame types,
// error codes, and the frame end byte, since they don't vary *within
// the versions we support*. Obviously we may need to revisit this if
// that ever changes.

using System;
using System.Threading.Tasks;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    ///<summary>Small ISession implementation used only for channel 0.</summary>
    class MainSession : Session
    {
        private readonly object _closingLock = new object();

        private bool _closeServerInitiated;
        private bool _closing;

        public MainSession(Connection connection) : base(connection, 0)
        {
        }

        public Action Handler { get; set; }

        public override ValueTask HandleFrame(in InboundFrame frame)
        {
            lock (_closingLock)
            {
                if (!_closing)
                {
                    return base.HandleFrame(in frame);
                }
            }

            if (!_closeServerInitiated && frame.IsMethod())
            {
                MethodBase method = Connection.Protocol.DecodeMethodFrom(frame.Payload);
                if ((method.ProtocolClassId == ClassConstants.Connection) && (method.ProtocolMethodId == ConnectionMethodConstants.Close))
                {
                    return base.HandleFrame(in frame);
                }

                if ((method.ProtocolClassId == ClassConstants.Connection) && (method.ProtocolMethodId == ConnectionMethodConstants.CloseOk))
                {
                    // This is the reply (CloseOk) we were looking for
                    // Call any listener attached to this session
                    Handler();
                }
            }

            return default;

            // Either a non-method frame, or not what we were looking
            // for. Ignore it - we're quiescing.
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

        public override ValueTask Transmit(Command cmd)
        {
            // Allow always for sending close ok
            // Or if application initiated, allow also for sending close
            MethodBase method = cmd.Method;
            if (((method.ProtocolClassId == ClassConstants.Connection) && (method.ProtocolMethodId == ConnectionMethodConstants.CloseOk))
                ||
                (!_closeServerInitiated && (method.ProtocolClassId == ClassConstants.Connection) && (method.ProtocolMethodId == ConnectionMethodConstants.Close)))
            {
                return base.Transmit(cmd);
            }

            lock (_closingLock)
            {
                if (!_closing)
                {
                    return base.Transmit(cmd);
                }
            }

            return default;
        }
    }
}
