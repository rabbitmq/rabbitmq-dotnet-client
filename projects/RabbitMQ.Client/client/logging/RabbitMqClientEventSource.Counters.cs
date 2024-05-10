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

using System.Diagnostics.Tracing;
using System.Threading;

namespace RabbitMQ.Client.Logging
{
    internal sealed partial class RabbitMqClientEventSource
    {
        private static int s_connectionsOpened;
        private static int s_connectionsClosed;
        private static int s_channelsOpened;
        private static int s_channelsClosed;
        private static long s_bytesSent;
        private static long s_bytesReceived;
        private static long s_commandsSent;
        private static long s_commandsReceived;

        [NonEvent]
        public void ConnectionOpened()
        {
            Interlocked.Increment(ref s_connectionsOpened);
        }

        [NonEvent]
        public void ConnectionClosed()
        {
            Interlocked.Increment(ref s_connectionsClosed);
        }

        [NonEvent]
        public void ChannelOpened()
        {
            Interlocked.Increment(ref s_channelsOpened);
        }

        [NonEvent]
        public void ChannelClosed()
        {
            Interlocked.Increment(ref s_channelsClosed);
        }

        [NonEvent]
        public void DataReceived(int byteCount)
        {
            Interlocked.Add(ref s_bytesReceived, byteCount);
        }

        [NonEvent]
        public void CommandSent(int byteCount)
        {
            Interlocked.Increment(ref s_commandsSent);
            Interlocked.Add(ref s_bytesSent, byteCount);
        }

        [NonEvent]
        public void CommandReceived()
        {
            Interlocked.Increment(ref s_commandsReceived);
        }
    }
}
