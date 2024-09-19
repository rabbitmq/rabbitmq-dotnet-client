// This source code is dual-licensed under the Apache License, version
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
using System.Collections.Generic;
using System.Threading;

namespace RabbitMQ.Client.Events
{
    public abstract class BaseExceptionEventArgs : AsyncEventArgs
    {
        ///<summary>Wrap an exception thrown by a callback.</summary>
        protected BaseExceptionEventArgs(IDictionary<string, object> detail, Exception exception, CancellationToken cancellationToken = default)
            : base(cancellationToken)
        {
            Detail = detail;
            Exception = exception;
        }

        ///<summary>Access helpful information about the context in
        ///which the wrapped exception was thrown.</summary>
        public readonly IDictionary<string, object> Detail;

        ///<summary>Access the wrapped exception.</summary>
        public readonly Exception Exception;
    }

    ///<summary>Describes an exception that was thrown during the
    ///library's invocation of an application-supplied callback
    ///handler.</summary>
    ///<remarks>
    ///<para>
    /// When an exception is thrown from a callback registered with
    /// part of the RabbitMQ .NET client library, it is caught,
    /// packaged into a CallbackExceptionEventArgs, and passed through
    /// the appropriate IChannel's or IConnection's CallbackException
    /// event handlers. If an exception is thrown in a
    /// CallbackException handler, it is silently swallowed, as
    /// CallbackException is the last chance to handle these kinds of
    /// exception.
    ///</para>
    ///<para>
    /// Code constructing CallbackExceptionEventArgs instances will
    /// usually place helpful information about the context of the
    /// call in the IDictionary available through the Detail property.
    ///</para>
    ///</remarks>
    public class CallbackExceptionEventArgs : BaseExceptionEventArgs
    {
        private const string ContextString = "context";
        private const string ConsumerString = "consumer";

        // TODO Why is this public when there is a build method?
        public CallbackExceptionEventArgs(IDictionary<string, object> detail, Exception exception, CancellationToken cancellationToken = default)
            : base(detail, exception, cancellationToken)
        {
        }

        public static CallbackExceptionEventArgs Build(Exception e, string context, CancellationToken cancellationToken = default)
        {
            var details = new Dictionary<string, object>(1)
            {
                {ContextString, context}
            };
            return new CallbackExceptionEventArgs(details, e, cancellationToken);
        }

        public static CallbackExceptionEventArgs Build(Exception e, string context, object consumer, CancellationToken cancellationToken = default)
        {
            var details = new Dictionary<string, object>(2)
            {
                {ContextString, context},
                {ConsumerString, consumer}
            };
            return new CallbackExceptionEventArgs(details, e, cancellationToken);
        }
    }
}
