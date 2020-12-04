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
//  Copyright (c) 2011-2020 VMware, Inc. or its affiliates.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using NUnit.Framework;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestBasicProperties
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

            Span<byte> span = new byte[1024];
            int offset = subject.WritePropertiesTo(span);

            // Read from Stream
            var propertiesFromStream = new Framing.BasicProperties(span.Slice(0, offset));

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

            Span<byte> span = new byte[1024];
            int offset = subject.WritePropertiesTo(span);

            // Read from Stream
            var propertiesFromStream = new Framing.BasicProperties(span.Slice(0, offset));

            Assert.AreEqual(replyTo, propertiesFromStream.ReplyTo);
            Assert.AreEqual(isReplyToPresent, propertiesFromStream.IsReplyToPresent());
            Assert.AreEqual(replyToAddress, propertiesFromStream.ReplyToAddress?.ToString());
        }
    }
}
