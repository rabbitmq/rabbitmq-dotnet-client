﻿// This source code is dual-licensed under the Apache License, version
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RabbitMQ.Client.Impl
{
    public static class ExtensionMethods
    {
        private static readonly ArraySegment<byte> m_emptyByteArray = new ArraySegment<byte>(new byte[0], 0, 0);

        /// <summary>
        /// Returns a random item from the list.
        /// </summary>
        /// <typeparam name="T">Element item type</typeparam>
        /// <param name="list">Input list</param>
        /// <returns></returns>
        public static T RandomItem<T>(this IList<T> list)
        {
            var n = list.Count;
            if (n == 0)
            {
                return default(T);
            }

            var hashCode = Math.Abs(Guid.NewGuid().GetHashCode());
            return list.ElementAt<T>(hashCode % n);
        }

        internal static ArraySegment<byte> GetBufferSegment(this byte[] data)
        {
            if (data == null)
            {
                return m_emptyByteArray;
            }

            return new ArraySegment<byte>(data, 0, data.Length);
        }

        internal static ArraySegment<byte> GetBufferSegment(this byte[] data, int offset, int count)
        {
            if (data == null)
            {
                return m_emptyByteArray;
            }

            return new ArraySegment<byte>(data, offset, count);
        }

        internal static ArraySegment<byte> GetBufferSegment(this MemoryStream ms)
        {
#if CORECLR15
            var payload = ms.ToArray();
            return new ArraySegment<byte>(payload, 0, payload.Length);
#else
            var buffer = ms.GetBuffer();
            return new ArraySegment<byte>(buffer, 0, (int)ms.Position);
#endif
        }
    }
}
