// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2015 Pivotal Software, Inc.
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
//  Copyright (c) 2007-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Threading;

namespace RabbitMQ.Util
{
    ///<summary>A thread-safe single-assignment reference cell.</summary>
    ///<remarks>
    ///A fresh BlockingCell holds no value (is empty). Any thread
    ///reading the Value property when the cell is empty will block
    ///until a value is made available by some other thread. The Value
    ///property can only be set once - on the first call, the
    ///BlockingCell is considered full, and made immutable. Further
    ///attempts to set Value result in a thrown
    ///InvalidOperationException.
    ///</remarks>
    public class BlockingCell
    {
        private readonly object _lock = new object();
        private object m_value = null;
        private bool m_valueSet = false;

        ///<summary>Retrieve the cell's value, blocking if none exists
        ///at present, or supply a value to an empty cell, thereby
        ///filling it.</summary>
        /// <exception cref="InvalidOperationException" />
        public object Value
        {
            get
            {
                lock (_lock)
                {
                    while (!m_valueSet)
                    {
                        Monitor.Wait(_lock);
                    }
                    return m_value;
                }
            }

            set
            {
                lock (_lock)
                {
                    if (m_valueSet)
                    {
                        throw new InvalidOperationException("Setting BlockingCell value twice forbidden");
                    }
                    m_value = value;
                    m_valueSet = true;
                    Monitor.PulseAll(_lock);
                }
            }
        }

        ///<summary>Return valid timeout value</summary>
        ///<remarks>If value of the parameter is less then zero, return 0
        ///to mean infinity</remarks>
        public static int validatedTimeout(int timeout)
        {
            return (timeout != Timeout.Infinite)
                   && (timeout < 0) ? 0 : timeout;
        }

        ///<summary>Retrieve the cell's value, waiting for the given
        ///timeout if no value is immediately available.</summary>
        ///<remarks>
        ///<para>
        /// If a value is present in the cell at the time the call is
        /// made, the call will return immediately. Otherwise, the
        /// calling thread blocks until either a value appears, or
        /// operation times out.
        ///</para>
        ///<para>
        /// If no value was available before the timeout, an exception
        /// is thrown.
        ///</para>
        ///</remarks>
        public object GetValue(TimeSpan timeout)
        {
            lock (_lock)
            {
                if (!m_valueSet)
                {
                    Monitor.Wait(_lock, timeout);
                    if (!m_valueSet)
                    {
                        throw new TimeoutException();
                    }
                }
                return m_value;
            }
        }

        ///<summary>Retrieve the cell's value, waiting for the given
        ///timeout if no value is immediately available.</summary>
        ///<remarks>
        ///<para>
        /// If a value is present in the cell at the time the call is
        /// made, the call will return immediately. Otherwise, the
        /// calling thread blocks until either a value appears, or
        /// operation times out.
        ///</para>
        ///<para>
        /// If no value was available before the timeout, an exception
        /// is thrown.
        ///</para>
        ///</remarks>
        public object GetValue(int timeout)
        {
            lock (_lock)
            {
                if (!m_valueSet)
                {
                    Monitor.Wait(_lock, validatedTimeout(timeout));
                    if (!m_valueSet)
                    {
                        throw new TimeoutException();
                    }
                }
                return m_value;
            }
        }
    }
}
