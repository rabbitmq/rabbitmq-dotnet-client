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
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Timers;

namespace RabbitMQ.Client
{
    public delegate void NotifyCredentialRefreshed(bool succesfully);

    public interface ICredentialsRefresher
    {
        ICredentialsProvider Register(ICredentialsProvider provider, NotifyCredentialRefreshed callback);
        bool Unregister(ICredentialsProvider provider);
    }

    [EventSource(Name = "TimerBasedCredentialRefresher")]
    public class TimerBasedCredentialRefresherEventSource : EventSource
    {
        public static TimerBasedCredentialRefresherEventSource Log { get; } = new TimerBasedCredentialRefresherEventSource();

        [Event(1)]
        public void Registered(string name) => WriteEvent(1, "Registered", name);
        [Event(2)]
        public void Unregistered(string name) => WriteEvent(2, "UnRegistered", name);
        [Event(3)]
        public void ScheduledTimer(string name, double interval) => WriteEvent(3, "ScheduledTimer", name, interval);
        [Event(4)]
        public void TriggeredTimer(string name) => WriteEvent(4, "TriggeredTimer", name);
        [Event(5)]
        public void RefreshedCredentials(string name, bool succesfully) => WriteEvent(5, "RefreshedCredentials", name, succesfully);
    }

    public class TimerBasedCredentialRefresher : ICredentialsRefresher
    {
        private Dictionary<ICredentialsProvider, Timer> _registrations = new Dictionary<ICredentialsProvider, Timer>();

        public ICredentialsProvider Register(ICredentialsProvider provider, NotifyCredentialRefreshed callback)
        {
            if (!provider.ValidUntil.HasValue || provider.ValidUntil.Value.Equals(TimeSpan.Zero))
            {
                return provider;
            }

            _registrations.Add(provider, scheduleTimer(provider, callback));
            TimerBasedCredentialRefresherEventSource.Log.Registered(provider.Name);
            return provider;
        }

        public bool Unregister(ICredentialsProvider provider)
        {
            if (!_registrations.ContainsKey(provider))
            {
                return false;
            }

            var timer = _registrations[provider];
            if (timer != null)
            {
                TimerBasedCredentialRefresherEventSource.Log.Unregistered(provider.Name);
                timer.Stop();
                _registrations.Remove(provider);
                timer.Dispose();
                return true;
            }
            else
            {
                return false;
            }
        }

        private Timer scheduleTimer(ICredentialsProvider provider, NotifyCredentialRefreshed callback)
        {
            Timer timer = new Timer();
            timer.Interval = provider.ValidUntil.Value.TotalMilliseconds * (1.0 - (1 / 3.0));
            timer.Elapsed += (o, e) =>
            {
                TimerBasedCredentialRefresherEventSource.Log.TriggeredTimer(provider.Name);
                try
                {
                    provider.Refresh();
                    scheduleTimer(provider, callback);
                    callback.Invoke(provider.Password != null);
                    TimerBasedCredentialRefresherEventSource.Log.RefreshedCredentials(provider.Name, true);
                }
                catch (Exception)
                {
                    callback.Invoke(false);
                    TimerBasedCredentialRefresherEventSource.Log.RefreshedCredentials(provider.Name, false);
                }

            };
            timer.Enabled = true;
            timer.AutoReset = false;
            TimerBasedCredentialRefresherEventSource.Log.ScheduledTimer(provider.Name, timer.Interval);
            return timer;
        }
    }

    class NoOpCredentialsRefresher : ICredentialsRefresher
    {
        public ICredentialsProvider Register(ICredentialsProvider provider, NotifyCredentialRefreshed callback)
        {
            return provider;
        }

        public bool Unregister(ICredentialsProvider provider)
        {
            return false;
        }
    }
}
