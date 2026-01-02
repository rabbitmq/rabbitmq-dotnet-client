// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Threading;

namespace RabbitMQ.Client.Logging
{
    internal sealed partial class RabbitMqClientEventSource : EventSource
    {
        public static readonly RabbitMqClientEventSource Log = new RabbitMqClientEventSource();

#if NET
        private readonly PollingCounter _connectionOpenedCounter;
        private readonly PollingCounter _openConnectionCounter;
        private readonly PollingCounter _channelOpenedCounter;
        private readonly PollingCounter _openChannelCounter;
        private readonly IncrementingPollingCounter _bytesSentCounter;
        private readonly IncrementingPollingCounter _bytesReceivedCounter;
        private readonly IncrementingPollingCounter _commandSentCounter;
        private readonly IncrementingPollingCounter _commandReceivedCounter;
#endif

        public RabbitMqClientEventSource()
            : base("rabbitmq-client")
        {
#if NET
            _connectionOpenedCounter = new PollingCounter("total-connections-opened", this, () => s_connectionsOpened)
            {
                DisplayName = "Total connections opened"
            };
            _openConnectionCounter = new PollingCounter("current-open-connections", this, () => s_connectionsOpened - s_connectionsClosed)
            {
                DisplayName = "Current open connections count"
            };

            _channelOpenedCounter = new PollingCounter("total-channels-opened", this, () => s_channelsOpened)
            {
                DisplayName = "Total channels opened"
            };
            _openChannelCounter = new PollingCounter("current-open-channels", this, () => s_channelsOpened - s_channelsClosed)
            {
                DisplayName = "Current open channels count"
            };

            _bytesSentCounter = new IncrementingPollingCounter("bytes-sent-rate", this, () => Interlocked.Read(ref s_bytesSent))
            {
                DisplayName = "Byte sending rate",
                DisplayUnits = "B",
                DisplayRateTimeScale = new TimeSpan(0, 0, 1)
            };
            _bytesReceivedCounter = new IncrementingPollingCounter("bytes-received-rate", this, () => Interlocked.Read(ref s_bytesReceived))
            {
                DisplayName = "Byte receiving rate",
                DisplayUnits = "B",
                DisplayRateTimeScale = new TimeSpan(0, 0, 1)
            };

            _commandSentCounter = new IncrementingPollingCounter("AMQP-method-sent-rate", this, () => Interlocked.Read(ref s_commandsSent))
            {
                DisplayName = "AMQP method sending rate",
                DisplayUnits = "B",
                DisplayRateTimeScale = new TimeSpan(0, 0, 1)
            };
            _commandReceivedCounter = new IncrementingPollingCounter("AMQP-method-received-rate", this, () => Interlocked.Read(ref s_commandsReceived))
            {
                DisplayName = "AMQP method receiving rate",
                DisplayUnits = "B",
                DisplayRateTimeScale = new TimeSpan(0, 0, 1)
            };
#endif
        }

        public class Keywords
        {
            public const EventKeywords Log = (EventKeywords)1;
        }

        /*
         * Note that it appears Message format strings do not work as documented:
         * https://github.com/dotnet/runtime/issues/99274
         */
        [Event(1, Keywords = Keywords.Log, Level = EventLevel.Informational)]
        public void Info(string message)
        {
            if (IsEnabled())
            {
                WriteEvent(1, message);
            }
        }

        [Event(2, Keywords = Keywords.Log, Level = EventLevel.Warning)]
        public void Warn(string message)
        {
            if (IsEnabled())
            {
                WriteEvent(2, message);
            }
        }

#if NET
        [Event(3, Keywords = Keywords.Log, Level = EventLevel.Error)]
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The properties are preserved with the DynamicallyAccessedMembers attribute.")]
        public void Error(string message, RabbitMqExceptionDetail ex)
        {
            if (IsEnabled())
            {
                WriteEvent(3, message, ex);
            }
        }
#else
        [Event(3, Keywords = Keywords.Log, Level = EventLevel.Error)]
        public void Error(string message, RabbitMqExceptionDetail ex)
        {
            if (IsEnabled())
            {
                WriteEvent(3, message, ex);
            }
        }
#endif

        [NonEvent]
        public void Error(string message, Exception ex)
        {
            Error(message, new RabbitMqExceptionDetail(ex));
        }
    }
}
