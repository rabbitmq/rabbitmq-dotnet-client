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
using System.Net;

namespace RabbitMQ.Client.Exceptions
{
    /// <summary> Thrown when the wire-formatting code cannot encode a
    /// particular .NET value to AMQP protocol format.  </summary>
    public class WireFormattingException : ProtocolViolationException
    {
        private object m_offender;

        ///<summary>Object which this exception is complaining about;
        ///may be null if no particular offender exists</summary>
        public object Offender { get { return m_offender; } }

        ///<summary>Construct a WireFormattingException with no
        ///particular offender (i.e. null)</summary>
        public WireFormattingException(string message) : this(message, null) { }

        ///<summary>Construct a WireFormattingException with the given
        ///offender</summary>
        public WireFormattingException(string message, object offender)
            : base(message)
        {
            m_offender = offender;
        }
    }
}
