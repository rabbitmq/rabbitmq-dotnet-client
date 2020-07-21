// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2020 VMware, Inc.
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
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;

namespace RabbitMQ.Client.Exceptions
{
#if !NETSTANDARD1_5
    [Serializable]
#endif
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
    }
}
