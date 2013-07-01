// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2013 GoPivotal, Inc.
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
//  Copyright (c) 2007-2013 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace RabbitMQ.Client.Impl
{
    public abstract class SessionBase: ISession
    {
        private CommandHandler m_commandReceived;

        private readonly object m_shutdownLock = new object();
        private SessionShutdownEventHandler m_sessionShutdown;
        public ShutdownEventArgs m_closeReason = null;

        public readonly ConnectionBase m_connection;
        public readonly int m_channelNumber;

        public SessionBase(ConnectionBase connection, int channelNumber)
        {
            m_connection = connection;
            m_channelNumber = channelNumber;
            if (channelNumber != 0)
                connection.ConnectionShutdown +=
                    new ConnectionShutdownEventHandler(this.OnConnectionShutdown);
        }

        public virtual void OnCommandReceived(Command cmd)
        {
            CommandHandler handler = CommandReceived;
            if (handler != null)
            {
                handler(this, cmd);
            }
        }

        public virtual void OnConnectionShutdown(IConnection conn, ShutdownEventArgs reason)
        {
            Close(reason);
        }

        public virtual void OnSessionShutdown(ShutdownEventArgs reason)
        {
            //Console.WriteLine("Session shutdown "+ChannelNumber+": "+reason);
            m_connection.ConnectionShutdown -=
                new ConnectionShutdownEventHandler(this.OnConnectionShutdown);
            SessionShutdownEventHandler handler;
            lock (m_shutdownLock)
            {
                handler = m_sessionShutdown;
                m_sessionShutdown = null;
            }
            if (handler != null)
            {
                handler(this, reason);
            }
        }

        public override string ToString()
        {
            return this.GetType().Name+"#" + m_channelNumber + ":" + m_connection;
        }

        //---------------------------------------------------------------------------
        // ISession implementation

        public CommandHandler CommandReceived
        {
            get { return m_commandReceived; }
            set { m_commandReceived = value; }
        }

        public event SessionShutdownEventHandler SessionShutdown
        {
            add
            {
                bool ok = false;
                lock (m_shutdownLock)
                {
                    if (m_closeReason == null)
                    {
                        m_sessionShutdown += value;
                        ok = true;
                    }
                }
                if (!ok)
                {
                    value(this, m_closeReason);
                }
            }
            remove
            {
                lock (m_shutdownLock)
                {
                    m_sessionShutdown -= value;
                }
            }
        }

        public int ChannelNumber { get { return m_channelNumber; } }

        IConnection ISession.Connection { get { return m_connection; } }
        public ConnectionBase Connection { get { return m_connection; } }

        public ShutdownEventArgs CloseReason { get { return m_closeReason; } }

        public bool IsOpen { get { return m_closeReason == null; } }

        public abstract void HandleFrame(Frame frame);

        public virtual void Transmit(Command cmd)
        {
            lock (m_shutdownLock)
            {
                if (m_closeReason != null)
                {
                    if (!m_connection.Protocol.CanSendWhileClosed(cmd))
                  	    throw new AlreadyClosedException(m_closeReason);
                }
                // We transmit *inside* the lock to avoid interleaving
                // of frames within a channel.
                cmd.Transmit(m_channelNumber, m_connection);
            }
        }

        public void Close(ShutdownEventArgs reason)
        {
            Close(reason, true);
        }
        
        public void Close(ShutdownEventArgs reason, bool notify)
        {
            lock (m_shutdownLock)
            {
                if (m_closeReason == null)
                {
                    m_closeReason = reason;
                }
            }
            if (notify)
                OnSessionShutdown(m_closeReason);
        }
        
        public void Notify()
        {
            // Ensure that we notify only when session is already closed
            // If not, throw exception, since this is a serious bug in the library
            lock (m_shutdownLock)
            {
        	    if (m_closeReason == null)
                    throw new Exception("Internal Error in Session.Close");   	
            }
            OnSessionShutdown(m_closeReason);
        }
    }
}
