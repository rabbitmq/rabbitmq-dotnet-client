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
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;

namespace RabbitMQ.Client.Logging
{
    [EventSource(Name = "rabbitmq-dotnet-client")]
    public sealed class RabbitMqClientEventSource : EventSource
    {
        public static readonly RabbitMqClientEventSource Log = new RabbitMqClientEventSource();
        public class Keywords
        {
            public const EventKeywords Log = (EventKeywords)1;
        }

        public RabbitMqClientEventSource() : base(EventSourceSettings.EtwSelfDescribingEventFormat)
        {
        }

        [Event(1, Message = "INFO", Keywords = Keywords.Log, Level = EventLevel.Informational)]
        public void Info(string message)
        {
            if (IsEnabled())
                WriteEvent(1, message);
        }

        [Event(2, Message = "WARN", Keywords = Keywords.Log, Level = EventLevel.Warning)]
        public void Warn(string message)
        {
            if (IsEnabled())
                WriteEvent(2, message);
        }

        [Event(3, Message = "ERROR", Keywords = Keywords.Log, Level = EventLevel.Error)]
        public void Error(string message, RabbitMqExceptionDetail ex)
        {
            if (IsEnabled())
#if NET6_0_OR_GREATER
                WriteExceptionEvent(message, ex);
#else
                WriteEvent(3, message, ex);
#endif
        }

        [NonEvent]
        public void Error(string message, Exception ex)
        {
            Error(message, new RabbitMqExceptionDetail(ex));
        }

#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The properties are preserved with the DynamicallyAccessedMembers attribute.")]
        private void WriteExceptionEvent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(string message, T ex)
        {
            WriteEvent(3, message, ex);
        }
#endif
    }
}
