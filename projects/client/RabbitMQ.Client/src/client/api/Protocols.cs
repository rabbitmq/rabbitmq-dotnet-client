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
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Reflection;
using System.Configuration;

namespace RabbitMQ.Client
{
    ///<summary>Provides access to the supported IProtocol implementations</summary>
    public class Protocols
    {
        // Hide the constructor - no instances of Protocols needed.
        // We'd make this class static, but for MS's .NET 1.1 compilers
        private Protocols() {}

        ///<summary>Protocol version 0-9-1 as modified by VMWare.</summary>
        public static IProtocol AMQP_0_9_1
        {
            get { return new RabbitMQ.Client.Framing.v0_9_1.Protocol(); }
        }

        ///<summary>Retrieve the current default protocol variant
        ///(currently AMQP_0_9_1)</summary>
        public static IProtocol DefaultProtocol
        {
            get { return AMQP_0_9_1; }
        }
    }
}
