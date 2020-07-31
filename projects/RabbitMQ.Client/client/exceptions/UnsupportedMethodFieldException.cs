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
    /// <summary>Thrown when the model cannot transmit a method field
    /// because the version of the protocol the model is implementing
    /// does not contain a definition for the field in
    /// question.</summary>
#if !NETSTANDARD1_5
    [Serializable]
#endif
    public class UnsupportedMethodFieldException : NotSupportedException
    {
        public UnsupportedMethodFieldException(string methodName, string fieldName)
        {
            MethodName = methodName;
            FieldName = fieldName;
        }

        ///<summary>The name of the unsupported field.</summary>
        public string FieldName { get; private set; }

        ///<summary>The name of the method involved.</summary>
        public string MethodName { get; private set; }
    }
}
