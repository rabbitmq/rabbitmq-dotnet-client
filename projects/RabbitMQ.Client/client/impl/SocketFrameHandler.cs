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
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Pipelines.Sockets.Unofficial;
using Pipelines.Sockets.Unofficial.Threading;

using RabbitMQ.Client.Exceptions;

using static Pipelines.Sockets.Unofficial.Threading.MutexSlim;

namespace RabbitMQ.Client.Impl
{
    internal static class TaskExtensions
    {
        public static async Task TimeoutAfter(this Task task, TimeSpan timeout)
        {
            if (task == await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false))
            {
                await task.ConfigureAwait(false);
            }
            else
            {
                Task supressErrorTask = task.ContinueWith((t, s) => t.Exception.Handle(e => true), null, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
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
        private int _writeableStateTimeoutMicroSeconds;
        private readonly ITcpClient _socket;
        private readonly ChannelWriter<OutgoingFrame> _channelWriter;
        private readonly ChannelReader<OutgoingFrame> _channelReader;
        public PipeReader PipeReader { get; private set; }

        private readonly PipeWriter _pipeWriter;
        private Task _writerTask;
        private readonly object _semaphore = new object();
        private readonly IMeasuredDuplexPipe _pipe;
        private bool _closed;
        private readonly MutexSlim _singleWriterMutex;

        public SocketFrameHandler(AmqpTcpEndpoint endpoint, Func<AddressFamily, ITcpClient> socketFactory, TimeSpan connectionTimeout, TimeSpan readTimeout, TimeSpan writeTimeout)
        {
            Endpoint = endpoint;
            var channel = Channel.CreateUnbounded<OutgoingFrame>(
                new UnboundedChannelOptions
                {
                    AllowSynchronousContinuations = true,
                    SingleReader = true,
                    SingleWriter = true
                });

            _channelReader = channel.Reader;
            _channelWriter = channel.Writer;

            // Resolve the hostname to know if it's even possible to even try IPv6
            IPAddress[] adds = Dns.GetHostAddresses(endpoint.HostName);
            IPAddress ipv6 = TcpClientAdapterHelper.GetMatchingHost(adds, AddressFamily.InterNetworkV6);

            if (ipv6 == default(IPAddress))
            {
                if (endpoint.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    throw new ConnectFailureException("Connection failed", new ArgumentException($"No IPv6 address could be resolved for {endpoint.HostName}"));
                }
            }
            else if (ShouldTryIPv6(endpoint))
            {
                try
                {
                    _socket = ConnectUsingIPv6(new IPEndPoint(ipv6, endpoint.Port), socketFactory, connectionTimeout);
                }
                catch (ConnectFailureException)
                {
                    // We resolved to a ipv6 address and tried it but it still didn't connect, try IPv4
                    _socket = null;
                }
            }

            if (_socket is null)
            {
                IPAddress ipv4 = TcpClientAdapterHelper.GetMatchingHost(adds, AddressFamily.InterNetwork);
                if (ipv4 == default(IPAddress))
                {
                    throw new ConnectFailureException("Connection failed", new ArgumentException($"No ip address could be resolved for {endpoint.HostName}"));
                }
                _socket = ConnectUsingIPv4(new IPEndPoint(ipv4, endpoint.Port), socketFactory, connectionTimeout);
            }

            var SendPipeOptions = new PipeOptions(pauseWriterThreshold: 512 * 1024, resumeWriterThreshold: 384 * 1024, useSynchronizationContext: false);
            var ReceivePipeOptions = new PipeOptions(pauseWriterThreshold: 512 * 1024, resumeWriterThreshold: 384 * 1024, useSynchronizationContext: false);

            if (endpoint.Ssl.Enabled)
            {
                try
                {
                    Stream netstream = _socket.GetStream();
                    netstream.ReadTimeout = (int)readTimeout.TotalMilliseconds;
                    netstream.WriteTimeout = (int)writeTimeout.TotalMilliseconds;
                    netstream = SslHelper.TcpUpgrade(netstream, endpoint.Ssl);
                    _pipe = StreamConnection.GetDuplex(netstream, SendPipeOptions, ReceivePipeOptions) as IMeasuredDuplexPipe;
                }
                catch (Exception)
                {
                    Close();
                    throw;
                }
            }
            else
            {
                _pipe = SocketConnection.Create(_socket.Client, SendPipeOptions, ReceivePipeOptions);
            }

            PipeReader = _pipe.Input;
            _pipeWriter = _pipe.Output;

            WriteTimeout = writeTimeout;
            _singleWriterMutex = new MutexSlim((int)writeTimeout.TotalMilliseconds);
        }
        public AmqpTcpEndpoint Endpoint { get; set; }

        public EndPoint LocalEndPoint
        {
            get { return _socket.Client.LocalEndPoint; }
        }

        public int LocalPort
        {
            get { return ((IPEndPoint)LocalEndPoint).Port; }
        }

        public EndPoint RemoteEndPoint
        {
            get { return _socket.Client.RemoteEndPoint; }
        }

        public int RemotePort
        {
            get { return ((IPEndPoint)LocalEndPoint).Port; }
        }

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
                _writeableStateTimeoutMicroSeconds = _socket.Client.SendTimeout * 1000;
            }
        }

        public void Close()
        {
            lock (_semaphore)
            {
                if (!_closed)
                {
                    _channelWriter.TryComplete();
                    //try { _writerTask.GetAwaiter().GetResult(); } catch { }

                    if (_pipe != null)
                    {
                        try { _pipe.Input?.CancelPendingRead(); } catch { }
                        try { _pipe.Input?.Complete(); } catch { }
                        try { _pipe.Output?.CancelPendingFlush(); } catch { }
                        try { _pipe.Output?.Complete(); } catch { }
                        try { using (_pipe as IDisposable) { } } catch { }
                    }

                    try { _socket.Close(); } catch { }
                    _closed = true;
                }
            }
        }

        public void SendHeader()
        {
            Span<byte> headerBytes = _pipeWriter.GetSpan(8);
            headerBytes[0] = (byte)'A';
            headerBytes[1] = (byte)'M';
            headerBytes[2] = (byte)'Q';
            headerBytes[3] = (byte)'P';
            if (Endpoint.Protocol.Revision != 0)
            {
                headerBytes[4] = 0;
                headerBytes[5] = (byte)Endpoint.Protocol.MajorVersion;
                headerBytes[6] = (byte)Endpoint.Protocol.MinorVersion;
                headerBytes[7] = (byte)Endpoint.Protocol.Revision;
            }
            else
            {
                headerBytes[4] = 1;
                headerBytes[5] = 1;
                headerBytes[6] = (byte)Endpoint.Protocol.MajorVersion;
                headerBytes[7] = (byte)Endpoint.Protocol.MinorVersion;
            }

            _pipeWriter.Advance(8);
            if (!_pipeWriter.FlushAsync().AsTask().Wait(_writeableStateTimeout))
            {
                var timeout = new TimeoutException();
                _pipeWriter.Complete(timeout);
                _channelWriter.Complete(timeout);
            }
        }

        public void Write(OutgoingFrame frame)
        {
            LockToken token = default;
            try
            {
                token = _singleWriterMutex.TryWait(WaitOptions.NoDelay);
                if (!token.Success)
                {
                    // Didn't get the lock immediately, let's backlog the write and start the backlog writer task.
                    if (_channelWriter.TryWrite(frame))
                    {
                        if (_writerTask == null || _writerTask.Status == TaskStatus.RanToCompletion)
                        {
                            _writerTask = Task.Run(WriteLoop, CancellationToken.None);
                        }

                        return;
                    }

                    token = _singleWriterMutex.TryWait();
                    if (!token.Success)
                    {
                        throw new TimeoutException();
                    }
                }

                ValueTask<bool> task = WriteFrameToPipe(frame);
                if (!task.IsCompleted)
                {
                    task.AsTask().Wait();
                }
            }
            finally
            {
                token.Dispose();
            }
        }

        private ValueTask<bool> WriteFrameToPipe(OutgoingFrame frame)
        {
            switch (frame.FrameType)
            {
                case FrameType.FrameHeartbeat:
                    Memory<byte> memory = _pipeWriter.GetMemory(Framing.Heartbeat.FrameSize);
                    Framing.Heartbeat.WriteTo(memory.Span);
                    _pipeWriter.Advance(Framing.Heartbeat.FrameSize);
                    break;
                case FrameType.FrameMethod:
                    int size = frame.GetMaxSize(frame.MaxBodyPayloadBytes);

                    // Will be returned by SocketFrameWriter.WriteLoop
                    Memory<byte> methodMemory = _pipeWriter.GetMemory(size);
                    int offset = Framing.Method.WriteTo(methodMemory.Span, frame.Channel, frame.Method);
                    if (frame.Method.HasContent)
                    {
                        int remainingBodyBytes = frame.Body.Length;
                        offset += Framing.Header.WriteTo(methodMemory.Span.Slice(offset), frame.Channel, frame.Header, remainingBodyBytes);
                        while (remainingBodyBytes > 0)
                        {
                            int frameSize = remainingBodyBytes > frame.MaxBodyPayloadBytes ? frame.MaxBodyPayloadBytes : remainingBodyBytes;
                            offset += Framing.BodySegment.WriteTo(methodMemory.Span.Slice(offset), frame.Channel, frame.Body.Span.Slice(frame.Body.Span.Length - remainingBodyBytes, frameSize));
                            remainingBodyBytes -= frameSize;
                        }
                    }

                    _pipeWriter.Advance(size);
                    break;
            }

            return Flush(_pipeWriter);
        }

        private async Task WriteLoop()
        {
            LockToken token = default;
            try
            {
                // Let's start by getting the writer mutex
                token = _singleWriterMutex.TryWait(WaitOptions.NoDelay);
                if (!token.Success)
                {
                    token = await _singleWriterMutex.TryWaitAsync(options: WaitOptions.DisableAsyncContext).ConfigureAwait(false);
                    if (!token.Success)
                    {
                        throw new TimeoutException();
                    }
                }

                while (_channelReader.TryRead(out OutgoingFrame outgoingFrame))
                {
                    ValueTask<bool> task = WriteFrameToPipe(outgoingFrame);
                    if (!task.IsCompleted)
                    {
                        await task.ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                token.Dispose();
            }
        }

        private static ValueTask<bool> Flush(PipeWriter writer)
        {
            bool GetResult(FlushResult flush)
                // tell the calling code whether any more messages
                // should be written
                => !(flush.IsCanceled || flush.IsCompleted);

            async ValueTask<bool> Awaited(ValueTask<FlushResult> incomplete)
                => GetResult(await incomplete.ConfigureAwait(false));

            // apply back-pressure etc
            ValueTask<FlushResult> flushTask = writer.FlushAsync();

            return flushTask.IsCompletedSuccessfully
                ? new ValueTask<bool>(GetResult(flushTask.Result))
                : Awaited(flushTask);
        }

        private static bool ShouldTryIPv6(AmqpTcpEndpoint endpoint)
        {
            return Socket.OSSupportsIPv6 && endpoint.AddressFamily != AddressFamily.InterNetwork;
        }

        private ITcpClient ConnectUsingIPv6(IPEndPoint endpoint,
                                            Func<AddressFamily, ITcpClient> socketFactory,
                                            TimeSpan timeout)
        {
            return ConnectUsingAddressFamily(endpoint, socketFactory, timeout, AddressFamily.InterNetworkV6);
        }

        private ITcpClient ConnectUsingIPv4(IPEndPoint endpoint,
                                            Func<AddressFamily, ITcpClient> socketFactory,
                                            TimeSpan timeout)
        {
            return ConnectUsingAddressFamily(endpoint, socketFactory, timeout, AddressFamily.InterNetwork);
        }

        private ITcpClient ConnectUsingAddressFamily(IPEndPoint endpoint,
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

        private void ConnectOrFail(ITcpClient socket, IPEndPoint endpoint, TimeSpan timeout)
        {
            try
            {
                socket.ConnectAsync(endpoint.Address, endpoint.Port)
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
