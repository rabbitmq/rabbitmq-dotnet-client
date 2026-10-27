// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------


using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Impl;
using Xunit;

namespace Test.Unit
{
    public class TestFieldValueNestingDepth
    {
        private const int Limit = WireFormatting.MaxFieldValueNestingDepth;
        private const int FarTooDeep = 100_000;

        [Theory]
        [InlineData('A')]
        [InlineData('F')]
        public void AFieldValueNestedToTheLimitIsRead_GHSA_qhmm(char kind)
        {
            byte[] value = NestedFieldValue(new string(kind, Limit + 1));
            WireFormatting.ReadFieldValue(value, out int bytesRead);
            Assert.Equal(value.Length, bytesRead);
        }

        [Theory]
        [InlineData('A')]
        [InlineData('F')]
        public void AFieldValueNestedPastTheLimitIsRejected_GHSA_qhmm(char kind)
        {
            byte[] value = NestedFieldValue(new string(kind, Limit + 2));
            Assert.Throws<SyntaxErrorException>(() => WireFormatting.ReadFieldValue(value, out _));
        }

        [Fact]
        public void TablesAndArraysCountTowardsOneLimit_GHSA_qhmm()
        {
            byte[] atLimit = NestedFieldValue(Alternating(Limit + 1));
            WireFormatting.ReadFieldValue(atLimit, out int bytesRead);
            Assert.Equal(atLimit.Length, bytesRead);

            byte[] pastLimit = NestedFieldValue(Alternating(Limit + 2));
            Assert.Throws<SyntaxErrorException>(() => WireFormatting.ReadFieldValue(pastLimit, out _));
        }

        [Theory]
        [InlineData('A')]
        [InlineData('F')]
        public void ATableCountsAsTheFirstLevel_GHSA_qhmm(char kind)
        {
            byte[] atLimit = Table(new string(kind, Limit));
            Assert.Equal(atLimit.Length, WireFormatting.ReadDictionary(atLimit, out _));

            byte[] pastLimit = Table(new string(kind, Limit + 1));
            Assert.Throws<SyntaxErrorException>(() => WireFormatting.ReadDictionary(pastLimit, out _));
        }

        [Fact]
        public void AnXDeathHeaderIsRead_GHSA_qhmm()
        {
            IDictionary<string, object> headers = new Dictionary<string, object>
            {
                ["x-death"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["count"] = 1L,
                        ["reason"] = "rejected",
                        ["queue"] = "q",
                        ["exchange"] = "",
                        ["routing-keys"] = new List<object> { "q" },
                    }
                }
            };
            byte[] table = new byte[WireFormatting.GetTableByteCount(headers)];
            WireFormatting.WriteTable(ref table.GetStart(), headers);

            Assert.Equal(table.Length, WireFormatting.ReadDictionary(table, out var read));
            Assert.True(read.ContainsKey("x-death"));
        }

        [Theory]
        [InlineData('A')]
        [InlineData('F')]
        public void TheWriterAcceptsWhatTheReaderAccepts_GHSA_qhmm(char kind)
        {
            IDictionary<string, object> atLimit = new Dictionary<string, object> { ["k"] = NestedObject(kind, Limit) };
            byte[] bytes = new byte[WireFormatting.GetTableByteCount(atLimit)];
            WireFormatting.WriteTable(ref bytes.GetStart(), atLimit);
            Assert.Equal(bytes.Length, WireFormatting.ReadDictionary(bytes, out _));

            IDictionary<string, object> pastLimit = new Dictionary<string, object> { ["k"] = NestedObject(kind, Limit + 1) };
            Assert.Throws<WireFormattingException>(() => WireFormatting.GetTableByteCount(pastLimit));
        }

        // Without the limit, this overflows the stack and terminates the test host.
        [Fact]
        public void ATableContainingItselfIsRejected_GHSA_qhmm()
        {
            IDictionary<string, object> table = new Dictionary<string, object>();
            table["self"] = table;
            Assert.Throws<WireFormattingException>(() => WireFormatting.GetTableByteCount(table));
        }

        // Without the limit, these overflow the stack and terminate the test host.
        [Theory]
        [InlineData('A')]
        [InlineData('F')]
        public void DeeplyNestedServerPropertiesAreRejected_GHSA_qhmm(char kind)
        {
            byte[] serverProperties = Table(new string(kind, FarTooDeep));
            byte[] connectionStart = new byte[2 + serverProperties.Length + 8];
            connectionStart[0] = 0;
            connectionStart[1] = 9;
            serverProperties.CopyTo(connectionStart, 2);
            Assert.Throws<SyntaxErrorException>(() => new ConnectionStart(connectionStart));
        }

        [Fact]
        public void DeeplyNestedMessageHeadersAreRejected_GHSA_qhmm()
        {
            byte[] headers = Table(new string('A', FarTooDeep));
            byte[] properties = new byte[2 + headers.Length];
            properties[0] = 1 << BasicProperties.HeaderBit;
            headers.CopyTo(properties, 2);
            Assert.Throws<SyntaxErrorException>(() => new ReadOnlyBasicProperties(properties));
        }

        private static object NestedObject(char kind, int containers)
        {
            object value = 42;
            for (int i = 0; i < containers; i++)
            {
                value = kind == 'A'
                    ? new List<object> { value }
                    : new Dictionary<string, object> { [""] = value };
            }
            return value;
        }

        private static string Alternating(int count)
        {
            var sb = new StringBuilder(count);
            for (int i = 0; i < count; i++)
            {
                sb.Append(i % 2 == 0 ? 'F' : 'A');
            }
            return sb.ToString();
        }

        private static readonly byte[] s_leaf = { (byte)'I', 0, 0, 0, 42 };

        // kinds[0] is the outermost container; each table holds its child under an empty key,
        // and the innermost container holds an int.
        private static byte[] NestedFieldValue(string kinds)
        {
            int size = s_leaf.Length;
            foreach (char kind in kinds)
            {
                size += kind == 'F' ? 6 : 5;
            }

            byte[] buffer = new byte[size];
            int pos = size - s_leaf.Length;
            s_leaf.CopyTo(buffer, pos);
            for (int i = kinds.Length - 1; i >= 0; i--)
            {
                if (kinds[i] == 'F')
                {
                    buffer[--pos] = 0;
                }
                int contentLength = size - pos;
                pos -= 4;
                BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(pos), (uint)contentLength);
                buffer[--pos] = (byte)kinds[i];
            }
            return buffer;
        }

        // A table holding NestedFieldValue(kinds) under an empty key.
        private static byte[] Table(string kinds)
            => NestedFieldValue("F" + kinds).AsSpan(1).ToArray();
    }
}
