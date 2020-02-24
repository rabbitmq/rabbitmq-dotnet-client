using System;
using System.Buffers;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Util
{

    public class FrameContentReader
    {
        public int Type { get; private set; }
        public int Channel { get; private set; }
        public int PayloadSize { get; private set; }
        public byte[] Payload { get; private set; }

        private ReadState _readState;
        private int _remainingBytes;

        public bool TryParseMessage(ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined)
        {
            var reader = new SequenceReader<byte>(input);

            if (_readState == ReadState.ReadHeader)
            {
                // The header consists of
                //
                // [1 byte] [2 bytes (ushort)] [4 bytes (uint)]
                // [type]   [channel]          [payloadSize]

                if (!reader.TryRead(out byte type))
                {
                    return false;
                }
                Type = type;

                if (Type == 'A')
                {
                    HandleWrongHeader(ref reader);
                }

                if (!reader.TryReadBigEndian(out short channel))
                {
                    return false;
                }
                Channel = channel;

                if (!reader.TryReadBigEndian(out int payloadSize))
                {
                    return false;
                }

                PayloadSize = payloadSize;
                consumed = reader.Position;
                examined = reader.Position;
                _remainingBytes = PayloadSize;

                if (PayloadSize == 0)
                {
                    _readState = ReadState.ReadEndMarker;

                }
                else
                {
                    _readState = ReadState.ReadPayload;
                    Payload = ArrayPool<byte>.Shared.Rent(PayloadSize);
                }
            }

            if (_readState == ReadState.ReadPayload)
            {
                int readCount = Math.Min(_remainingBytes, (int)reader.Remaining);
                int offset = PayloadSize - _remainingBytes;
                Span<byte> payloadSpan = new Span<byte>(Payload, offset, readCount);
                if (reader.TryCopyTo(payloadSpan))
                {
                    reader.Advance(readCount);
                    _remainingBytes -= readCount;
                }

                consumed = reader.Position;
                examined = reader.Position;

                if (_remainingBytes == 0)
                {
                    _readState = ReadState.ReadEndMarker;
                }
            }

            if (_readState == ReadState.ReadEndMarker)
            {
                if (reader.TryRead(out byte endMarker))
                {
                    if (endMarker != Constants.FrameEnd)
                    {
                        ThrowHelper.InvalidFrameEndMarker(endMarker);
                    }

                    _readState = ReadState.Complete;
                    consumed = reader.Position;
                    examined = reader.Position;
                    return true;
                }
            }

            return false;
        }

        private void HandleWrongHeader(ref SequenceReader<byte> reader)
        {
            Span<byte> span = ArrayPool<byte>.Shared.Rent(7);
            if (reader.TryCopyTo(span))
            {
                if (span[0] != 'M' || span[1] != 'Q' || span[2] != 'P')
                {
                    ThrowHelper.InvalidProtocolHeader();
                }

                int transportHigh = span[3];
                int transportLow = span[4];
                int serverMajor = span[5];
                int serverMinor = span[6];
                ThrowHelper.PacketNotRecognized(transportHigh, transportLow, serverMajor, serverMinor);
            }
            ThrowHelper.InvalidProtocolHeader();
        }

        public void Reset()
        {
            _readState = ReadState.ReadHeader;
            Type = 0;
        }

        private enum ReadState
        {
            ReadHeader, ReadPayload, ReadEndMarker, Complete
        }
    }
}
