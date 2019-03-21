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

using System.Collections.Generic;
using System.IO;

namespace RabbitMQ.Client.Content
{
    /// <summary>
    /// Analyzes AMQP Basic-class messages binary-compatible with QPid's "StreamMessage" wire encoding.
    /// </summary>
    public class StreamMessageReader : BasicMessageReader, IStreamMessageReader
    {
        /// <summary>
        /// MIME type associated with QPid StreamMessages.
        /// </summary>
        public static readonly string MimeType = StreamMessageBuilder.MimeType;


        /// <summary>
        /// Construct an instance for reading. See <see cref="BasicMessageReader"/>.
        /// </summary>
        public StreamMessageReader(IBasicProperties properties, byte[] payload)
            : base(properties, payload)
        {
        }

        /// <summary>
        /// Reads a <see cref="bool"/> from the message body.
        /// </summary>
        public bool ReadBool()
        {
            return StreamWireFormatting.ReadBool(Reader);
        }

        /// <summary>
        /// Reads a <see cref="byte"/> from the message body.
        /// </summary>
        public byte ReadByte()
        {
            return StreamWireFormatting.ReadByte(Reader);
        }

        /// <summary>
        /// Reads a <see cref="byte"/> array from the message body.
        /// The body contains information about the size of the array to read.
        /// </summary>
        public byte[] ReadBytes()
        {
            return StreamWireFormatting.ReadBytes(Reader);
        }

        /// <summary>
        /// Reads a <see cref="char"/> from the message body.
        /// </summary>
        public char ReadChar()
        {
            return StreamWireFormatting.ReadChar(Reader);
        }

        /// <summary>
        /// Reads a <see cref="double"/> from the message body.
        /// </summary>
        public double ReadDouble()
        {
            return StreamWireFormatting.ReadDouble(Reader);
        }

        /// <summary>
        /// Reads a <see cref="short"/> from the message body.
        /// </summary>
        public short ReadInt16()
        {
            return StreamWireFormatting.ReadInt16(Reader);
        }

        /// <summary>
        /// Reads an <see cref="int"/> from the message body.
        /// </summary>
        public int ReadInt32()
        {
            return StreamWireFormatting.ReadInt32(Reader);
        }

        /// <summary>
        /// Reads a <see cref="long"/> from the message body.
        /// </summary>
        public long ReadInt64()
        {
            return StreamWireFormatting.ReadInt64(Reader);
        }

        /// <summary>
        /// Reads an <see cref="object"/> from the message body.
        /// </summary>
        public object ReadObject()
        {
            return StreamWireFormatting.ReadObject(Reader);
        }

        /// <summary>
        /// Reads <see cref="object"/> array from the message body until the end-of-stream is reached.
        /// </summary>
        public object[] ReadObjects()
        {
            var result = new List<object>();
            while (true)
            {
                try
                {
                    object val = ReadObject();
                    result.Add(val);
                }
                catch (EndOfStreamException)
                {
                    break;
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// Reads a <see cref="float"/> from the message body.
        /// </summary>
        public float ReadSingle()
        {
            return StreamWireFormatting.ReadSingle(Reader);
        }

        /// <summary>
        /// Reads a <see cref="string"/> from the message body.
        /// </summary>
        public string ReadString()
        {
            return StreamWireFormatting.ReadString(Reader);
        }
    }
}
