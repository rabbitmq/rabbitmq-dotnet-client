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

namespace RabbitMQ.Client.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;

#if NET451
#else
[EventData]
#endif
    public class RabbitMqExceptionDetail
    {
        public RabbitMqExceptionDetail(Exception ex)
        {
            this.Type = ex.GetType().FullName;
            this.Message = ex.Message;
            this.StackTrace = ex.StackTrace;
            if(ex.InnerException != null)
            {
                this.InnerException = ex.InnerException.ToString();
            }
        }

        public RabbitMqExceptionDetail(IDictionary<string, object> ex)
        {
            this.Type = ex["Type"].ToString();
            this.Message = ex["Message"].ToString();
            this.StackTrace = ex["StackTrace"].ToString();
            object inner;
            if(ex.TryGetValue("InnerException", out inner))
            {
                this.InnerException = inner.ToString();
            }
        }

        public string Type { get; private set; }
        public string Message { get; private set; }
        public string StackTrace { get; private set; }
        public string InnerException { get; private set; }

        public override string ToString()
        {
            return String.Format("Exception: {0}\r\n{1}\r\n\r\n{2}\r\nInnerException:\r\n{3}", Type, Message, StackTrace, InnerException);
        }
    }
}
