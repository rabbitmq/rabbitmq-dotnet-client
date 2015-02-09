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

namespace RabbitMQ.Client.Content
{
    /// <summary>
    /// Interface for constructing messages binary-compatible with QPid's "StreamMessage" wire encoding.
    /// </summary>
    public interface IStreamMessageBuilder : IMessageBuilder
    {
        /// <summary>
        /// Writes a <see cref="bool"/> value into the message body being assembled.
        /// </summary>
        IStreamMessageBuilder WriteBool(bool value);

        /// <summary>
        /// Writes a <see cref="byte"/> value into the message body being assembled.
        /// </summary>
        IStreamMessageBuilder WriteByte(byte value);

        /// <summary>
        /// Writes a section of a byte array into the message body being assembled.
        /// </summary>
        IStreamMessageBuilder WriteBytes(byte[] source, int offset, int count);

        /// <summary>
        /// Writes a <see cref="byte"/> array into the message body being assembled.
        /// </summary>
        IStreamMessageBuilder WriteBytes(byte[] source);

        /// <summary>
        /// Writes a <see cref="char"/> value into the message body being assembled.
        /// </summary>
        IStreamMessageBuilder WriteChar(char value);

        /// <summary>
        /// Writes a <see cref="double"/> value into the message body being assembled.
        /// </summary>
        IStreamMessageBuilder WriteDouble(double value);

        /// <summary>
        /// Writes a <see cref="short"/> value into the message body being assembled.
        /// </summary>
        IStreamMessageBuilder WriteInt16(short value);

        /// <summary>
        /// Writes an <see cref="int"/> value into the message body being assembled.
        /// </summary>
        IStreamMessageBuilder WriteInt32(int value);

        /// <summary>
        /// Writes a <see cref="long"/> value into the message body being assembled.
        /// </summary>
        IStreamMessageBuilder WriteInt64(long value);

        /// <summary>
        /// Writes an <see cref="object"/> value into the message body being assembled.
        /// </summary>
        /// <remarks>
        /// The only permitted types are bool, int, short, byte, char,
        /// long, float, double, byte[] and string.
        /// </remarks>
        IStreamMessageBuilder WriteObject(object value);

        /// <summary>
        /// Writes objects using WriteObject(), one after the other. No length indicator is written.
        /// See also <see cref="IStreamMessageReader.ReadObjects"/>.
        /// </summary>
        IStreamMessageBuilder WriteObjects(params object[] values);

        /// <summary>
        /// Writes a <see cref="float"/> value into the message body being assembled.
        /// </summary>
        IStreamMessageBuilder WriteSingle(float value);

        /// <summary>
        /// Writes a <see cref="float"/>string value into the message body being assembled.
        /// </summary>
        IStreamMessageBuilder WriteString(string value);
    }
}