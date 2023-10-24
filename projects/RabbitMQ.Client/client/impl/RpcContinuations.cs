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
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    internal class SimpleBlockingRpcContinuation : IRpcContinuation
    {
        private readonly BlockingCell<Either<IncomingCommand, ShutdownEventArgs>> m_cell = new BlockingCell<Either<IncomingCommand, ShutdownEventArgs>>();

        public void GetReply(TimeSpan timeout)
        {
            Either<IncomingCommand, ShutdownEventArgs> result = m_cell.WaitForValue(timeout);
            if (result.Alternative == EitherAlternative.Left)
            {
                return;
            }
            ThrowOperationInterruptedException(result.RightValue);
        }

        public void GetReply(TimeSpan timeout, out IncomingCommand reply)
        {
            Either<IncomingCommand, ShutdownEventArgs> result = m_cell.WaitForValue(timeout);
            if (result.Alternative == EitherAlternative.Left)
            {
                reply = result.LeftValue;
                return;
            }

            reply = IncomingCommand.Empty;
            ThrowOperationInterruptedException(result.RightValue);
        }

        private static void ThrowOperationInterruptedException(ShutdownEventArgs shutdownEventArgs)
            => throw new OperationInterruptedException(shutdownEventArgs);

        public void HandleCommand(in IncomingCommand cmd)
        {
            m_cell.ContinueWithValue(Either<IncomingCommand, ShutdownEventArgs>.Left(cmd));
        }

        public void HandleChannelShutdown(ShutdownEventArgs reason)
        {
            m_cell.ContinueWithValue(Either<IncomingCommand, ShutdownEventArgs>.Right(reason));
        }
    }

    internal class BasicConsumerRpcContinuation : SimpleBlockingRpcContinuation
    {
        public IBasicConsumer m_consumer;
        public string m_consumerTag;
    }

    internal class BasicGetRpcContinuation : SimpleBlockingRpcContinuation
    {
        public BasicGetResult m_result;
    }

    internal class QueueDeclareRpcContinuation : SimpleBlockingRpcContinuation
    {
        public QueueDeclareOk m_result;
    }
}
