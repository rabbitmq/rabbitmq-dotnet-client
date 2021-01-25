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

using System.Collections.Generic;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    internal class SessionManager
    {
        public readonly ushort ChannelMax;
        private readonly IntAllocator _ints;
        private readonly Connection _connection;
        private readonly Dictionary<int, ISession> _sessionMap = new Dictionary<int, ISession>();

        public SessionManager(Connection connection, ushort channelMax)
        {
            _connection = connection;
            ChannelMax = (channelMax == 0) ? ushort.MaxValue : channelMax;
            _ints = new IntAllocator(1, ChannelMax);
        }

        public int Count
        {
            get
            {
                lock (_sessionMap)
                {
                    return _sessionMap.Count;
                }
            }
        }

        public ISession Create()
        {
            lock (_sessionMap)
            {
                int channelNumber = _ints.Allocate();
                if (channelNumber == -1)
                {
                    throw new ChannelAllocationException();
                }

                ISession session = new Session(_connection, (ushort)channelNumber);
                session.SessionShutdown += HandleSessionShutdown;
                _sessionMap[channelNumber] = session;
                return session;
            }
        }

        private void HandleSessionShutdown(object sender, ShutdownEventArgs reason)
        {
            lock (_sessionMap)
            {
                var session = (ISession)sender;
                _sessionMap.Remove(session.ChannelNumber);
                _ints.Free(session.ChannelNumber);
            }
        }

        public ISession Lookup(int number)
        {
            lock (_sessionMap)
            {
                return _sessionMap[number];
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
            lock (_sessionMap)
            {
                ISession previous = _sessionMap[channelNumber];
                previous.SessionShutdown -= HandleSessionShutdown;
                _sessionMap[channelNumber] = replacement;
                replacement.SessionShutdown += HandleSessionShutdown;
                return previous;
            }
        }
    }
}
