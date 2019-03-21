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
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;

namespace RabbitMQ.Client.Impl
{
    public interface IFrameHandler
    {
        AmqpTcpEndpoint Endpoint { get; }

#if !NETFX_CORE
        EndPoint LocalEndPoint { get; }
#endif

        int LocalPort { get; }

#if !NETFX_CORE
        EndPoint RemoteEndPoint { get; }
#endif

        int RemotePort { get; }

        ///<summary>Socket read timeout, in milliseconds. Zero signals "infinity".</summary>
        int ReadTimeout { set; }

        ///<summary>Socket write timeout, in milliseconds. Zero signals "infinity".</summary>
        int WriteTimeout { set; }

        void Close();

        ///<summary>Read a frame from the underlying
        ///transport. Returns null if the read operation timed out
        ///(see Timeout property).</summary>
        Frame ReadFrame();

        void SendHeader();

        void WriteFrame(Frame frame);

        void WriteFrameSet(IList<Frame> frames);

        void Flush();
    }
}
