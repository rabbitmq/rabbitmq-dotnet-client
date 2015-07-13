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
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Impl
{
    /// <summary>
    /// Thrown when the protocol handlers detect an unknown class
    /// number or method number.
    /// </summary>
    public class UnknownClassOrMethodException : HardProtocolException
    {
        public UnknownClassOrMethodException(ushort classId, ushort methodId)
            : base(string.Format("The Class or Method <{0}.{1}> is unknown", classId, methodId))
        {
            ClassId = classId;
            MethodId = methodId;
        }

        ///<summary>The AMQP content-class ID.</summary>
        public ushort ClassId { get; private set; }

        ///<summary>The AMQP method ID within the content-class, or 0 if none.</summary>
        public ushort MethodId { get; private set; }

        public override ushort ReplyCode
        {
            get { return Constants.NotImplemented; }
        }

        public override string ToString()
        {
            if (MethodId == 0)
            {
                return base.ToString() + "<" + ClassId + ">";
            }
            else
            {
                return base.ToString() + "<" + ClassId + "." + MethodId + ">";
            }
        }
    }
}
