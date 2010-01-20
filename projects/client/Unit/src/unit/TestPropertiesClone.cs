// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2009 LShift Ltd., Cohesive Financial
//   Technologies LLC., and Rabbit Technologies Ltd.
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
//   The contents of this file are subject to the Mozilla Public License
//   Version 1.1 (the "License"); you may not use this file except in
//   compliance with the License. You may obtain a copy of the License at
//   http://www.rabbitmq.com/mpl.html
//
//   Software distributed under the License is distributed on an "AS IS"
//   basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
//   License for the specific language governing rights and limitations
//   under the License.
//
//   The Original Code is The RabbitMQ .NET Client.
//
//   The Initial Developers of the Original Code are LShift Ltd,
//   Cohesive Financial Technologies LLC, and Rabbit Technologies Ltd.
//
//   Portions created before 22-Nov-2008 00:00:00 GMT by LShift Ltd,
//   Cohesive Financial Technologies LLC, or Rabbit Technologies Ltd
//   are Copyright (C) 2007-2008 LShift Ltd, Cohesive Financial
//   Technologies LLC, and Rabbit Technologies Ltd.
//
//   Portions created by LShift Ltd are Copyright (C) 2007-2009 LShift
//   Ltd. Portions created by Cohesive Financial Technologies LLC are
//   Copyright (C) 2007-2009 Cohesive Financial Technologies
//   LLC. Portions created by Rabbit Technologies Ltd are Copyright
//   (C) 2007-2009 Rabbit Technologies Ltd.
//
//   All Rights Reserved.
//
//   Contributor(s): ______________________________________.
//
//---------------------------------------------------------------------------
using NUnit.Framework;

using System;
using System.IO;
using System.Collections;

using RabbitMQ.Client;
using RabbitMQ.Client.Impl;

[TestFixture]
public class TestPropertiesClone
{
    [Test]
    public void TestBasicPropertiesCloneV0_8()
    {
        TestBasicPropertiesClone(new RabbitMQ.Client.Framing.v0_8.BasicProperties());
    }

    [Test]
    public void TestBasicPropertiesNoneCloneV0_8()
    {
        TestBasicPropertiesNoneClone(new RabbitMQ.Client.Framing.v0_8.BasicProperties());
    }

    [Test]
    public void TestBasicPropertiesCloneV0_8qpid()
    {
        TestBasicPropertiesClone(new RabbitMQ.Client.Framing.v0_8qpid.BasicProperties());
    }

    [Test]
    public void TestBasicPropertiesNoneCloneV0_8qpid()
    {
        TestBasicPropertiesNoneClone(new RabbitMQ.Client.Framing.v0_8qpid.BasicProperties());
    }

    [Test]
    public void TestBasicPropertiesCloneV0_9()
    {
        TestBasicPropertiesClone(new RabbitMQ.Client.Framing.v0_9.BasicProperties());
    }

    [Test]
    public void TestBasicPropertiesNoneCloneV0_9()
    {
        TestBasicPropertiesNoneClone(new RabbitMQ.Client.Framing.v0_9.BasicProperties());
    }

    [Test]
    public void TestStreamPropertiesCloneV0_8()
    {
        TestStreamPropertiesClone(new RabbitMQ.Client.Framing.v0_8.StreamProperties());
    }

    [Test]
    public void TestStreamPropertiesNoneCloneV0_8()
    {
        TestStreamPropertiesNoneClone(new RabbitMQ.Client.Framing.v0_8.StreamProperties());
    }

    [Test]
    public void TestStreamPropertiesCloneV0_8qpid()
    {
        TestStreamPropertiesClone(new RabbitMQ.Client.Framing.v0_8qpid.StreamProperties());
    }

    [Test]
    public void TestStreamPropertiesNoneCloneV0_8qpid()
    {
        TestStreamPropertiesNoneClone(new RabbitMQ.Client.Framing.v0_8qpid.StreamProperties());
    }

    [Test]
    public void TestStreamPropertiesCloneV0_9()
    {
        TestStreamPropertiesClone(new RabbitMQ.Client.Framing.v0_9.StreamProperties());
    }

    [Test]
    public void TestStreamPropertiesNoneCloneV0_9()
    {
        TestStreamPropertiesNoneClone(new RabbitMQ.Client.Framing.v0_9.StreamProperties());
    }

    [Test]
    public void TestFilePropertiesCloneV0_8()
    {
        TestFilePropertiesClone(new RabbitMQ.Client.Framing.v0_8.FileProperties());
    }

    [Test]
    public void TestFilePropertiesNoneCloneV0_8()
    {
        TestFilePropertiesNoneClone(new RabbitMQ.Client.Framing.v0_8.FileProperties());
    }

    [Test]
    public void TestFilePropertiesCloneV0_8qpid()
    {
        TestFilePropertiesClone(new RabbitMQ.Client.Framing.v0_8qpid.FileProperties());
    }

    [Test]
    public void TestFilePropertiesNoneCloneV0_8qpid()
    {
        TestFilePropertiesNoneClone(new RabbitMQ.Client.Framing.v0_8qpid.FileProperties());
    }

    [Test]
    public void TestFilePropertiesCloneV0_9()
    {
        TestFilePropertiesClone(new RabbitMQ.Client.Framing.v0_9.FileProperties());
    }

    [Test]
    public void TestFilePropertiesNoneCloneV0_9()
    {
        TestFilePropertiesNoneClone(new RabbitMQ.Client.Framing.v0_9.FileProperties());
    }

    private void TestBasicPropertiesClone(BasicProperties bp)
    {
        // Set initial values
        bp.ContentType = "foo_1";
        bp.ContentEncoding = "foo_2";
        bp.Headers = new Hashtable();
        bp.Headers.Add("foo_3", "foo_4");
        bp.Headers.Add("foo_5", "foo_6");
        bp.DeliveryMode = 12;
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
        bp.DeliveryMode = 23;
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
        Assert.AreEqual(true, bpClone.Headers.Contains("foo_3"));
        Assert.AreEqual("foo_4", bpClone.Headers["foo_3"]);
        Assert.AreEqual(true, bpClone.Headers.Contains("foo_5"));
        Assert.AreEqual("foo_6", bpClone.Headers["foo_5"]);
        Assert.AreEqual(12, bpClone.DeliveryMode);
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
        bp.Headers = new Hashtable();
        bp.Headers.Add("foo_3", "foo_4");
        bp.Headers.Add("foo_5", "foo_6");
        bp.DeliveryMode = 12;
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
        sp.Headers = new Hashtable();
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
        Assert.AreEqual(true, spClone.Headers.Contains("foo_3"));
        Assert.AreEqual("foo_4", spClone.Headers["foo_3"]);
        Assert.AreEqual(true, spClone.Headers.Contains("foo_5"));
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
        sp.Headers = new Hashtable();
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

    private void TestFilePropertiesClone(FileProperties fp)
    {
        // Set members in source object
        fp.ContentType = "foo_1";
        fp.ContentEncoding = "foo_2";
        fp.Headers = new Hashtable();
        fp.Headers.Add("foo_3", "foo_4");
        fp.Headers.Add("foo_5", "foo_6");
        fp.Priority = 12;
        fp.ReplyTo = "foo_7";
        fp.MessageId = "foo_8";
        fp.Filename = "foo_9";
        fp.Timestamp = new AmqpTimestamp(123);
        fp.ClusterId = "foo_10";

        // Clone
        FileProperties fpClone = fp.Clone() as FileProperties;

        // Change values in source object
        fp.ContentType = "foo_11";
        fp.ContentEncoding = "foo_12";
        fp.Headers = new Hashtable();
        fp.Headers.Add("foo_13", "foo_14");
        fp.Headers.Add("foo_15", "foo_16");
        fp.Priority = 34;
        fp.ReplyTo = "foo_17";
        fp.MessageId = "foo_18";
        fp.Filename = "foo_19";
        fp.Timestamp = new AmqpTimestamp(234);
        fp.ClusterId = "foo_20";

        // Make sure values have not changed in clone
        Assert.AreEqual("foo_1", fpClone.ContentType);
        Assert.AreEqual("foo_2", fpClone.ContentEncoding);
        Assert.AreEqual(2, fpClone.Headers.Count);
        Assert.AreEqual(true, fpClone.Headers.Contains("foo_3"));
        Assert.AreEqual("foo_4", fpClone.Headers["foo_3"]);
        Assert.AreEqual(true, fpClone.Headers.Contains("foo_5"));
        Assert.AreEqual("foo_6", fpClone.Headers["foo_5"]);
        Assert.AreEqual(12, fpClone.Priority);
        Assert.AreEqual("foo_7", fpClone.ReplyTo);
        Assert.AreEqual("foo_8", fpClone.MessageId);
        Assert.AreEqual("foo_9", fpClone.Filename);
        Assert.AreEqual(new AmqpTimestamp(123), fpClone.Timestamp);
        Assert.AreEqual("foo_10", fpClone.ClusterId);
    }

    private void TestFilePropertiesNoneClone(FileProperties fp)
    {
        // Do not set any members and clone
        FileProperties fpClone = fp.Clone() as FileProperties;

        // Set values in source object
        fp.ContentType = "foo_11";
        fp.ContentEncoding = "foo_12";
        fp.Headers = new Hashtable();
        fp.Headers.Add("foo_13", "foo_14");
        fp.Headers.Add("foo_15", "foo_16");
        fp.Priority = 34;
        fp.ReplyTo = "foo_17";
        fp.MessageId = "foo_18";
        fp.Filename = "foo_19";
        fp.Timestamp = new AmqpTimestamp(234);
        fp.ClusterId = "foo_20";

        // Check that no member is present in clone
        Assert.AreEqual(false, fpClone.IsContentTypePresent());
        Assert.AreEqual(false, fpClone.IsContentEncodingPresent());
        Assert.AreEqual(false, fpClone.IsHeadersPresent());
        Assert.AreEqual(false, fpClone.IsPriorityPresent());
        Assert.AreEqual(false, fpClone.IsReplyToPresent());
        Assert.AreEqual(false, fpClone.IsMessageIdPresent());
        Assert.AreEqual(false, fpClone.IsFilenamePresent());
        Assert.AreEqual(false, fpClone.IsTimestampPresent());
        Assert.AreEqual(false, fpClone.IsClusterIdPresent());
    }
}
