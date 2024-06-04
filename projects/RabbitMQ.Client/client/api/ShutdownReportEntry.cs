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

namespace RabbitMQ.Client
{
    /// <summary>
    /// Single entry object in the shutdown report that encapsulates description
    /// of the error which occurred during shutdown.
    /// </summary>
    public class ShutdownReportEntry
    {
        public ShutdownReportEntry(string description, Exception exception)
        {
            Description = description;
            Exception = exception;
        }

        /// <summary>
        /// Description provided in the error.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// <see cref="Exception"/> object that occurred during shutdown, or null if unspecified.
        /// </summary>
        public Exception Exception { get; set; }

        public override string ToString()
        {
            string description = $"Message: {Description}";
            return (Exception != null) ? $"{description} Exception: {Exception}" : description;
        }
    }
}
