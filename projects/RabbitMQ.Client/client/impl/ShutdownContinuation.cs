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

using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    internal class ShutdownContinuation
    {
        public readonly BlockingCell<ShutdownEventArgs> m_cell = new BlockingCell<ShutdownEventArgs>();

        // You will note there are two practically identical overloads
        // of OnShutdown() here. This is because Microsoft's C#
        // compilers do not consistently support the Liskov
        // substitutability principle. When I use
        // OnShutdown(object,ShutdownEventArgs), the compilers
        // complain that OnShutdown can't be placed into a
        // ConnectionShutdownEventHandler because object doesn't
        // "match" IConnection, even though there's no context in
        // which the program could Go Wrong were it to accept the
        // code. The same problem appears for
        // ChannelShutdownEventHandler. The .NET 1.1 compiler complains
        // about these two cases, and the .NET 2.0 compiler does not -
        // presumably they improved the type checker with the new
        // release of the compiler.

        public virtual void OnConnectionShutdown(object sender, ShutdownEventArgs reason)
        {
            m_cell.ContinueWithValue(reason);
        }

        public virtual ShutdownEventArgs Wait()
        {
            return m_cell.WaitForValue();
        }

        public ShutdownEventArgs Wait(TimeSpan timeout)
        {
            return m_cell.WaitForValue(timeout);
        }
    }
}
