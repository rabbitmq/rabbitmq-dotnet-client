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

using System.Collections.Generic;

using RabbitMQ.Client.Impl;

using Xunit;

namespace RabbitMQ.Client.Unit
{

    public class TestPropertiesClone
    {
        [Fact]
        public void TestBasicPropertiesCloneV0_9_1()
        {
            TestBasicPropertiesClone(new Framing.BasicProperties());
        }

        [Fact]
        public void TestBasicPropertiesNoneCloneV0_9_1()
        {
            TestBasicPropertiesNoneClone(new Framing.BasicProperties());
        }

        private void TestBasicPropertiesClone(BasicProperties bp)
        {
            // Set initial values
            bp.ContentType = "foo_1";
            bp.ContentEncoding = "foo_2";
            bp.Headers = new Dictionary<string, object>
            {
                { "foo_3", "foo_4" },
                { "foo_5", "foo_6" }
            };
            bp.DeliveryMode = 2;
            // Persistent also changes DeliveryMode's value to 2
            bp.Persistent = true;
            bp.Priority = 12;
            bp.CorrelationId = "foo_7";
            bp.ReplyTo = "foo_8";
            bp.Expiration = "foo_9";
            bp.MessageId = "foo_10";
            bp.Timestamp = new AmqpTimestamp(123);
            bp.Type = "foo_11";
            bp.UserId = "foo_12";
            bp.AppId = "foo_13";
            bp.ClusterId = "foo_14";

            // Clone
            BasicProperties bpClone = bp.Clone() as BasicProperties;

            // Change values in source object
            bp.ContentType = "foo_15";
            bp.ContentEncoding = "foo_16";
            bp.Headers.Remove("foo_3");
            bp.Headers.Remove("foo_5");
            bp.Headers.Add("foo_17", "foo_18");
            bp.Headers.Add("foo_19", "foo_20");
            bp.DeliveryMode = 1;
            // Persistent also changes DeliveryMode's value to 1
            bp.Persistent = false;
            bp.Priority = 23;
            bp.CorrelationId = "foo_21";
            bp.ReplyTo = "foo_22";
            bp.Expiration = "foo_23";
            bp.MessageId = "foo_24";
            bp.Timestamp = new AmqpTimestamp(234);
            bp.Type = "foo_25";
            bp.UserId = "foo_26";
            bp.AppId = "foo_27";
            bp.ClusterId = "foo_28";

            // Make sure values have not changed in clone
            Assert.Equal("foo_1", bpClone.ContentType);
            Assert.Equal("foo_2", bpClone.ContentEncoding);
            Assert.Equal(2, bpClone.Headers.Count);
            Assert.True(bpClone.Headers.ContainsKey("foo_3"));
            Assert.Equal("foo_4", bpClone.Headers["foo_3"]);
            Assert.True(bpClone.Headers.ContainsKey("foo_5"));
            Assert.Equal("foo_6", bpClone.Headers["foo_5"]);
            Assert.Equal(2, bpClone.DeliveryMode);
            Assert.True(bpClone.Persistent);
            Assert.Equal(12, bpClone.Priority);
            Assert.Equal("foo_7", bpClone.CorrelationId);
            Assert.Equal("foo_8", bpClone.ReplyTo);
            Assert.Null(bpClone.ReplyToAddress);
            Assert.Equal("foo_9", bpClone.Expiration);
            Assert.Equal("foo_10", bpClone.MessageId);
            Assert.Equal(new AmqpTimestamp(123), bpClone.Timestamp);
            Assert.Equal("foo_11", bpClone.Type);
            Assert.Equal("foo_12", bpClone.UserId);
            Assert.Equal("foo_13", bpClone.AppId);
            Assert.Equal("foo_14", bpClone.ClusterId);
        }

        private void TestBasicPropertiesNoneClone(BasicProperties bp)
        {
            // Do not set any member and clone
            BasicProperties bpClone = bp.Clone() as BasicProperties;

            // Set members in source object
            bp.ContentType = "foo_1";
            bp.ContentEncoding = "foo_2";
            bp.Headers = new Dictionary<string, object>
            {
                { "foo_3", "foo_4" },
                { "foo_5", "foo_6" }
            };
            bp.DeliveryMode = 2;
            // Persistent also changes DeliveryMode's value to 2
            bp.Persistent = true;
            bp.Priority = 12;
            bp.CorrelationId = "foo_7";
            bp.ReplyTo = "foo_8";
            bp.Expiration = "foo_9";
            bp.MessageId = "foo_10";
            bp.Timestamp = new AmqpTimestamp(123);
            bp.Type = "foo_11";
            bp.UserId = "foo_12";
            bp.AppId = "foo_13";
            bp.ClusterId = "foo_14";

            // Check that no member is present in clone
            Assert.False(bpClone.IsContentTypePresent());
            Assert.False(bpClone.IsContentEncodingPresent());
            Assert.False(bpClone.IsHeadersPresent());
            Assert.False(bpClone.IsDeliveryModePresent());
            Assert.False(bpClone.IsPriorityPresent());
            Assert.False(bpClone.IsCorrelationIdPresent());
            Assert.False(bpClone.IsReplyToPresent());
            Assert.False(bpClone.IsExpirationPresent());
            Assert.False(bpClone.IsMessageIdPresent());
            Assert.False(bpClone.IsTimestampPresent());
            Assert.False(bpClone.IsTypePresent());
            Assert.False(bpClone.IsUserIdPresent());
            Assert.False(bpClone.IsAppIdPresent());
            Assert.False(bpClone.IsClusterIdPresent());
        }
    }
}
