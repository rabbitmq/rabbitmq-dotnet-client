// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2014 GoPivotal, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
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
//  at http://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    public abstract class SessionBase : ISession
    {
        private readonly object _shutdownLock = new object();
        private EventHandler<ShutdownEventArgs> _sessionShutdown;

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

        public event EventHandler<ShutdownEventArgs> SessionShutdown
        {
            add
            {
                bool ok = false;
                lock (_shutdownLock)
                {
                    if (CloseReason == null)
                    {
                        _sessionShutdown += value;
                        ok = true;
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
        public Action<ISession, Command> CommandReceived { get; set; }
        public Connection Connection { get; private set; }

        public bool IsOpen
        {
            get { return CloseReason == null; }
        }

        IConnection ISession.Connection
        {
            get { return Connection; }
        }

        public virtual void OnCommandReceived(Command cmd)
        {
            Action<ISession, Command> handler = CommandReceived;
            if (handler != null)
            {
                handler(this, cmd);
            }
        }

        public virtual void OnConnectionShutdown(object conn, ShutdownEventArgs reason)
        {
            Close(reason);
        }

        public virtual void OnSessionShutdown(ShutdownEventArgs reason)
        {
            Connection.ConnectionShutdown -= OnConnectionShutdown;
            EventHandler<ShutdownEventArgs> handler;
            lock (_shutdownLock)
            {
                handler = _sessionShutdown;
                _sessionShutdown = null;
            }
            if (handler != null)
            {
                handler(this, reason);
            }
        }

        public override string ToString()
        {
            return GetType().Name + "#" + ChannelNumber + ":" + Connection;
        }

        public void Close(ShutdownEventArgs reason)
        {
            Close(reason, true);
        }

        public void Close(ShutdownEventArgs reason, bool notify)
        {
            lock (_shutdownLock)
            {
                if (CloseReason == null)
                {
                    CloseReason = reason;
                }
            }
            if (notify)
            {
                OnSessionShutdown(CloseReason);
            }
        }

        public abstract void HandleFrame(Frame frame);

        public void Notify()
        {
            // Ensure that we notify only when session is already closed
            // If not, throw exception, since this is a serious bug in the library
            lock (_shutdownLock)
            {
                if (CloseReason == null)
                {
                    throw new Exception("Internal Error in Session.Close");
                }
            }
            OnSessionShutdown(CloseReason);
        }

        public virtual void Transmit(Command cmd)
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
                // We transmit *inside* the lock to avoid interleaving
                // of frames within a channel.
                cmd.Transmit(ChannelNumber, Connection);
            }
        }
    }
}
