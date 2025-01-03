// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
            if (ex.TryGetValue("InnerException", out object? inner))
            {
                InnerException = inner.ToString();
            }
        }

        // NOTE: This type is used to write EventData in RabbitMqClientEventSource.Error. To make it trim-compatible, these properties are preserved
        // in RabbitMqClientEventSource. If RabbitMqExceptionDetail gets a property that is a complex type, we need to ensure the nested properties are
        // preserved as well.

        public string? Type { get; }
        public string? Message { get; }
        public string? StackTrace { get; }
        public string? InnerException { get; }

        public override string ToString()
        {
            return $"Exception: {Type}\r\n{Message}\r\n\r\n{StackTrace}\r\nInnerException:\r\n{InnerException}";
        }
    }
}
