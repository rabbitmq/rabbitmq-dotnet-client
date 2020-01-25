// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2016 Pivotal Software, Inc.
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

namespace RabbitMQ.Client.Logging
{
    using System;
    using System.Diagnostics.Tracing;

    [EventSource(Name="rabbitmq-dotnet-client")]
    public sealed class RabbitMqClientEventSource : EventSource
    {
        public class Keywords
        {
            public const EventKeywords Log = (EventKeywords)1;
        }
#if NET452
        public RabbitMqClientEventSource() : base()
        {

        }
#else
        public RabbitMqClientEventSource() : base(EventSourceSettings.EtwSelfDescribingEventFormat)
        {
        }
#endif

        public static RabbitMqClientEventSource Log = new RabbitMqClientEventSource ();

        [Event(1, Message = "INFO", Keywords = Keywords.Log, Level = EventLevel.Informational)]
        public void Info(string message)
        {
            if(IsEnabled())
                this.WriteEvent(1, message);
        }

        [Event(2, Message = "WARN", Keywords = Keywords.Log, Level = EventLevel.Warning)]
        public void Warn(string message)
        {
            if(IsEnabled())
                this.WriteEvent(2, message);
        }
#if NET452
        [Event(3, Message = "ERROR", Keywords = Keywords.Log, Level = EventLevel.Error)]
        public void Error(string message, string detail)
        {
            if(IsEnabled())
                this.WriteEvent(3, message, detail);
        }
#else
        [Event(3, Message = "ERROR", Keywords = Keywords.Log, Level = EventLevel.Error)]
        public void Error(string message,  RabbitMqExceptionDetail ex)
        {
            if(IsEnabled())
                this.WriteEvent(3, message, ex);
        }
#endif

        [NonEvent]
        public void Error(string message, Exception ex)
        {

#if NET452
            Error(message, ex.ToString());
#else
            Error(message, new RabbitMqExceptionDetail(ex));
#endif
        }
    }
}