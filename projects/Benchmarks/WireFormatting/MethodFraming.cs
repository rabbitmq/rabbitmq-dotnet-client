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
        internal OutgoingFrameMemory BasicAckWrite() => Framing.SerializeToFrames(ref _basicAck, Channel);
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

        [Params(0)]
        public ushort Channel { get; set; }

        [Params(0xFFFF)]
        public int FrameMax { get; set; }

        [Benchmark]
        internal OutgoingFrameMemory BasicPublishWriteNonEmpty() => Framing.SerializeToFrames(ref _basicPublish, ref _properties, _body, Channel, FrameMax);

        [Benchmark]
        internal OutgoingFrameMemory BasicPublishWrite() => Framing.SerializeToFrames(ref _basicPublish, ref _propertiesEmpty, _bodyEmpty, Channel, FrameMax);

        [Benchmark]
        internal OutgoingFrameMemory BasicPublishMemoryWrite() => Framing.SerializeToFrames(ref _basicPublishMemory, ref _propertiesEmpty, _bodyEmpty, Channel, FrameMax);
    }

    [Config(typeof(Config))]
    [BenchmarkCategory("Framing")]
    public class MethodFramingChannelClose
    {
        private ChannelClose _channelClose = new ChannelClose(333, string.Empty, 0099, 2999);

        [Params(0)]
        public ushort Channel { get; set; }

        [Benchmark]
        internal OutgoingFrameMemory ChannelCloseWrite() => Framing.SerializeToFrames(ref _channelClose, Channel);
    }
}
