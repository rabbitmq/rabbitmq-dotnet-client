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
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    public class SimpleBlockingRpcContinuation : IRpcContinuation
    {
        public readonly TaskCompletionSource<Either> tcs = new TaskCompletionSource<Either>();

        public virtual async Task<Command> GetReply()
        {
            var result = await tcs.Task.ConfigureAwait(false);
            switch (result.Alternative)
            {
                case EitherAlternative.Left:
                    return (Command)result.Value;
                case EitherAlternative.Right:
                    throw new OperationInterruptedException((ShutdownEventArgs)result.Value);
                default:
                    string error = "Illegal EitherAlternative " + result.Alternative;
#if !(NETFX_CORE)
                    Trace.Fail(error);
#else
                    MetroEventSource.Log.Error(error);
#endif
                    return null;
            }
        }

        public virtual async Task<Command> GetReply(TimeSpan timeout)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Token.Register(() => tcs.SetCanceled());
            cancellationTokenSource.CancelAfter(timeout);
            var result = await tcs.Task.ConfigureAwait(false);
            switch (result.Alternative)
            {
                case EitherAlternative.Left:
                    return (Command)result.Value;
                case EitherAlternative.Right:
                    throw new OperationInterruptedException((ShutdownEventArgs)result.Value);
                default:
                    ReportInvalidInvariant(result);
                    return null;
            }
        }

        private static void ReportInvalidInvariant(Either result)
        {
            string error = "Illegal EitherAlternative " + result.Alternative;
#if !(NETFX_CORE)
            Trace.Fail(error);
#else
            MetroEventSource.Log.Error(error);
#endif
        }

        public virtual void HandleCommand(Command cmd)
        {
            tcs.SetResult(Either.Left(cmd));
        }

        public virtual void HandleModelShutdown(ShutdownEventArgs reason)
        {
            tcs.SetResult(Either.Right(reason));
        }
    }
}
