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

namespace RabbitMQ.Client.Content
{
    /// <summary>
    /// Analyzes messages binary-compatible with QPid's "BytesMessage" wire encoding.
    /// </summary>
    public interface IBytesMessageReader : IMessageReader
    {
        /// <summary>
        /// Reads a given number ("count") of bytes from the message body,
        /// placing them into "target", starting at "offset".
        /// </summary>
        int Read(byte[] target, int offset, int count);

        /// <summary>
        /// Reads a <see cref="byte"/> from the message body.
        /// </summary>
        byte ReadByte();

        /// <summary>
        /// Reads a given number of bytes from the message body.
        /// </summary>
        byte[] ReadBytes(int count);

        /// <summary>
        /// Reads a <see cref="char"/> from the message body.
        /// </summary>
        char ReadChar();

        /// <summary>
        /// Reads a <see cref="double"/> from the message body.
        /// </summary>
        double ReadDouble();

        /// <summary>
        /// Reads a <see cref="short"/> from the message body.
        /// </summary>
        short ReadInt16();

        /// <summary>
        /// Reads an <see cref="int"/> from the message body.
        /// </summary>
        int ReadInt32();

        /// <summary>
        /// Reads a <see cref="long"/> from the message body.
        /// </summary>
        long ReadInt64();

        /// <summary>
        /// Reads a <see cref="float"/> from the message body.
        /// </summary>
        float ReadSingle();

        /// <summary>
        /// Reads a <see cref="string"/> from the message body.
        /// </summary>
        string ReadString();
    }
}
