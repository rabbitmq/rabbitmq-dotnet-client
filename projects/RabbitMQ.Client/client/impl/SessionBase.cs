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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    abstract class SessionBase : ISession
    {
        private readonly object _shutdownLock = new object();
        private AsyncEventHandler<ShutdownEventArgs> _sessionShutdown;

        public SessionBase(Connection connection, int channelNumber)
        {
            CloseReason = null;
            Connection = connection;
            ChannelNumber = channelNumber;
            if (channelNumber != 0)
            {
                connection.ConnectionShutdown += OnConnectionShutdown;
            }
        }

        public event AsyncEventHandler<ShutdownEventArgs> SessionShutdown
        {
            add
            {
                bool ok = false;
                if (CloseReason == null)
                {
                    lock (_shutdownLock)
                    {
                        if (CloseReason == null)
                        {
                            _sessionShutdown += value;
                            ok = true;
                        }
                    }
                }
                if (!ok)
                {
                    value(this, CloseReason);
                }
            }
            remove
            {
                lock (_shutdownLock)
                {
                    _sessionShutdown -= value;
                }
            }
        }

        public int ChannelNumber { get; private set; }
        public ShutdownEventArgs CloseReason { get; set; }
        public Func<ISession, Command, ValueTask> CommandReceived { get; set; }
        public Connection Connection { get; private set; }

        public bool IsOpen => CloseReason == null;

        public virtual ValueTask OnCommandReceived(Command cmd)
        {
            return (cmd is object && CommandReceived is object) ? CommandReceived(this, cmd) : default;
        }

        public virtual ValueTask OnConnectionShutdown(object conn, ShutdownEventArgs reason)
        {
            return Close(reason);
        }

        public virtual ValueTask OnSessionShutdown(ShutdownEventArgs reason)
        {
            Connection.ConnectionShutdown -= OnConnectionShutdown;
            AsyncEventHandler<ShutdownEventArgs> handler;
            lock (_shutdownLock)
            {
                handler = _sessionShutdown;
                _sessionShutdown = null;
            }

            return handler.InvokeAsync(this, reason);
        }

        public override string ToString()
        {
            return $"{GetType().Name}#{ChannelNumber}:{Connection}";
        }

        public ValueTask Close(ShutdownEventArgs reason)
        {
            return Close(reason, true);
        }

        public ValueTask Close(ShutdownEventArgs reason, bool notify)
        {
            if (CloseReason == null)
            {
                lock (_shutdownLock)
                {
                    if (CloseReason == null)
                    {
                        CloseReason = reason;
                    }
                }
            }

            if (notify)
            {
                OnSessionShutdown(CloseReason);
            }

            return default;
        }

        public abstract ValueTask HandleFrame(in InboundFrame frame);

        public void Notify()
        {
            // Ensure that we notify only when session is already closed
            // If not, throw exception, since this is a serious bug in the library
            if (CloseReason == null)
            {
                lock (_shutdownLock)
                {
                    if (CloseReason == null)
                    {
                        throw new Exception("Internal Error in Session.Close");
                    }
                }
            }
            OnSessionShutdown(CloseReason);
        }

        public virtual ValueTask Transmit(Command cmd)
        {
            if (CloseReason != null)
            {
                lock (_shutdownLock)
                {
                    if (CloseReason != null)
                    {
                        if (!Connection.Protocol.CanSendWhileClosed(cmd))
                        {
                            throw new AlreadyClosedException(CloseReason);
                        }
                    }
                }
            }

            // We used to transmit *inside* the lock to avoid interleaving
            // of frames within a channel.  But that is fixed in socket frame handler instead, so no need to lock.
            return Connection.Transmit(cmd, ChannelNumber);
        }

        public virtual ValueTask Transmit(IList<Command> commands)
        {
            return Connection.Transmit(commands, ChannelNumber);
        }
    }
}
