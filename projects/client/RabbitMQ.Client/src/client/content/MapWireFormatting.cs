// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2014 GoPivotal, Inc.
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
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Content
{
    ///<summary>Internal support class for use in reading and writing
    ///information binary-compatible with QPid's "MapMessage" wire
    ///encoding.</summary>
    ///<exception cref="ProtocolViolationException"/>
    public class MapWireFormatting
    {
        public static IDictionary<string, object> ReadMap(NetworkBinaryReader reader)
        {
            int entryCount = BytesWireFormatting.ReadInt32(reader);
            if (entryCount < 0)
            {
                string message = string.Format("Invalid (negative) entryCount: {0}", entryCount);
                throw new ProtocolViolationException(message);
            }

            IDictionary<string, object> table = new Dictionary<string, object>(entryCount);
            for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
            {
                string key = StreamWireFormatting.ReadUntypedString(reader);
                object value = StreamWireFormatting.ReadObject(reader);
                table[key] = value;
            }

            return table;
        }

        /// <param name="writer">Type is <seealso cref="RabbitMQ.Util.NetworkBinaryWriter"/>.</param>
        /// <param name="table">Type is <seealso cref="System.Collections.Generic.IDictionary{TKey,TValue}"/>.</param>
        public static void WriteMap(NetworkBinaryWriter writer, IDictionary<string, object> table)
        {
            int entryCount = table.Count;
            BytesWireFormatting.WriteInt32(writer, entryCount);

            foreach (KeyValuePair<string, object> entry in table)
            {
                StreamWireFormatting.WriteUntypedString(writer, entry.Key);
                StreamWireFormatting.WriteObject(writer, entry.Value);
            }
        }
    }
}
