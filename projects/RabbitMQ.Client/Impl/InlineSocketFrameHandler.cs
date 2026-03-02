// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.Impl
{
    internal sealed class InlineSocketFrameHandler : SocketFrameHandler
    {
        // Enforces that only one thread can write to the PipeWriter at a time.
        private readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);

        public InlineSocketFrameHandler(AmqpTcpEndpoint amqpTcpEndpoint, ITcpClient socket, Stream stream) : base(amqpTcpEndpoint, socket, stream)
        {
        }

        protected override ValueTask InternalClose(CancellationToken cancellationToken)
        {
            _writeSemaphore.Dispose();
            return default;
        }

        protected override async ValueTask InternalWriteAsync(OutgoingFrameMemory frames, CancellationToken cancellationToken)
        {
            try
            {
                await _writeSemaphore.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    frames.WriteTo(_pipeWriter);
                    await _pipeWriter.FlushAsync(cancellationToken)
                        .ConfigureAwait(false);
                    RabbitMqClientEventSource.Log.CommandSent(frames.Size);
                }
                finally
                {
                    _writeSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                ESLog.Error("Inline socket write has crashed", ex);
                throw;
            }
            finally
            {
                frames.Dispose();
            }
        }
    }
}
