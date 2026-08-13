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
using System.Buffers;
using System.Text;

using BenchmarkDotNet.Attributes;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Benchmarks
{
    [Config(typeof(Config))]
    [BenchmarkCategory("Framing")]
    public class MethodFramingBasicAck
    {
        private BasicAck _basicAck = new BasicAck(ulong.MaxValue, true);

        [Params(0)]
        public ushort Channel { get; set; }

        [Benchmark]
        public int BasicAckWrite() => Framing.SerializeToFrames(ref _basicAck, Channel).Size;
    }

    [Config(typeof(Config))]
    [BenchmarkCategory("Framing")]
    public class MethodFramingBasicPublish
    {
        private const string StringValue = "Exchange_OR_RoutingKey";
        private BasicPublish _basicPublish = new BasicPublish(StringValue, StringValue, false, false);
        private BasicPublishMemory _basicPublishMemory = new BasicPublishMemory(Encoding.UTF8.GetBytes(StringValue), Encoding.UTF8.GetBytes(StringValue), false, false);
        private EmptyBasicProperty _propertiesEmpty = new EmptyBasicProperty();
        private BasicProperties _properties = new BasicProperties { AppId = "Application id", MessageId = "Random message id" };
        private readonly ReadOnlyMemory<byte> _bodyEmpty = ReadOnlyMemory<byte>.Empty;
        private readonly ReadOnlyMemory<byte> _body = new byte[512];
        private readonly ReadOnlySequence<byte> _bodySingleSegment = new ReadOnlySequence<byte>(new byte[512]);
        private readonly ReadOnlySequence<byte> _bodyMultiSegment = CreateSegmentedBody(segmentCount: 8, segmentSize: 64);
        private readonly IDisposable _bodyOwner = new NoOpBodyOwner();

        [Params(0)]
        public ushort Channel { get; set; }

        [Params(0xFFFF)]
        public int FrameMax { get; set; }

        [Benchmark]
        public int BasicPublishWriteNonEmpty() => Framing.SerializeToFrames(ref _basicPublish, ref _properties, _body, bodyOwner: null, Channel, FrameMax).Size;

        [Benchmark]
        public int BasicPublishWrite() => Framing.SerializeToFrames(ref _basicPublish, ref _propertiesEmpty, _bodyEmpty, bodyOwner: null, Channel, FrameMax).Size;

        [Benchmark]
        public int BasicPublishMemoryWrite() => Framing.SerializeToFrames(ref _basicPublishMemory, ref _propertiesEmpty, _bodyEmpty, bodyOwner: null, Channel, FrameMax).Size;

        [Benchmark]
        public int BasicPublishWriteNonEmptyWithOwner() => Framing.SerializeToFrames(ref _basicPublish, ref _properties, _body, _bodyOwner, Channel, FrameMax).Size;

        [Benchmark]
        public int BasicPublishWriteSingleSegmentSequence() => Framing.SerializeToFrames(ref _basicPublish, ref _properties, _bodySingleSegment, bodyOwner: null, Channel, FrameMax).Size;

        [Benchmark]
        public int BasicPublishWriteSingleSegmentSequenceWithOwner() => Framing.SerializeToFrames(ref _basicPublish, ref _properties, _bodySingleSegment, _bodyOwner, Channel, FrameMax).Size;

        [Benchmark]
        public int BasicPublishWriteMultiSegmentSequence() => Framing.SerializeToFrames(ref _basicPublish, ref _properties, _bodyMultiSegment, bodyOwner: null, Channel, FrameMax).Size;

        [Benchmark]
        public int BasicPublishWriteMultiSegmentSequenceWithOwner() => Framing.SerializeToFrames(ref _basicPublish, ref _properties, _bodyMultiSegment, _bodyOwner, Channel, FrameMax).Size;

        private static ReadOnlySequence<byte> CreateSegmentedBody(int segmentCount, int segmentSize)
        {
            var first = new BodySegment(new byte[segmentSize]);
            BodySegment last = first;
            for (int i = 1; i < segmentCount; i++)
            {
                last = last.Append(new byte[segmentSize]);
            }

            return new ReadOnlySequence<byte>(first, 0, last, segmentSize);
        }

        private sealed class BodySegment : ReadOnlySequenceSegment<byte>
        {
            public BodySegment(ReadOnlyMemory<byte> memory)
            {
                Memory = memory;
                RunningIndex = 0;
            }

            public BodySegment Append(ReadOnlyMemory<byte> memory)
            {
                var segment = new BodySegment(memory) { RunningIndex = RunningIndex + Memory.Length };
                Next = segment;
                return segment;
            }
        }

        /// <summary>
        /// The benchmarks measure framing only and never write or dispose the frame, so the body
        /// owner just has to be a non-null <see cref="IDisposable"/> to select the zero-copy path.
        /// </summary>
        private sealed class NoOpBodyOwner : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    [Config(typeof(Config))]
    [BenchmarkCategory("Framing")]
    public class MethodFramingChannelClose
    {
        private ChannelClose _channelClose = new ChannelClose(333, string.Empty, 0099, 2999);

        [Params(0)]
        public ushort Channel { get; set; }

        [Benchmark]
        public int ChannelCloseWrite() => Framing.SerializeToFrames(ref _channelClose, Channel).Size;
    }
}
