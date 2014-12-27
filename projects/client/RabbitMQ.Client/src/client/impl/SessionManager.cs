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
using System.Collections.Generic;
using System.Threading;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Util;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Impl
{
    public class SessionManager
    {
        public readonly ushort ChannelMax;
        private readonly IntAllocator Ints;
        private readonly Connection m_connection;
        private readonly IDictionary<int, ISession> m_sessionMap = new Dictionary<int, ISession>();
        private bool m_autoClose = false;

        public SessionManager(Connection connection, ushort channelMax)
        {
            m_connection = connection;
            ChannelMax = (channelMax == 0) ? ushort.MaxValue : channelMax;
            Ints = new IntAllocator(1, ChannelMax);
        }

        public bool AutoClose
        {
            get { return m_autoClose; }
            set
            {
                m_autoClose = value;
                CheckAutoClose();
            }
        }

        public int Count
        {
            get
            {
                lock (m_sessionMap)
                {
                    return m_sessionMap.Count;
                }
            }
        }

        ///<summary>Called from CheckAutoClose, in a separate thread,
        ///when we decide to close the connection.</summary>
        public void AutoCloseConnection()
        {
            m_connection.Abort(Constants.ReplySuccess, "AutoClose", ShutdownInitiator.Library, Timeout.Infinite);
        }

        ///<summary>If m_autoClose and there are no active sessions
        ///remaining, Close()s the connection with reason code
        ///200.</summary>
        public void CheckAutoClose()
        {
            if (m_autoClose)
            {
                lock (m_sessionMap)
                {
                    if (m_sessionMap.Count == 0)
                    {
                        // Run this in a background thread, because
                        // usually CheckAutoClose will be called from
                        // HandleSessionShutdown above, which runs in
                        // the thread of the connection. If we were to
                        // attempt to close the connection from within
                        // the connection's thread, we would suffer a
                        // deadlock as the connection thread would be
                        // blocking waiting for its own mainloop to
                        // reply to it.
                        new Thread(AutoCloseConnection).Start();
                    }
                }
            }
        }

        public ISession Create()
        {
            lock (m_sessionMap)
            {
                int channelNumber = Ints.Allocate();
                if (channelNumber == -1)
                {
                    throw new ChannelAllocationException();
                }
                return CreateInternal(channelNumber);
            }
        }

        public ISession Create(int channelNumber)
        {
            lock (m_sessionMap)
            {
                if (!Ints.Reserve(channelNumber))
                {
                    throw new ChannelAllocationException(channelNumber);
                }
                return CreateInternal(channelNumber);
            }
        }

        public ISession CreateInternal(int channelNumber)
        {
            lock (m_sessionMap)
            {
                ISession session = new Session(m_connection, channelNumber);
                session.SessionShutdown += HandleSessionShutdown;
                m_sessionMap[channelNumber] = session;
                return session;
            }
        }

        public void HandleSessionShutdown(ISession session, ShutdownEventArgs reason)
        {
            lock (m_sessionMap)
            {
                m_sessionMap.Remove(session.ChannelNumber);
                Ints.Free(session.ChannelNumber);
                CheckAutoClose();
            }
        }

        public ISession Lookup(int number)
        {
            lock (m_sessionMap)
            {
                return m_sessionMap[number];
            }
        }

        ///<summary>Replace an active session slot with a new ISession
        ///implementation. Used during channel quiescing.</summary>
        ///<remarks>
        /// Make sure you pass in a channelNumber that's currently in
        /// use, as if the slot is unused, you'll get a null pointer
        /// exception.
        ///</remarks>
        public ISession Swap(int channelNumber, ISession replacement)
        {
            lock (m_sessionMap)
            {
                ISession previous = m_sessionMap[channelNumber];
                previous.SessionShutdown -= HandleSessionShutdown;
                m_sessionMap[channelNumber] = replacement;
                replacement.SessionShutdown += HandleSessionShutdown;
                return previous;
            }
        }
    }
}
