// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2015 Pivotal Software, Inc.
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
//  Copyright (c) 2007-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System.Collections.Generic;

namespace RabbitMQ.Client.Content
{
    /// <summary>
    /// Constructs AMQP Basic-class messages binary-compatible with QPid's "MapMessage" wire encoding.
    /// </summary>
    public class MapMessageBuilder : BasicMessageBuilder, IMapMessageBuilder
    {
        /// <summary>
        /// MIME type associated with QPid MapMessages.
        /// </summary>
        public const string MimeType = "jms/map-message";

        /// <summary>
        /// Construct an instance for writing. See <see cref="BasicMessageBuilder"/>.
        /// </summary>
        public MapMessageBuilder(IModel model)
            : base(model)
        {
            Body = new Dictionary<string, object>();
        }

        /// <summary>
        /// Construct an instance for writing. See <see cref="BasicMessageBuilder"/>.
        /// </summary>
        public MapMessageBuilder(IModel model, int initialAccumulatorSize)
            : base(model, initialAccumulatorSize)
        {
            Body = new Dictionary<string, object>();
        }

        /// <summary>
        /// Retrieves the dictionary that will be written into the body of the message.
        /// </summary>
        public IDictionary<string, object> Body { get; protected set; }

        /// <summary>
        /// Finish and retrieve the content body for transmission.
        /// </summary>
        /// <remarks>
        /// Calling this message clears Body to null. Subsequent calls will fault.
        /// </remarks>
        public override byte[] GetContentBody()
        {
            MapWireFormatting.WriteMap(Writer, Body);
            var res = base.GetContentBody();
            Body = null;
            return res;
        }

        /// <summary>
        /// Returns the default MIME content type for messages this instance constructs,
        /// or null if none is available or relevant.
        /// </summary>
        public override string GetDefaultContentType()
        {
            return MimeType;
        }
    }
}
