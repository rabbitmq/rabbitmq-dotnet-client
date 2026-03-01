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
using System.Threading.Channels;
using System.Threading.Tasks;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.Impl
{
    internal sealed class BackgroundSocketFrameHandler : SocketFrameHandler
    {
        private readonly ChannelWriter<OutgoingFrameMemory> _channelWriter;
        private readonly ChannelReader<OutgoingFrameMemory> _channelReader;
        private readonly Task _writerTask;

        public BackgroundSocketFrameHandler(AmqpTcpEndpoint amqpTcpEndpoint, ITcpClient socket, Stream stream, CancellationToken cancellationToken) : base(amqpTcpEndpoint, socket, stream)
        {
            var channel = System.Threading.Channels.Channel.CreateBounded<OutgoingFrameMemory>(
                new BoundedChannelOptions(128)
                {
                    AllowSynchronousContinuations = false,
                    SingleReader = true,
                    SingleWriter = false
                });

            _channelWriter = channel.Writer;
            _channelReader = channel.Reader;

            _writerTask = Task.Run(WriteLoopAsync, cancellationToken);
        }

        protected override async ValueTask InternalClose(CancellationToken cancellationToken)
        {
            _channelWriter.Complete();
            if (_writerTask != null)
            {
                await _writerTask.ConfigureAwait(false);
            }
        }

        protected override ValueTask InternalWriteAsync(OutgoingFrameMemory frames, CancellationToken cancellationToken)
        {
            // Cross-thread boundary: The caller's 'ReadOnlyMemory<byte> body' might be 
            // overwritten or disposed before the background writer loop processes it.
            // We must materialize the body into a rented array to guarantee its lifetime.
            // 
            // OWNERSHIP TRANSFER: This call consumes the original 'frames' struct.
            // The caller must NOT dispose the original struct, as the rented header 
            // arrays are now owned by 'BackgroundSocketFrameHandler'.
            OutgoingFrameMemory frameWithCopiedBody = frames.TransferOwnershipAndCopyBody();

            return _channelWriter.WriteAsync(frameWithCopiedBody, cancellationToken);
        }

        private async Task WriteLoopAsync()
        {
            try
            {
                while (await _channelReader.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (_channelReader.TryRead(out OutgoingFrameMemory frames))
                    {
                        try
                        {
                            frames.WriteTo(_pipeWriter);
                            await _pipeWriter.FlushAsync()
                                .ConfigureAwait(false);
                            RabbitMqClientEventSource.Log.CommandSent(frames.Size);
                        }
                        finally
                        {
                            frames.Dispose();
                        }
                    }

                    await _pipeWriter.FlushAsync()
                        .ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                ESLog.Error("Background socket write loop has crashed", ex);
                throw;
            }
        }
    }
}
