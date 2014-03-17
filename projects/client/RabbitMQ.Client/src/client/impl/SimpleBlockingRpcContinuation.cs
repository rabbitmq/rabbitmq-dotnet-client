// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2013 GoPivotal, Inc.
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

using System.Diagnostics;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    public class SimpleBlockingRpcContinuation : IRpcContinuation
    {
        public readonly BlockingCell m_cell = new BlockingCell();
        public SimpleBlockingRpcContinuation() { }

        public virtual void HandleCommand(Command cmd)
        {
            m_cell.Value = Either.Left(cmd);
        }

        public virtual void HandleModelShutdown(ShutdownEventArgs reason)
        {
            m_cell.Value = Either.Right(reason);
        }

        public virtual Command GetReply()
        {
            Either result = (Either)m_cell.Value;
            switch (result.Alternative)
            {
                case EitherAlternative.Left:
                    return (Command)result.Value;
                case EitherAlternative.Right:
                    throw new OperationInterruptedException((ShutdownEventArgs)result.Value);
                default:
                    Trace.Fail("Illegal EitherAlternative " + result.Alternative);
                    return null;
            }
        }
    }
}
