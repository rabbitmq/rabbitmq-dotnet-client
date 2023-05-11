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
using System.Net;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    internal interface IFrameHandler
    {
        AmqpTcpEndpoint Endpoint { get; }

        EndPoint LocalEndPoint { get; }

        int LocalPort { get; }

        EndPoint RemoteEndPoint { get; }

        int RemotePort { get; }

        ///<summary>Socket read timeout. System.Threading.Timeout.InfiniteTimeSpan signals "infinity".</summary>
        TimeSpan ReadTimeout { set; }

        ///<summary>Socket write timeout. System.Threading.Timeout.InfiniteTimeSpan signals "infinity".</summary>
        TimeSpan WriteTimeout { set; }

        void Close();
        ValueTask CloseAsync();

        ///<summary>Read a frame from the underlying
        ///transport. Returns null if the read operation timed out
        ///(see Timeout property).</summary>
        ValueTask<InboundFrame> ReadFrameAsync();

        ///<summary>Try to synchronously read a frame from the underlying transport.
        ///Returns false if connection buffer contains insufficient data.
        ///</summary>
        bool TryReadFrame(out InboundFrame frame);

        ValueTask SendProtocolHeaderAsync();

        ValueTask WriteAsync(RentedMemory frames);
    }
}
