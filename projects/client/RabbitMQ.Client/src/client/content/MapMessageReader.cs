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

namespace RabbitMQ.Client.Content
{
    /// <summary>
    /// Analyzes AMQP Basic-class messages binary-compatible with QPid's "MapMessage" wire encoding.
    /// </summary>
    public class MapMessageReader : BasicMessageReader, IMapMessageReader
    {
        /// <summary>
        /// MIME type associated with QPid MapMessages.
        /// </summary>
        public const string MimeType = MapMessageBuilder.MimeType;

        protected IDictionary<string, object> m_table;

        /// <summary>
        /// Construct an instance for reading. See <see cref="BasicMessageReader"/>.
        /// </summary>
        public MapMessageReader(IBasicProperties properties, byte[] payload)
            : base(properties, payload)
        {
        }

        ///<summary>
        /// Parses the message body into an <see cref="IDictionary{TKey,TValue}"/> instance.
        /// </summary>
        /// <exception cref="System.Net.ProtocolViolationException"/>.
        public IDictionary<string, object> Body
        {
            get
            {
                if (m_table == null)
                {
                    m_table = MapWireFormatting.ReadMap(Reader);
                }
                return m_table;
            }
        }
    }
}
