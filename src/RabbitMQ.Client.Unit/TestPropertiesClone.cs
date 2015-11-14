// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2015 Pivotal Software, Inc.
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
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System.Collections.Generic;
using NUnit.Framework;
using RabbitMQ.Client;
using RabbitMQ.Client.Impl;

[TestFixture]
public class TestPropertiesClone
{
    [Test]
    public void TestBasicPropertiesCloneV0_9_1()
    {
        TestBasicPropertiesClone(new RabbitMQ.Client.Framing.BasicProperties());
    }

    [Test]
    public void TestBasicPropertiesNoneCloneV0_9_1()
    {
        TestBasicPropertiesNoneClone(new RabbitMQ.Client.Framing.BasicProperties());
    }

    private void TestBasicPropertiesClone(BasicProperties bp)
    {
        // Set initial values
        bp.ContentType = "foo_1";
        bp.ContentEncoding = "foo_2";
        bp.Headers = new Dictionary<string, object>();
        bp.Headers.Add("foo_3", "foo_4");
        bp.Headers.Add("foo_5", "foo_6");
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
        Assert.AreEqual("foo_1", bpClone.ContentType);
        Assert.AreEqual("foo_2", bpClone.ContentEncoding);
        Assert.AreEqual(2, bpClone.Headers.Count);
        Assert.AreEqual(true, bpClone.Headers.ContainsKey("foo_3"));
        Assert.AreEqual("foo_4", bpClone.Headers["foo_3"]);
        Assert.AreEqual(true, bpClone.Headers.ContainsKey("foo_5"));
        Assert.AreEqual("foo_6", bpClone.Headers["foo_5"]);
        Assert.AreEqual(2, bpClone.DeliveryMode);
        Assert.AreEqual(true, bpClone.Persistent);
        Assert.AreEqual(12, bpClone.Priority);
        Assert.AreEqual("foo_7", bpClone.CorrelationId);
        Assert.AreEqual("foo_8", bpClone.ReplyTo);
        Assert.AreEqual("foo_9", bpClone.Expiration);
        Assert.AreEqual("foo_10", bpClone.MessageId);
        Assert.AreEqual(new AmqpTimestamp(123), bpClone.Timestamp);
        Assert.AreEqual("foo_11", bpClone.Type);
        Assert.AreEqual("foo_12", bpClone.UserId);
        Assert.AreEqual("foo_13", bpClone.AppId);
        Assert.AreEqual("foo_14", bpClone.ClusterId);
    }

    private void TestBasicPropertiesNoneClone(BasicProperties bp)
    {
        // Do not set any member and clone
        BasicProperties bpClone = bp.Clone() as BasicProperties;

        // Set members in source object
        bp.ContentType = "foo_1";
        bp.ContentEncoding = "foo_2";
        bp.Headers = new Dictionary<string, object>();
        bp.Headers.Add("foo_3", "foo_4");
        bp.Headers.Add("foo_5", "foo_6");
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
        Assert.AreEqual(false, bpClone.IsContentTypePresent());
        Assert.AreEqual(false, bpClone.IsContentEncodingPresent());
        Assert.AreEqual(false, bpClone.IsHeadersPresent());
        Assert.AreEqual(false, bpClone.IsDeliveryModePresent());
        Assert.AreEqual(false, bpClone.IsPriorityPresent());
        Assert.AreEqual(false, bpClone.IsCorrelationIdPresent());
        Assert.AreEqual(false, bpClone.IsReplyToPresent());
        Assert.AreEqual(false, bpClone.IsExpirationPresent());
        Assert.AreEqual(false, bpClone.IsMessageIdPresent());
        Assert.AreEqual(false, bpClone.IsTimestampPresent());
        Assert.AreEqual(false, bpClone.IsTypePresent());
        Assert.AreEqual(false, bpClone.IsUserIdPresent());
        Assert.AreEqual(false, bpClone.IsAppIdPresent());
        Assert.AreEqual(false, bpClone.IsClusterIdPresent());
    }

    private void TestStreamPropertiesClone(StreamProperties sp)
    {
        // Set members in source object
        sp.ContentType = "foo_1";
        sp.ContentEncoding = "foo_2";
        sp.Headers = new Dictionary<string, object>();
        sp.Headers.Add("foo_3", "foo_4");
        sp.Headers.Add("foo_5", "foo_6");
        sp.Priority = 12;
        sp.Timestamp = new AmqpTimestamp(123);

        // Clone
        StreamProperties spClone = sp.Clone() as StreamProperties;

        // Change values in source object
        sp.ContentType = "foo_7";
        sp.ContentEncoding = "foo_8";
        sp.Headers.Remove("foo_3");
        sp.Headers.Remove("foo_5");
        sp.Headers.Add("foo_9", "foo_10");
        sp.Headers.Add("foo_11", "foo_12");
        sp.Priority = 34;
        sp.Timestamp = new AmqpTimestamp(234);

        // Make sure values have not changed in clone
        Assert.AreEqual("foo_1", spClone.ContentType);
        Assert.AreEqual("foo_2", spClone.ContentEncoding);
        Assert.AreEqual(2, spClone.Headers.Count);
        Assert.AreEqual(true, spClone.Headers.ContainsKey("foo_3"));
        Assert.AreEqual("foo_4", spClone.Headers["foo_3"]);
        Assert.AreEqual(true, spClone.Headers.ContainsKey("foo_5"));
        Assert.AreEqual("foo_6", spClone.Headers["foo_5"]);
        Assert.AreEqual(12, spClone.Priority);
        Assert.AreEqual(new AmqpTimestamp(123), spClone.Timestamp);
    }

    private void TestStreamPropertiesNoneClone(StreamProperties sp)
    {
        // Do not set any members and clone
        StreamProperties spClone = sp.Clone() as StreamProperties;

        // Set members in source object
        sp.ContentType = "foo_1";
        sp.ContentEncoding = "foo_2";
        sp.Headers = new Dictionary<string, object>();
        sp.Headers.Add("foo_3", "foo_4");
        sp.Headers.Add("foo_5", "foo_6");
        sp.Priority = 12;
        sp.Timestamp = new AmqpTimestamp(123);

        // Check that no member is present in clone
        Assert.AreEqual(false, spClone.IsContentTypePresent());
        Assert.AreEqual(false, spClone.IsContentEncodingPresent());
        Assert.AreEqual(false, spClone.IsHeadersPresent());
        Assert.AreEqual(false, spClone.IsPriorityPresent());
        Assert.AreEqual(false, spClone.IsTimestampPresent());
    }
}
