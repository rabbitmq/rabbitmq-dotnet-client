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
    /// Interface for analyzing application messages.
    /// </summary>
    /// <remarks>
    /// Subinterfaces provide specialized data-reading methods. This
    /// base interface deals with the lowest common denominator:
    /// bytes, with no special encodings for higher-level objects.
    /// </remarks>
    public interface IMessageReader
    {
        /// <summary>
        /// Retrieve the message body, as a byte array.
        /// </summary>
        byte[] BodyBytes { get; }

        /// <summary>
        /// Retrieve the <see cref="Stream"/> being used to read from the message body.
        /// </summary>
        Stream BodyStream { get; }

        /// <summary>
        /// Retrieves the content header properties of the message being read. Is of type <seealso cref="IDictionary{TKey,TValue}"/>.
        /// </summary>
        IDictionary<string, object> Headers { get; }

        /// <summary>
        /// Read a single byte from the body stream, without encoding or interpretation.
        /// Returns -1 for end-of-stream.
        /// </summary>
        int RawRead();

        /// <summary>
        /// Read bytes from the body stream into a section of
        /// an existing byte array, without encoding or
        /// interpretation. Returns the number of bytes read from the
        /// body and written into the target array, which may be less
        /// than the number requested if the end-of-stream is reached.
        /// </summary>
        int RawRead(byte[] target, int offset, int length);
    }
}
