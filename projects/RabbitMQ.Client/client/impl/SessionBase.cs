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
using System.Threading;

using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    internal abstract class SessionBase : ISession
    {
        private ShutdownEventArgs _closeReason;
        public ShutdownEventArgs CloseReason => Volatile.Read(ref _closeReason);

        protected SessionBase(Connection connection, ushort channelNumber)
        {
            Connection = connection;
            ChannelNumber = channelNumber;
            if (channelNumber != 0)
            {
                connection.ConnectionShutdown += OnConnectionShutdown;
            }
        }

        public event EventHandler<ShutdownEventArgs> SessionShutdown
        {
            add
            {
                if (CloseReason is null)
                {
                    _sessionShutdownWrapper.AddHandler(value);
                }
                else
                {
                    value(this, CloseReason);
                }
            }
            remove
            {
                _sessionShutdownWrapper.RemoveHandler(value);
            }
        }
        private EventingWrapper<ShutdownEventArgs> _sessionShutdownWrapper;

        public ushort ChannelNumber { get; }

        public CommandReceivedAction CommandReceived { get; set; }
        public Connection Connection { get; }

        public bool IsOpen => CloseReason is null;

        public virtual void OnConnectionShutdown(object conn, ShutdownEventArgs reason)
        {
            Close(reason);
        }

        public virtual void OnSessionShutdown(ShutdownEventArgs reason)
        {
            Connection.ConnectionShutdown -= OnConnectionShutdown;
            _sessionShutdownWrapper.Invoke(this, reason);
        }

        public override string ToString()
        {
            return $"{GetType().Name}#{ChannelNumber}:{Connection}";
        }

        public void Close(ShutdownEventArgs reason)
        {
            Close(reason, true);
        }

        public void Close(ShutdownEventArgs reason, bool notify)
        {
            Interlocked.CompareExchange(ref _closeReason, reason, null);
            if (notify)
            {
                OnSessionShutdown(CloseReason);
            }
        }

        public abstract bool HandleFrame(in InboundFrame frame);

        public void Notify()
        {
            // Ensure that we notify only when session is already closed
            // If not, throw exception, since this is a serious bug in the library
            var reason = CloseReason;
            if (reason is null)
            {
                throw new Exception("Internal Error in Session.Close");
            }

            OnSessionShutdown(reason);
        }

        public virtual void Transmit<T>(in T cmd) where T : struct, IOutgoingCommand
        {
            if (cmd.Method.ProtocolCommandId != client.framing.ProtocolCommandId.ChannelCloseOk && !IsOpen)
            {
                throw new AlreadyClosedException(CloseReason);
            }

            // We used to transmit *inside* the lock to avoid interleaving
            // of frames within a channel.  But that is fixed in socket frame handler instead, so no need to lock.
            Connection.Write(cmd.SerializeToFrames(ChannelNumber, Connection.FrameMax));
        }

        public virtual void Transmit<T>(List<T> cmds) where T : struct, IOutgoingCommand
        {
            uint frameMax = Connection.FrameMax;
            for (int i = 0; i < cmds.Count; i++)
            {
                Connection.Write(cmds[i].SerializeToFrames(ChannelNumber, frameMax));
            }
        }
    }
}
