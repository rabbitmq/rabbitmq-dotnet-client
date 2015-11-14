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
//  Copyright (c) 2013-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using Windows.System.Threading;

namespace System.Threading
{
#if NETFX_CORE
    /// <summary>
    /// Provides a mechanism for executing a method at specified intervals.  Adapts to <see cref="ThreadPoolTimer"/> on Windows 8+ platforms.
    /// </summary>
    internal class Timer : IDisposable
    {
        private readonly Action<object> callback;
        private readonly object state;

        private ThreadPoolTimer threadPoolTimer;

        public Timer(Action<object> callback)
        {
            this.callback = callback;
            this.state = null;
        }

        public void Dispose()
        {
            if (threadPoolTimer != null)
            {
                threadPoolTimer.Cancel();
                threadPoolTimer = null;
            }
        }

        /// <summary>
        /// Changes the start time and the interval between method invocations for a timer, using 32-bit signed integers to measure time intervals.
        /// </summary>
        /// <param name="dueTime">
        /// The amount of time to delay before the invoking the callback method specified when the Timer was constructed, in milliseconds. Specify Timeout.Infinite to prevent the timer from restarting. Specify zero (0) to restart the timer immediately. 
        /// </param>
        /// <param name="period">
        /// The time interval between invocations of the callback method specified when the Timer was constructed, in milliseconds. Specify Timeout.Infinite to disable periodic signaling.
        /// </param>
        /// <returns>true if the timer was successfully updated; otherwise, false.</returns>
        public bool Change(int dueTime, int period)
        {
            if (dueTime < -1)
            {
                throw new ArgumentOutOfRangeException("dueTime", "NeedNonNegOrNegative1");
            }

            if (period < -1)
            {
                throw new ArgumentOutOfRangeException("period", "NeedNonNegOrNegative1");
            }

            if (threadPoolTimer != null)
            {
                threadPoolTimer.Cancel();
                threadPoolTimer = null;
            }

            if (dueTime == Timeout.Infinite)
            {
                // If dueTime is Infinite, the callback method is never invoked; the timer is disabled
                return true;
            }
            else if (dueTime == 0)
            {
                // If dueTime is zero (0), the callback method is invoked immediately
                StartTimers(period);
            }
            else
            {
                ThreadPoolTimer.CreateTimer(timer => this.StartTimers(period), TimeSpan.FromMilliseconds(dueTime), timer => timer.Cancel());
            }

            return true;
        }

        private void StartTimers(int period)
        {
            if (period == Timeout.Infinite || period == 0)
            {
                // If period is zero (0) or Infinite, and dueTime is not Infinite, the callback method is invoked once; 
                threadPoolTimer = ThreadPoolTimer.CreateTimer(timer => callback(state), TimeSpan.FromMilliseconds(0), timer => timer.Cancel());
            }
            else
            {
                threadPoolTimer = ThreadPoolTimer.CreatePeriodicTimer(timer => callback(state), TimeSpan.FromMilliseconds(period));
            }
        }
    }
#endif
}