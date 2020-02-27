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
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;

using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    class SimpleBlockingRpcContinuation : IRpcContinuation
    {
        public readonly BlockingCell<Either<Command, ShutdownEventArgs>> m_cell = new BlockingCell<Either<Command, ShutdownEventArgs>>();

        public virtual Command GetReply()
        {
            Either<Command, ShutdownEventArgs> result = m_cell.WaitForValue();
            switch (result.Alternative)
            {
                case EitherAlternative.Left:
                    return result.LeftValue;
                case EitherAlternative.Right:
                    throw new OperationInterruptedException(result.RightValue);
                default:
                    return null;
            }
        }

        public virtual Command GetReply(TimeSpan timeout)
        {
            Either<Command, ShutdownEventArgs> result = m_cell.WaitForValue(timeout);
            switch (result.Alternative)
            {
                case EitherAlternative.Left:
                    return result.LeftValue;
                case EitherAlternative.Right:
                    throw new OperationInterruptedException(result.RightValue);
                default:
                    return null;
            }
        }

        public virtual void HandleCommand(Command cmd)
        {
            m_cell.ContinueWithValue(Either<Command,ShutdownEventArgs>.Left(cmd));
        }

        public virtual void HandleModelShutdown(ShutdownEventArgs reason)
        {
            m_cell.ContinueWithValue(Either<Command,ShutdownEventArgs>.Right(reason));
        }
    }
}
