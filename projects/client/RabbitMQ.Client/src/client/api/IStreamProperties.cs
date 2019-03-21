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

namespace RabbitMQ.Client
{
    /// <summary>
    /// Common AMQP Stream content-class headers interface,
    ///spanning the union of the functionality offered by versions 0-8, 0-8qpid, 0-9 and 0-9-1 of AMQP.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The specification code generator provides
    /// protocol-version-specific implementations of this interface. To
    /// obtain an implementation of this interface in a
    /// protocol-version-neutral way, use IModel.CreateStreamProperties().
    /// </para>
    /// <para>
    /// Each property is readable, writable and clearable: a cleared
    /// property will not be transmitted over the wire. Properties on a fresh instance are clear by default.
    /// </para>
    /// </remarks>
    public interface IStreamProperties : IContentHeader
    {
        /// <summary>
        /// MIME content encoding.
        /// </summary>
        string ContentEncoding { get; set; }

        /// <summary>
        /// MIME content type.
        /// </summary>
        string ContentType { get; set; }

        /// <summary>
        /// Message header field table.
        /// </summary>
        IDictionary<string, object> Headers { get; set; }

        /// <summary>
        /// Message priority, 0 to 9.
        /// </summary>
        byte Priority { get; set; }

        /// <summary>
        /// Message timestamp.
        /// </summary>
        AmqpTimestamp Timestamp { get; set; }

        /// <summary>
        /// Clear the <see cref="ContentEncoding"/> property.
        /// </summary>
        void ClearContentEncoding();

        /// <summary>
        /// Clear the <see cref="ContentType"/> property.
        /// </summary>
        void ClearContentType();

        /// <summary>
        /// Clear the <see cref="Headers"/> property.
        /// </summary>
        void ClearHeaders();

        /// <summary>
        /// Clear the <see cref="Priority"/> property.
        /// </summary>
        void ClearPriority();

        /// <summary>
        /// Clear the <see cref="Timestamp"/> property.
        /// </summary>
        void ClearTimestamp();

        /// <summary>
        /// Returns true if the <see cref="ContentEncoding"/> property is present.
        /// </summary>
        bool IsContentEncodingPresent();

        /// <summary>
        /// Returns true if the <see cref="ContentType"/> property is present.
        /// </summary>
        bool IsContentTypePresent();

        /// <summary>
        /// Returns true if the <see cref="Headers"/> property is present.
        /// </summary>
        bool IsHeadersPresent();

        /// <summary>
        /// Returns true if the <see cref="Priority"/> property is present.
        /// </summary>
        bool IsPriorityPresent();

        /// <summary>
        /// Returns true if the <see cref="Timestamp"/> property is present.
        /// </summary>
        bool IsTimestampPresent();
    }
}
