// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;

namespace RabbitMQ.Client.Logging
{
    [EventData]
    public class RabbitMqExceptionDetail
    {
        public RabbitMqExceptionDetail(Exception ex)
        {
            Type = ex.GetType().FullName;
            Message = ex.Message;
            StackTrace = ex.StackTrace;
            if (ex.InnerException != null)
            {
                InnerException = ex.InnerException.ToString();
            }
        }

        public RabbitMqExceptionDetail(IDictionary<string, object> ex)
        {
            Type = ex["Type"].ToString();
            Message = ex["Message"].ToString();
            StackTrace = ex["StackTrace"].ToString();
            if (ex.TryGetValue("InnerException", out object inner))
            {
                InnerException = inner.ToString();
            }
        }

        public string Type { get; private set; }
        public string Message { get; private set; }
        public string StackTrace { get; private set; }
        public string InnerException { get; private set; }

        public override string ToString() => $"Exception: {Type}\r\n{Message}\r\n\r\n{StackTrace}\r\nInnerException:\r\n{InnerException}";
    }
}
