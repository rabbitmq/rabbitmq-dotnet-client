﻿// Autogenerated code. Do not edit.

// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//   The contents of this file are subject to the Mozilla Public License
//   Version 1.1 (the "License"); you may not use this file except in
//   compliance with the License. You may obtain a copy of the License at
//   https://www.rabbitmq.com/mpl.html
//
//   Software distributed under the License is distributed on an "AS IS"
//   basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
//   License for the specific language governing rights and limitations
//   under the License.
//
//   The Original Code is RabbitMQ.
//
//   The Initial Developer of the Original Code is Pivotal Software, Inc.
//   Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System.Text;
namespace RabbitMQ.Client.Framing.Impl
{
    /// <summary>Autogenerated type. Private implementation class - do not use directly.</summary>
    internal sealed class TxRollback : Client.Impl.MethodBase, ITxRollback
    {



        public TxRollback()
        {
        }

        public override uint ProtocolCommandId => MethodConstants.TxRollback;
        public override string ProtocolMethodName => "tx.rollback";
        public override bool HasContent => false;

        public override void ReadArgumentsFrom(ref Client.Impl.MethodArgumentReader reader)
        {
        }

        public override void WriteArgumentsTo(ref Client.Impl.MethodArgumentWriter writer)
        {
        }

        public override int GetRequiredBufferSize()
        {
            int bufferSize = 0;
            return bufferSize;
        }

        public override void AppendArgumentDebugStringTo(StringBuilder sb)
        {
            sb.Append("(");
            sb.Append(")");
        }
    }
}
