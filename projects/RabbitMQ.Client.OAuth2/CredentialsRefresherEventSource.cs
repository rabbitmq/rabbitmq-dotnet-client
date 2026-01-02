// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;

namespace RabbitMQ.Client.OAuth2
{
    [EventSource(Name = "CredentialRefresher")]
    public class CredentialsRefresherEventSource : EventSource
    {
        public static CredentialsRefresherEventSource Log { get; } = new CredentialsRefresherEventSource();

        [Event(1)]
        public void Started(string name) => WriteEvent(1, "Started", name);

        [Event(2)]
        public void Stopped(string name) => WriteEvent(2, "Stopped", name);

        [Event(3)]
#if NET
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "Parameters to this method are primitive and are trimmer safe")]
#endif
        public void RefreshedCredentials(string name) => WriteEvent(3, "RefreshedCredentials", name);
    }
}
