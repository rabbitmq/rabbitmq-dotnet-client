// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2016 Pivotal Software, Inc.
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
//  at http://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

namespace RabbitMQ.Client.Content
{
    /// <summary>
    /// Constructs AMQP Basic-class messages binary-compatible with QPid's "StreamMessage" wire encoding.
    /// </summary>
    public class StreamMessageBuilder : BasicMessageBuilder, IStreamMessageBuilder
    {
        /// <summary>
        /// MIME type associated with QPid StreamMessages.
        /// </summary>
        public const string MimeType = "jms/stream-message";

        /// <summary>
        /// Construct an instance for writing. See <see cref="BasicMessageBuilder"/>.
        /// </summary>
        public StreamMessageBuilder(IModel model)
            : base(model)
        {
        }

        /// <summary>
        /// Construct an instance for writing. See <see cref="BasicMessageBuilder"/>.
        /// </summary>
        public StreamMessageBuilder(IModel model, int initialAccumulatorSize)
            : base(model, initialAccumulatorSize)
        {
        }

        /// <summary>
        /// Returns the default MIME content type for messages this instance constructs,
        /// or null if none is available or relevant.
        /// </summary>
        public override string GetDefaultContentType()
        {
            return MimeType;
        }

        /// <summary>
        /// Writes a <see cref="bool"/> value into the message body being assembled.
        /// </summary>
        public IStreamMessageBuilder WriteBool(bool value)
        {
            StreamWireFormatting.WriteBool(Writer, value);
            return this;
        }

        /// <summary>
        /// Writes a <see cref="byte"/> value into the message body being assembled.
        /// </summary>
        public IStreamMessageBuilder WriteByte(byte value)
        {
            StreamWireFormatting.WriteByte(Writer, value);
            return this;
        }

        /// <summary>
        /// Writes a section of a byte array into the message body being assembled.
        /// </summary>
        public IStreamMessageBuilder WriteBytes(byte[] source, int offset, int count)
        {
            StreamWireFormatting.WriteBytes(Writer, source, offset, count);
            return this;
        }

        /// <summary>
        /// Writes a <see cref="byte"/> array into the message body being assembled.
        /// </summary>
        public IStreamMessageBuilder WriteBytes(byte[] source)
        {
            StreamWireFormatting.WriteBytes(Writer, source);
            return this;
        }

        /// <summary>
        /// Writes a <see cref="char"/> value into the message body being assembled.
        /// </summary>
        public IStreamMessageBuilder WriteChar(char value)
        {
            StreamWireFormatting.WriteChar(Writer, value);
            return this;
        }

        /// <summary>
        /// Writes a <see cref="double"/> value into the message body being assembled.
        /// </summary>
        public IStreamMessageBuilder WriteDouble(double value)
        {
            StreamWireFormatting.WriteDouble(Writer, value);
            return this;
        }

        /// <summary>
        /// Writes a <see cref="short"/> value into the message body being assembled.
        /// </summary>
        public IStreamMessageBuilder WriteInt16(short value)
        {
            StreamWireFormatting.WriteInt16(Writer, value);
            return this;
        }

        /// <summary>
        /// Writes an <see cref="int"/> value into the message body being assembled.
        /// </summary>
        public IStreamMessageBuilder WriteInt32(int value)
        {
            StreamWireFormatting.WriteInt32(Writer, value);
            return this;
        }

        /// <summary>
        /// Writes a <see cref="long"/> value into the message body being assembled.
        /// </summary>
        public IStreamMessageBuilder WriteInt64(long value)
        {
            StreamWireFormatting.WriteInt64(Writer, value);
            return this;
        }

        /// <summary>
        /// Writes an <see cref="object"/> value into the message body being assembled.
        /// </summary>
        /// <remarks>
        /// The only permitted types are bool, int, short, byte, char,
        /// long, float, double, byte[] and string.
        /// </remarks>
        public IStreamMessageBuilder WriteObject(object value)
        {
            StreamWireFormatting.WriteObject(Writer, value);
            return this;
        }

        /// <summary>
        /// Writes objects using WriteObject(), one after the other. No length indicator is written.
        /// See also <see cref="IStreamMessageReader.ReadObjects"/>.
        /// </summary>
        public IStreamMessageBuilder WriteObjects(params object[] values)
        {
            foreach (object val in values)
            {
                WriteObject(val);
            }
            return this;
        }

        /// <summary>
        /// Writes a <see cref="float"/> value into the message body being assembled.
        /// </summary>
        public IStreamMessageBuilder WriteSingle(float value)
        {
            StreamWireFormatting.WriteSingle(Writer, value);
            return this;
        }

        /// <summary>
        /// Writes a <see cref="float"/>string value into the message body being assembled.
        /// </summary>
        public IStreamMessageBuilder WriteString(string value)
        {
            StreamWireFormatting.WriteString(Writer, value);
            return this;
        }
    }
}
