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
//  at http://www.mozilla.org/MPL/
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
    using System.Collections.Generic;
#if NET451
    using Microsoft.Diagnostics.Tracing;
#else
    using System.Diagnostics.Tracing;
#endif

    public sealed class RabbitMqConsoleEventListener : EventListener, IDisposable
    {
        public RabbitMqConsoleEventListener()
        {
            this.EnableEvents(RabbitMqClientEventSource.Log, EventLevel.Informational, RabbitMqClientEventSource.Keywords.Log);
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            foreach(var pl in eventData.Payload)
            {
                var dict = pl as IDictionary<string, object>;
                if(dict != null)
                {
                    var rex = new RabbitMqExceptionDetail(dict);
                    Console.WriteLine("{0}: {1}", eventData.Level, rex.ToString());
                }
                else
                {
                    Console.WriteLine("{0}: {1}", eventData.Level, pl.ToString());
                }
            }
        }

        public override void Dispose()
        {
            this.DisableEvents(RabbitMqClientEventSource.Log);
        }
    }
}