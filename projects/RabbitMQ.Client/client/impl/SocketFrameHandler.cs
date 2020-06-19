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
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Pipelines.Sockets.Unofficial;

using RabbitMQ.Client.Exceptions;

namespace RabbitMQ.Client.Impl
{
    internal static class TaskExtensions
    {
        public static async Task TimeoutAfter(this Task task, TimeSpan timeout)
        {
            if (task == await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false))
            {
                await task;
            }
            else
            {
                Task supressErrorTask = task.ContinueWith(t => t.Exception.Handle(e => true), TaskContinuationOptions.OnlyOnFaulted);
                throw new TimeoutException();
            }
        }
    }

    internal class SocketFrameHandler : IFrameHandler
    {
        // Socket poll timeout in ms. If the socket does not
        // become writeable in this amount of time, we throw
        // an exception.
        private TimeSpan _writeableStateTimeout = TimeSpan.FromSeconds(30);
        private readonly ITcpClient _socket;
        private readonly IDuplexPipe _pipe;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public SocketFrameHandler(AmqpTcpEndpoint endpoint,
            Func<AddressFamily, ITcpClient> socketFactory,
            TimeSpan connectionTimeout, TimeSpan readTimeout, TimeSpan writeTimeout)
        {
            Endpoint = endpoint;

            if (ShouldTryIPv6(endpoint))
            {
                try
                {
                    _socket = ConnectUsingIPv6(endpoint, socketFactory, connectionTimeout);
                }
                catch (ConnectFailureException)
                {
                    _socket = null;
                }
            }

            if (_socket == null && endpoint.AddressFamily != AddressFamily.InterNetworkV6)
            {
                _socket = ConnectUsingIPv4(endpoint, socketFactory, connectionTimeout);
            }



            if (endpoint.Ssl.Enabled)
            {
                try
                {
                    Stream netstream = _socket.GetStream();
                    netstream.ReadTimeout = (int)readTimeout.TotalMilliseconds;
                    netstream.WriteTimeout = (int)writeTimeout.TotalMilliseconds;
                    netstream = SslHelper.TcpUpgrade(netstream, endpoint.Ssl);
                    _pipe = StreamConnection.GetDuplex(netstream);
                }
                catch (Exception)
                {
                    Close();
                    throw;
                }
            }
            else
            {
                _pipe = SocketConnection.Create(_socket.Client);
            }

            WriteTimeout = writeTimeout;
        }
        public AmqpTcpEndpoint Endpoint { get; set; }

        public EndPoint LocalEndPoint => _socket.Client.LocalEndPoint;

        public int LocalPort => ((IPEndPoint)LocalEndPoint).Port;

        public EndPoint RemoteEndPoint => _socket.Client.RemoteEndPoint;

        public int RemotePort => ((IPEndPoint)LocalEndPoint).Port;

        public TimeSpan ReadTimeout
        {
            set
            {
                try
                {
                    if (_socket.Connected)
                    {
                        _socket.ReceiveTimeout = value;
                    }
                }
                catch (SocketException)
                {
                    // means that the socket is already closed
                }
            }
        }

        public TimeSpan WriteTimeout
        {
            set
            {
                _writeableStateTimeout = value;
                _socket.Client.SendTimeout = (int)_writeableStateTimeout.TotalMilliseconds;
            }
        }

        public void Close()
        {
            _pipe.Output.Complete();
            try
            {
                _socket.Close();
            }
            catch (Exception)
            {
                // ignore, we are closing anyway
            }
        }

        public async ValueTask<InboundFrame> ReadFrame()
        {
            Activity activity = null;
            if (RabbitMQDiagnosticListener.Source.IsEnabled() && RabbitMQDiagnosticListener.Source.IsEnabled("RabbitMQ.Client.ReadFrame"))
            {
                activity = new Activity("RabbitMQ.Client.ReadFrame");
                RabbitMQDiagnosticListener.Source.StartActivity(activity, null);
            }

            InboundFrame frame = default;
            try
            {
                while (true)
                {
                    if (!_pipe.Input.TryRead(out ReadResult result))
                    {
                        try
                        {
                            ValueTask<ReadResult> readTask = _pipe.Input.ReadAsync(_cts.Token);
                            result = readTask.IsCompletedSuccessfully ? readTask.Result : await readTask.ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            throw new EndOfStreamException("EOF reached");
                        }
                        catch (OperationInterruptedException)
                        {
                            throw new EndOfStreamException("EOF reached");
                        }
                    }

                    if (result.Buffer.IsEmpty)
                    {
                        throw new EndOfStreamException("EOF reached");
                    }

                    if (InboundFrame.TryReadFrom(result.Buffer, out frame))
                    {
                        _pipe.Input.AdvanceTo(result.Buffer.GetPosition(8 + frame.Payload.Length));
                        return frame;
                    }
                    else
                    {
                        _pipe.Input.AdvanceTo(result.Buffer.Start, result.Buffer.End);
                    }
                }
            }
            finally
            {
                // stop activity if started
                if (activity != null)
                {
                    RabbitMQDiagnosticListener.Source.StopActivity(activity, new { Frame = frame });
                }
            }
        }

        public async ValueTask SendHeader()
        {
            Memory<byte> headerBytes = _pipe.Output.GetMemory(8);
            Encoding.ASCII.GetBytes("AMQP").CopyTo(headerBytes.Span);
            if (Endpoint.Protocol.Revision != 0)
            {
                headerBytes.Span[4] = 0;
                headerBytes.Span[5] = (byte)Endpoint.Protocol.MajorVersion;
                headerBytes.Span[6] = (byte)Endpoint.Protocol.MinorVersion;
                headerBytes.Span[7] = (byte)Endpoint.Protocol.Revision;
            }
            else
            {
                headerBytes.Span[4] = 1;
                headerBytes.Span[5] = 1;
                headerBytes.Span[6] = (byte)Endpoint.Protocol.MajorVersion;
                headerBytes.Span[7] = (byte)Endpoint.Protocol.MinorVersion;
            }

            _pipe.Output.Advance(8);
            await _pipe.Output.FlushAsync().ConfigureAwait(false);
        }

        public void WriteFrame(in MethodOutboundFrame frame)
        {
            try
            {
                int bufferSize = frame.GetMinimumBufferSize();
                Memory<byte> bytes = _pipe.Output.GetMemory(bufferSize);
                frame.WriteTo(bytes);
                _pipe.Output.Advance(bufferSize);
            }
            catch (Exception e)
            {
                _cts.Cancel();
                _pipe.Output.Complete(e);
            }
        }

        public void WriteFrame(in HeaderOutboundFrame frame)
        {
            try
            {
                int bufferSize = frame.GetMinimumBufferSize();
                Memory<byte> bytes = _pipe.Output.GetMemory(bufferSize);
                frame.WriteTo(bytes);
                _pipe.Output.Advance(bufferSize);
            }
            catch (Exception e)
            {
                _cts.Cancel();
                _pipe.Output.Complete(e);
            }
        }

        public void WriteFrame(in EmptyOutboundFrame frame)
        {
            try
            {
                int bufferSize = frame.GetMinimumBufferSize();
                Memory<byte> bytes = _pipe.Output.GetMemory(bufferSize);
                frame.WriteTo(bytes);
                _pipe.Output.Advance(bufferSize);
            }
            catch (Exception e)
            {
                _cts.Cancel();
                _pipe.Output.Complete(e);
            }
        }

        public void WriteFrame(in BodySegmentOutboundFrame frame)
        {
            try
            {
                int bufferSize = frame.GetMinimumBufferSize();
                Memory<byte> bytes = _pipe.Output.GetMemory(bufferSize);
                frame.WriteTo(bytes);
                _pipe.Output.Advance(bufferSize);
            }
            catch (Exception e)
            {
                _cts.Cancel();
                _pipe.Output.Complete(e);
            }
        }

        public async ValueTask Flush()
        {
            ValueTask<FlushResult> flushTask = _pipe.Output.FlushAsync();
            if (!flushTask.IsCompletedSuccessfully)
            {
                await flushTask.ConfigureAwait(false);
            }
        }

        private bool ShouldTryIPv6(AmqpTcpEndpoint endpoint) => Socket.OSSupportsIPv6 && endpoint.AddressFamily != AddressFamily.InterNetwork;

        private ITcpClient ConnectUsingIPv6(AmqpTcpEndpoint endpoint,
                                            Func<AddressFamily, ITcpClient> socketFactory,
                                            TimeSpan timeout) => ConnectUsingAddressFamily(endpoint, socketFactory, timeout, AddressFamily.InterNetworkV6);

        private ITcpClient ConnectUsingIPv4(AmqpTcpEndpoint endpoint,
                                            Func<AddressFamily, ITcpClient> socketFactory,
                                            TimeSpan timeout) => ConnectUsingAddressFamily(endpoint, socketFactory, timeout, AddressFamily.InterNetwork);

        private ITcpClient ConnectUsingAddressFamily(AmqpTcpEndpoint endpoint,
                                                    Func<AddressFamily, ITcpClient> socketFactory,
                                                    TimeSpan timeout, AddressFamily family)
        {
            ITcpClient socket = socketFactory(family);
            try
            {
                ConnectOrFail(socket, endpoint, timeout);
                return socket;
            }
            catch (ConnectFailureException)
            {
                socket.Dispose();
                throw;
            }
        }

        private void ConnectOrFail(ITcpClient socket, AmqpTcpEndpoint endpoint, TimeSpan timeout)
        {
            try
            {
                socket.ConnectAsync(endpoint.HostName, endpoint.Port)
                     .TimeoutAfter(timeout)
                     .ConfigureAwait(false)
                     // this ensures exceptions aren't wrapped in an AggregateException
                     .GetAwaiter()
                     .GetResult();
            }
            catch (ArgumentException e)
            {
                throw new ConnectFailureException("Connection failed", e);
            }
            catch (SocketException e)
            {
                throw new ConnectFailureException("Connection failed", e);
            }
            catch (NotSupportedException e)
            {
                throw new ConnectFailureException("Connection failed", e);
            }
            catch (TimeoutException e)
            {
                throw new ConnectFailureException("Connection failed", e);
            }
        }
    }
}
