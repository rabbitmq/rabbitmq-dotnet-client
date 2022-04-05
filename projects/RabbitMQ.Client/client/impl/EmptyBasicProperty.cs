using System;
using System.Collections.Generic;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.client.impl
{
#nullable enable
    internal readonly struct EmptyBasicProperty : IReadOnlyBasicProperties, IAmqpHeader
    {
        internal static EmptyBasicProperty Empty;

        ushort IAmqpHeader.ProtocolClassId => ClassConstants.Basic;

        int IAmqpWriteable.WriteTo(Span<byte> span)
        {
            return WireFormatting.WriteShort(ref span.GetStart(), 0);
        }

        int IAmqpWriteable.GetRequiredBufferSize()
        {
            return 2; // number of presence fields (14) in 2 bytes blocks
        }

        public string? AppId => default;
        public string? ClusterId => default;
        public string? ContentEncoding => default;
        public string? ContentType => default;
        public string? CorrelationId => default;
        public DeliveryModes DeliveryMode => default;
        public string? Expiration => default;
        public IDictionary<string, object?>? Headers => default;
        public string? MessageId => default;
        public bool Persistent => default;
        public byte Priority => default;
        public string? ReplyTo => default;
        public PublicationAddress? ReplyToAddress => default;
        public AmqpTimestamp Timestamp => default;
        public string? Type => default;
        public string? UserId => default;

        public bool IsAppIdPresent() => false;
        public bool IsClusterIdPresent() => false;
        public bool IsContentEncodingPresent() => false;
        public bool IsContentTypePresent() => false;
        public bool IsCorrelationIdPresent() => false;
        public bool IsDeliveryModePresent() => false;
        public bool IsExpirationPresent() => false;
        public bool IsHeadersPresent() => false;
        public bool IsMessageIdPresent() => false;
        public bool IsPriorityPresent() => false;
        public bool IsReplyToPresent() => false;
        public bool IsTimestampPresent() => false;
        public bool IsTypePresent() => false;
        public bool IsUserIdPresent() => false;
    }
}
