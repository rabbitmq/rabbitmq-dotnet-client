// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2012 VMware, Inc.
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
//  The Initial Developer of the Original Code is VMware, Inc.
//  Copyright (c) 2007-2012 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections;
using System.IO;
using System.Text;

namespace RabbitMQ.Client.Exceptions {
    ///<summary>Thrown when no connection could be opened during a
    ///ConnectionFactory.CreateConnection attempt.</summary>
    ///<remarks>
    /// CreateConnection (optionally) handles redirections, so even a
    /// single-endpoint connection attempt may end up attempting to
    /// connect to multiple TCP endpoints. This exception contains
    /// information on how many times each endpoint was tried, and the
    /// outcome of the most recent attempt against each endpoint. See
    /// the ConnectionAttempts and ConnectionErrors properties.
    ///</remarks>
    public class BrokerUnreachableException: IOException
    {
        private IDictionary m_connectionAttempts;
        private IDictionary m_connectionErrors;

        ///<summary>A map from AmqpTcpEndpoint to int, counting the
        ///number of attempts that were made against each
        ///endpoint.</summary>
        public IDictionary ConnectionAttempts { get { return m_connectionAttempts; } }

        ///<summary>A map from AmqpTcpEndpoint to Exception, recording
        ///the outcome of the most recent connection attempt against
        ///each endpoint.</summary>
        public IDictionary ConnectionErrors { get { return m_connectionErrors; } }

        ///<summary>same as ConnectionErrors property</summary>
        public override IDictionary Data { get { return m_connectionErrors; } }

        ///<summary>Construct a BrokerUnreachableException. Expects
        ///maps as per the description of the ConnectionAttempts and
        ///ConnectionErrors properties. The inner exception is associated
        ///with only one connection attempt.</summary>
        public BrokerUnreachableException(IDictionary connectionAttempts,
                                          IDictionary connectionErrors,
                                          Exception Inner)
            : base("None of the specified endpoints were reachable", Inner)
        {
            m_connectionAttempts = connectionAttempts;
            m_connectionErrors = connectionErrors;
        }

        ///<summary>Provide a full description of the various
        ///connection attempts that were made, as well as the usual
        ///Exception stack trace.</summary>
        public override string ToString() {
            StringBuilder sb = new StringBuilder(base.Message);
            sb.Append("\nEndpoints attempted:\n");
            foreach (DictionaryEntry entry in m_connectionAttempts) {
                sb.Append("------------------------------------------------\n");
                sb.Append("endpoint=").Append(entry.Key);
                sb.Append(", attempts=").Append(entry.Value).Append("\n");
                sb.Append(m_connectionErrors[entry.Key] as Exception);
            }
            sb.Append("\n================================================\n");
            sb.Append("Stack trace:\n");
            sb.Append(base.StackTrace);
            return sb.ToString();
        }
    }
}
