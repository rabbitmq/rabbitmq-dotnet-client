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
using System.Collections.Generic;

using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    internal delegate void CommandReceivedAction(in IncomingCommand cmd);

    internal interface ISession
    {
        /// <summary>
        /// Gets the channel number.
        /// </summary>
        ushort ChannelNumber { get; }

        /// <summary>
        /// Gets the close reason.
        /// </summary>
        ShutdownEventArgs CloseReason { get; }

        ///<summary>
        /// Single recipient - no need for multiple handlers to be informed of arriving commands.
        ///</summary>
        CommandReceivedAction CommandReceived { get; set; }

        /// <summary>
        /// Gets the connection.
        /// </summary>
        Connection Connection { get; }

        /// <summary>
        /// Gets a value indicating whether this session is open.
        /// </summary>
        bool IsOpen { get; }

        ///<summary>
        /// Multicast session shutdown event.
        ///</summary>
        event EventHandler<ShutdownEventArgs> SessionShutdown;

        void Close(ShutdownEventArgs reason);
        void Close(ShutdownEventArgs reason, bool notify);
        bool HandleFrame(in InboundFrame frame);
        void Notify();
        void Transmit<T>(T cmd) where T : MethodBase;
        void Transmit<TMethod, THeader>(TMethod cmd, THeader header, ReadOnlyMemory<byte> body) where TMethod : MethodBase where THeader : ContentHeaderBase;
        void Transmit(List<OutgoingContentCommand> cmds);
    }
}
