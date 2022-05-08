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
using System.Threading;

namespace RabbitMQ.Util;

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
internal class BlockingCell<T>
{
    private readonly ManualResetEventSlim _manualResetEventSlim = new ManualResetEventSlim(false);
    private T _value = default;

    public void ContinueWithValue(T value)
    {
        _value = value;
        _manualResetEventSlim.Set();
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
        if (_manualResetEventSlim.Wait(timeout))
        {
            return _value;
        }
        throw new TimeoutException();
    }

    ///<summary>Retrieve the cell's value, blocking if none exists
    ///at present, or supply a value to an empty cell, thereby
    ///filling it.</summary>
    /// <exception cref="TimeoutException" />
    public T WaitForValue()
    {
        return WaitForValue(TimeSpan.FromMinutes(60));
    }
}
