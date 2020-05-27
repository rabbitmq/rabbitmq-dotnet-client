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

using NUnit.Framework;

using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    class TestFrameFormatting : WireFormattingFixture
    {
        [Test]
        public void EmptyOutboundFrame()
        {
            var frame = new EmptyOutboundFrame();
            var body = new byte[frame.GetMinimumBufferSize()];

            frame.WriteTo(body);

            Assert.AreEqual(0, frame.GetMinimumPayloadBufferSize());
            Assert.AreEqual(8, frame.GetMinimumBufferSize());
            Assert.AreEqual(Constants.FrameHeartbeat, body[0]);
            Assert.AreEqual(0, body[1]); // channel
            Assert.AreEqual(0, body[2]); // channel
            Assert.AreEqual(0, body[3]); // payload size
            Assert.AreEqual(0, body[4]); // payload size
            Assert.AreEqual(0, body[5]); // payload size
            Assert.AreEqual(0, body[6]); // payload size
            Assert.AreEqual(Constants.FrameEnd, body[7]);
        }
    }
}
