// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using Xunit.Abstractions;

namespace Test
{
    internal class TestOutputWriterEventListener : EventListener
    {
        private readonly ITestOutputHelper _output;
        private readonly bool _initialized = false;
        private ConcurrentQueue<EventSource> _eventSources;

        public TestOutputWriterEventListener(ITestOutputHelper output)
        {
            _output = output;
            _initialized = true;
            foreach (EventSource eventSource in _eventSources)
            {
                MaybeEnableEvents(eventSource);
            }
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            lock (this)
            {
                if (_eventSources == null)
                {
                    _eventSources = new ConcurrentQueue<EventSource>();
                }

                _eventSources.Enqueue(eventSource);
            }

            // If initialization is already done, we can enable EventSource right away.
            // This will take care of all EventSources created after initialization is done.
            if (_initialized)
            {
                MaybeEnableEvents(eventSource);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            try
            {
#if NET
                if (eventData.Payload.Count > 0)
                {
                    string payloadName = eventData.PayloadNames[0];
                    object payload = eventData.Payload[0];
                    _output.WriteLine("{0}|{1}|{2}|{3}",
                        eventData.TimeStamp, eventData.Level, payloadName, payload);
                }
                else
                {
                    _output.WriteLine("{0}|{1}", eventData.TimeStamp, eventData.Level);
                }
#else
                if (eventData.Payload.Count > 0)
                {
                    string payloadName = eventData.PayloadNames[0];
                    object payload = eventData.Payload[0];
                    _output.WriteLine("{0}|{1}|{2}",
                        eventData.Level, payloadName, payload);
                }
                else
                {
                    _output.WriteLine("{1}", eventData.Level);
                }
#endif
            }
            catch (InvalidOperationException)
            {
                /*
                 * Note:
                 * This exception can be thrown if there is no running test.
                 */
            }
        }

        private void MaybeEnableEvents(EventSource eventSource)
        {
            if (eventSource.Name == "rabbitmq-client")
            {
                EnableEvents(eventSource, EventLevel.LogAlways);
            }
        }
    }
}
