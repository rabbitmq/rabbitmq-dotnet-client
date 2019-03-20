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
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
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
    public class BlockingCell<T>
    {
        private readonly ManualResetEventSlim manualResetEventSlim = new ManualResetEventSlim(false);
        private T m_value = default(T);
        public EventHandler<T> ContinueUsingValue;

        public void ContinueWithValue(T value)
        {
            m_value = value;
            manualResetEventSlim.Set();
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
        /// <exception cref="TimeoutException" />
        public T WaitForValue(TimeSpan timeout)
        {
            if (manualResetEventSlim.Wait(timeout))
            {
                if (ContinueUsingValue != null) ContinueUsingValue(this, m_value);
                return m_value;
            }
            throw new TimeoutException();
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
        /// <exception cref="TimeoutException" />
        public T WaitForValue(int timeout)
        {
            return WaitForValue(TimeSpan.FromMilliseconds(timeout));
        }
        ///<summary>Retrieve the cell's value, blocking if none exists
        ///at present, or supply a value to an empty cell, thereby
        ///filling it.</summary>
        /// <exception cref="TimeoutException" />
        public T WaitForValue()
        {
            return WaitForValue(TimeSpan.FromMinutes(60));
        }

        ///<summary>Return valid timeout value</summary>
        ///<remarks>If value of the parameter is less then zero, return 0
        ///to mean infinity</remarks>
        public static int validatedTimeout(int timeout)
        {
            return (timeout != Timeout.Infinite) && (timeout < 0) ? 0 : timeout;
        }
    }
}
