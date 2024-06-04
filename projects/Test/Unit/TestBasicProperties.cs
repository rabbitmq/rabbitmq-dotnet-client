// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using RabbitMQ.Client;
using Xunit;

namespace Test.Unit
{
    public class TestBasicProperties
    {
        [Fact]
        public void TestPersistentPropertyChangesDeliveryMode_PersistentTrueDelivery2()
        {
            // Arrange
            var subject = new BasicProperties
            {
                // Act
                Persistent = true
            };

            // Assert
            Assert.Equal(DeliveryModes.Persistent, subject.DeliveryMode);
            Assert.True(subject.Persistent);

            Span<byte> span = new byte[1024];
            int offset = ((IAmqpWriteable)subject).WriteTo(span);

            // Read from Stream
            var propertiesFromStream = new ReadOnlyBasicProperties(span.Slice(0, offset));

            Assert.Equal(DeliveryModes.Persistent, propertiesFromStream.DeliveryMode);
            Assert.True(propertiesFromStream.Persistent);

            // Verify Basic Properties
            var basicProperties = new BasicProperties(propertiesFromStream);

            Assert.Equal(DeliveryModes.Persistent, basicProperties.DeliveryMode);
            Assert.True(basicProperties.Persistent);
        }

        [Fact]
        public void TestPersistentPropertyChangesDeliveryMode_PersistentFalseDelivery1()
        {
            // Arrange
            var subject = new BasicProperties
            {

                // Act
                Persistent = false
            };

            // Assert
            Assert.Equal(DeliveryModes.Transient, subject.DeliveryMode);
            Assert.False(subject.Persistent);

            Span<byte> span = new byte[1024];
            int offset = ((IAmqpWriteable)subject).WriteTo(span);

            // Read from Stream
            var propertiesFromStream = new ReadOnlyBasicProperties(span.Slice(0, offset));

            Assert.Equal(DeliveryModes.Transient, propertiesFromStream.DeliveryMode);
            Assert.False(propertiesFromStream.Persistent);

            // Verify Basic Properties
            var basicProperties = new BasicProperties(propertiesFromStream);

            Assert.Equal(DeliveryModes.Transient, basicProperties.DeliveryMode);
            Assert.False(basicProperties.Persistent);
        }

        [Theory]
        [InlineData(null, null, null)]
        [InlineData(null, null, "7D221C7E-1788-4D11-9CA5-AC41425047CF")]
        [InlineData(null, "732E39DC-AF56-46E8-B8A9-079C4B991B2E", null)]
        [InlineData(null, "732E39DC-AF56-46E8-B8A9-079C4B991B2E", "7D221C7E-1788-4D11-9CA5-AC41425047CF")]
        [InlineData("cluster1", null, null)]
        [InlineData("cluster1", null, "7D221C7E-1788-4D11-9CA5-AC41425047CF")]
        [InlineData("cluster1", "732E39DC-AF56-46E8-B8A9-079C4B991B2E", null)]
        [InlineData("cluster1", "732E39DC-AF56-46E8-B8A9-079C4B991B2E", "7D221C7E-1788-4D11-9CA5-AC41425047CF")]
        public void TestNullableProperties_CanWrite(string clusterId, string correlationId, string messageId)
        {
            // Arrange
            var subject = new BasicProperties
            {
                // Act
                ClusterId = clusterId,
                CorrelationId = correlationId,
                MessageId = messageId
            };

            // Assert
            bool isClusterIdPresent = clusterId != null;
            Assert.Equal(isClusterIdPresent, subject.IsClusterIdPresent());

            bool isCorrelationIdPresent = correlationId != null;
            Assert.Equal(isCorrelationIdPresent, subject.IsCorrelationIdPresent());

            bool isMessageIdPresent = messageId != null;
            Assert.Equal(isMessageIdPresent, subject.IsMessageIdPresent());

            Span<byte> span = new byte[1024];
            int offset = ((IAmqpWriteable)subject).WriteTo(span);

            // Read from Stream
            var propertiesFromStream = new ReadOnlyBasicProperties(span.Slice(0, offset));

            Assert.Equal(clusterId, propertiesFromStream.ClusterId);
            Assert.Equal(correlationId, propertiesFromStream.CorrelationId);
            Assert.Equal(messageId, propertiesFromStream.MessageId);
            Assert.Equal(isClusterIdPresent, propertiesFromStream.IsClusterIdPresent());
            Assert.Equal(isCorrelationIdPresent, propertiesFromStream.IsCorrelationIdPresent());
            Assert.Equal(isMessageIdPresent, propertiesFromStream.IsMessageIdPresent());

            // Verify Basic Properties
            var basicProperties = new BasicProperties(propertiesFromStream);

            Assert.Equal(clusterId, basicProperties.ClusterId);
            Assert.Equal(correlationId, basicProperties.CorrelationId);
            Assert.Equal(messageId, basicProperties.MessageId);
            Assert.Equal(isClusterIdPresent, basicProperties.IsClusterIdPresent());
            Assert.Equal(isCorrelationIdPresent, basicProperties.IsCorrelationIdPresent());
            Assert.Equal(isMessageIdPresent, basicProperties.IsMessageIdPresent());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("foo_1")]
        [InlineData("fanout://name/key")]
        public void TestProperties_ReplyTo(string replyTo)
        {
            // Arrange
            var subject = new BasicProperties
            {
                // Act
                ReplyTo = replyTo,
            };

            // Assert
            bool isReplyToPresent = replyTo != null;
            PublicationAddress.TryParse(replyTo, out PublicationAddress result);
            string replyToAddress = result?.ToString();
            Assert.Equal(isReplyToPresent, subject.IsReplyToPresent());

            Span<byte> span = new byte[1024];
            int offset = ((IAmqpWriteable)subject).WriteTo(span);

            // Read from Stream
            var propertiesFromStream = new ReadOnlyBasicProperties(span.Slice(0, offset));

            Assert.Equal(replyTo, propertiesFromStream.ReplyTo);
            Assert.Equal(isReplyToPresent, propertiesFromStream.IsReplyToPresent());
            Assert.Equal(replyToAddress, propertiesFromStream.ReplyToAddress?.ToString());

            // Verify Basic Properties
            var basicProperties = new BasicProperties(propertiesFromStream);

            Assert.Equal(replyTo, basicProperties.ReplyTo);
            Assert.Equal(isReplyToPresent, basicProperties.IsReplyToPresent());
            Assert.Equal(replyToAddress, basicProperties.ReplyToAddress?.ToString());
        }
    }
}
