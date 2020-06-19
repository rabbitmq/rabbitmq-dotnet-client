// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2011-2020 VMware, Inc. or its affiliates.
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
//  Copyright (c) 2011-2020 VMware, Inc. or its affiliates.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;

namespace RabbitMQ.Client.Unit
{

    [TestFixture]
    internal class TestBasicProperties
    {
        [Test]
        public void TestPersistentPropertyChangesDeliveryMode_PersistentTrueDelivery2()
        {
            // Arrange
            var subject = new Framing.BasicProperties
            {
                // Act
                Persistent = true
            };

            // Assert
            Assert.AreEqual(2, subject.DeliveryMode);
            Assert.AreEqual(true, subject.Persistent);
        }

        [Test]
        public void TestPersistentPropertyChangesDeliveryMode_PersistentFalseDelivery1()
        {
            // Arrange
            var subject = new Framing.BasicProperties
            {

                // Act
                Persistent = false
            };

            // Assert
            Assert.AreEqual(1, subject.DeliveryMode);
            Assert.AreEqual(false, subject.Persistent);
        }

        [Test]
        public void TestNullableProperties_CanWrite(
            [Values(null, "cluster1")] string clusterId,
            [Values(null, "732E39DC-AF56-46E8-B8A9-079C4B991B2E")] string correlationId,
            [Values(null, "7D221C7E-1788-4D11-9CA5-AC41425047CF")] string messageId
            )
        {
            // Arrange
            var subject = new Framing.BasicProperties
            {

                // Act
                ClusterId = clusterId,
                CorrelationId = correlationId,
                MessageId = messageId
            };

            // Assert
            bool isClusterIdPresent = clusterId != null;
            Assert.AreEqual(isClusterIdPresent, subject.IsClusterIdPresent());

            bool isCorrelationIdPresent = correlationId != null;
            Assert.AreEqual(isCorrelationIdPresent, subject.IsCorrelationIdPresent());

            bool isMessageIdPresent = messageId != null;
            Assert.AreEqual(isMessageIdPresent, subject.IsMessageIdPresent());

            var writer = new Impl.ContentHeaderPropertyWriter(new byte[1024]);
            subject.WritePropertiesTo(ref writer);

            // Read from Stream
            var propertiesFromStream = new Framing.BasicProperties();
            var reader = new Impl.ContentHeaderPropertyReader(writer.Memory.Slice(0, writer.Offset));
            propertiesFromStream.ReadPropertiesFrom(ref reader);

            Assert.AreEqual(clusterId, propertiesFromStream.ClusterId);
            Assert.AreEqual(correlationId, propertiesFromStream.CorrelationId);
            Assert.AreEqual(messageId, propertiesFromStream.MessageId);
            Assert.AreEqual(isClusterIdPresent, propertiesFromStream.IsClusterIdPresent());
            Assert.AreEqual(isCorrelationIdPresent, propertiesFromStream.IsCorrelationIdPresent());
            Assert.AreEqual(isMessageIdPresent, propertiesFromStream.IsMessageIdPresent());
        }

        [Test]
        public void TestProperties_ReplyTo([Values(null, "foo_1", "fanout://name/key")] string replyTo)
        {
            // Arrange
            var subject = new Framing.BasicProperties
            {
                // Act
                ReplyTo = replyTo,
            };

            // Assert
            bool isReplyToPresent = replyTo != null;
            PublicationAddress.TryParse(replyTo, out PublicationAddress result);
            string replyToAddress = result?.ToString();
            Assert.AreEqual(isReplyToPresent, subject.IsReplyToPresent());

            var writer = new Impl.ContentHeaderPropertyWriter(new byte[1024]);
            subject.WritePropertiesTo(ref writer);

            // Read from Stream
            var propertiesFromStream = new Framing.BasicProperties();
            var reader = new Impl.ContentHeaderPropertyReader(writer.Memory.Slice(0, writer.Offset));
            propertiesFromStream.ReadPropertiesFrom(ref reader);

            Assert.AreEqual(replyTo, propertiesFromStream.ReplyTo);
            Assert.AreEqual(isReplyToPresent, propertiesFromStream.IsReplyToPresent());
            Assert.AreEqual(replyToAddress, propertiesFromStream.ReplyToAddress?.ToString());
        }
    }
}
