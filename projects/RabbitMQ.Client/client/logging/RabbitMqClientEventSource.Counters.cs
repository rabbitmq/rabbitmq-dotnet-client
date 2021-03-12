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
using System.Diagnostics.Tracing;
using System.Threading;

namespace RabbitMQ.Client.Logging
{
    #nullable enable
    internal sealed partial class RabbitMqClientEventSource
    {
        private static int ConnectionsOpened;
        private static int ConnectionsClosed;
        private static int ChannelsOpened;
        private static int ChannelsClosed;
        private static long BytesSent;
        private static long BytesReceived;
        private static long CommandsSent;
        private static long CommandsReceived;

#if !NETSTANDARD
        private PollingCounter? _connectionOpenedCounter;
        private PollingCounter? _openConnectionCounter;
        private PollingCounter? _channelOpenedCounter;
        private PollingCounter? _openChannelCounter;
        private IncrementingPollingCounter? _bytesSentCounter;
        private IncrementingPollingCounter? _bytesReceivedCounter;
        private IncrementingPollingCounter? _commandSentCounter;
        private IncrementingPollingCounter? _commandReceivedCounter;

        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            if (command.Command == EventCommand.Enable)
            {
                _connectionOpenedCounter ??= new PollingCounter("total-connections-opened", this, () => ConnectionsOpened) { DisplayName = "Total connections opened" };
                _openConnectionCounter ??= new PollingCounter("current-open-connections", this, () => ConnectionsOpened - ConnectionsClosed) { DisplayName = "Current open connections count" };

                _channelOpenedCounter ??= new PollingCounter("total-channels-opened", this, () => ChannelsOpened) { DisplayName = "Total channels opened" };
                _openChannelCounter ??= new PollingCounter("current-open-channels", this, () => ChannelsOpened - ChannelsClosed) { DisplayName = "Current open channels count" };

                _bytesSentCounter ??= new IncrementingPollingCounter("bytes-sent-rate", this, () => Interlocked.Read(ref BytesSent)) { DisplayName = "Byte sending rate", DisplayUnits = "B", DisplayRateTimeScale = new TimeSpan(0, 0, 1) };
                _bytesReceivedCounter ??= new IncrementingPollingCounter("bytes-received-rate", this, () => Interlocked.Read(ref BytesReceived)) { DisplayName = "Byte receiving rate", DisplayUnits = "B", DisplayRateTimeScale = new TimeSpan(0, 0, 1) };

                _commandSentCounter ??= new IncrementingPollingCounter("AMQP-method-sent-rate", this, () => Interlocked.Read(ref CommandsSent)) { DisplayName = "AMQP method sending rate", DisplayUnits = "B", DisplayRateTimeScale = new TimeSpan(0, 0, 1) };
                _commandReceivedCounter ??= new IncrementingPollingCounter("AMQP-method-received-rate", this, () => Interlocked.Read(ref CommandsReceived)) { DisplayName = "AMQP method receiving rate", DisplayUnits = "B", DisplayRateTimeScale = new TimeSpan(0, 0, 1) };
            }
        }
#endif
        [NonEvent]
        public void ConnectionOpened()
        {
            Interlocked.Increment(ref ConnectionsOpened);
        }

        [NonEvent]
        public void ConnectionClosed()
        {
            Interlocked.Increment(ref ConnectionsClosed);
        }

        [NonEvent]
        public void ChannelOpened()
        {
            Interlocked.Increment(ref ChannelsOpened);
        }

        [NonEvent]
        public void ChannelClosed()
        {
            Interlocked.Increment(ref ChannelsClosed);
        }

        [NonEvent]
        public void DataReceived(int byteCount)
        {
            Interlocked.Add(ref BytesReceived, byteCount);
        }

        [NonEvent]
        public void CommandSent(int byteCount)
        {
            Interlocked.Increment(ref CommandsSent);
            Interlocked.Add(ref BytesSent, byteCount);
        }

        [NonEvent]
        public void CommandReceived()
        {
            Interlocked.Increment(ref CommandsReceived);
        }
    }
}
