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
//  at https://www.mozilla.org/MPL/
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

namespace RabbitMQ.Client.Exceptions
{
    public abstract class RabbitMQClientException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="RabbitMQClientException" /> class.</summary>
        protected RabbitMQClientException()
        {

        }

        /// <summary>Initializes a new instance of the <see cref="RabbitMQClientException" /> class with a specified error message.</summary>
        /// <param name="message">The message that describes the error. </param>
        protected RabbitMQClientException(string message) : base(message)
        {

        }

        /// <summary>Initializes a new instance of the <see cref="RabbitMQClientException" /> class with a specified error message and a reference to the inner exception that is the cause of this exception.</summary>
        /// <param name="message">The error message that explains the reason for the exception. </param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified. </param>
        protected RabbitMQClientException(string message, Exception innerException) : base(message, innerException)
        {

        }

/*
#if !(NETFX_CORE)
        /// <summary>Initializes a new instance of the <see cref="RabbitMQClientException" /> class with serialized data.</summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo" /> that holds the serialized object data about the exception being thrown. </param>
        /// <param name="context">The <see cref="System.Runtime.Serialization.StreamingContext" /> that contains contextual information about the source or destination. </param>
        /// <exception cref="System.ArgumentNullException">The <paramref name="info" /> parameter is null. </exception>
        /// <exception cref="System.Runtime.Serialization.SerializationException">The class name is null or <see cref="System.Exception.HResult" /> is zero (0). </exception>
        [System.Security.SecuritySafeCritical]
        protected RabbitMQClientException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {

        }
#endif
*/
    }
}
