﻿// This source code is dual-licensed under the Apache License, version
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

namespace RabbitMQ.Client
{
    /// <summary>Wrapper for a byte[]. May appear as values read from
    ///and written to AMQP field tables.</summary>
    /// <remarks>
    /// <para>
    /// The sole reason for the existence of this class is to permit
    /// encoding of byte[] as 'x' in AMQP field tables, an extension
    /// to the specification that is part of the tentative JMS mapping
    /// implemented by QPid.
    /// </para>
    /// <para>
    /// Instances of this object may be found as values held in
    /// IDictionary instances returned from
    /// RabbitMQ.Client.Impl.WireFormatting.ReadTable, e.g. as part of
    /// IBasicProperties.Headers tables. Likewise, instances may be
    /// set as values in an IDictionary table to be encoded by
    /// RabbitMQ.Client.Impl.WireFormatting.WriteTable.
    /// </para>
    /// <para>
    /// When an instance of this class is encoded/decoded, the type
    /// tag 'x' is used in the on-the-wire representation. The AMQP
    /// standard type tag 'S' is decoded to a raw byte[], and a raw
    /// byte[] is encoded as 'S'. Instances of System.String are
    /// converted to a UTF-8 binary representation, and then encoded
    /// using tag 'S'. In order to force the use of tag 'x', instances
    /// of this class must be used.
    /// </para>
    /// </remarks>
    public class BinaryTableValue
    {
        /// <summary>
        /// Creates a new instance of the <see cref="BinaryTableValue"/> with an empty array for its Bytes property.
        /// </summary>
        public BinaryTableValue() : this(Array.Empty<byte>())
        {
        }


        /// <summary>
        /// Creates a new instance of the <see cref="BinaryTableValue"/>.
        /// </summary>
        /// <param name="bytes">The wrapped byte array, as decoded or as to be encoded.</param>
        public BinaryTableValue(byte[] bytes)
        {
            Bytes = bytes;
        }

        /// <summary>
        /// The wrapped byte array, as decoded or as to be encoded.
        /// </summary>
        public byte[] Bytes { get; set; }
    }
}
