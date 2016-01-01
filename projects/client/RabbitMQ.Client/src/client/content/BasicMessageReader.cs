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
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Content
{
    /// <summary>
    /// Framework for analyzing various types of AMQP Basic-class application messages.
    /// </summary>
    public class BasicMessageReader : IMessageReader
    {
        protected NetworkBinaryReader m_reader;
        protected MemoryStream m_stream;

        /// <summary>
        /// Construct an instance ready for reading.
        /// </summary>
        public BasicMessageReader(IBasicProperties properties, byte[] body)
        {
            Properties = properties;
            BodyBytes = body;
        }

        /// <summary>
        /// Retrieve the <see cref="IBasicProperties"/> associated with this instance.
        /// </summary>
        public IBasicProperties Properties { get; protected set; }

        /// <summary>
        /// Retrieve this instance's NetworkBinaryReader reading from <see cref="BodyBytes"/>.
        /// </summary>
        /// <remarks>
        /// If no NetworkBinaryReader instance exists, one is created,
        /// pointing at the beginning of the body. If one already
        /// exists, the existing instance is returned. The instance is
        /// not reset.
        /// </remarks>
        public NetworkBinaryReader Reader
        {
            get
            {
                if (m_reader == null)
                {
                    m_reader = new NetworkBinaryReader(BodyStream);
                }
                return m_reader;
            }
        }

        /// <summary>
        /// Retrieve the message body, as a byte array.
        /// </summary>
        public byte[] BodyBytes { get; protected set; }

        /// <summary>
        /// Retrieve the <see cref="Stream"/> being used to read from the message body.
        /// </summary>
        public Stream BodyStream
        {
            get
            {
                if (m_stream == null)
                {
                    m_stream = new MemoryStream(BodyBytes);
                }
                return m_stream;
            }
        }

        /// <summary>
        /// Retrieves the content header properties of the message being read. Is of type <seealso cref="IDictionary{TKey,TValue}"/>.
        /// </summary>
        public IDictionary<string, object> Headers
        {
            get
            {
                if (Properties.Headers == null)
                {
                    Properties.Headers = new Dictionary<string, object>();
                }
                return Properties.Headers;
            }
        }

        /// <summary>
        /// Read a single byte from the body stream, without encoding or interpretation.
        /// Returns -1 for end-of-stream.
        /// </summary>
        public int RawRead()
        {
            return BodyStream.ReadByte();
        }

        /// <summary>
        /// Read bytes from the body stream into a section of
        /// an existing byte array, without encoding or
        /// interpretation. Returns the number of bytes read from the
        /// body and written into the target array, which may be less
        /// than the number requested if the end-of-stream is reached.
        /// </summary>
        public int RawRead(byte[] target, int offset, int length)
        {
            return BodyStream.Read(target, offset, length);
        }
    }
}
