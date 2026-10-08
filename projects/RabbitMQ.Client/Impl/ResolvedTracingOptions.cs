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

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RabbitMQ.Client
{
    // One operation's effective tracing configuration, produced by
    // RabbitMQActivitySource.ResolveTracingOptions. Resolve once per operation and read every member
    // from the result: resolving twice let one span take its name from one configuration and its
    // propagated context from another.
    //
    // Deliberately `//` and not `///`: csc does not filter doc comments by accessibility, so `///` on
    // an internal member ships in RabbitMQ.Client.xml inside the NuGet package.
    internal readonly struct ResolvedTracingOptions
    {
        internal ResolvedTracingOptions(bool useRoutingKeyAsOperationName, bool usePublisherAsParent,
            bool captureVirtualHostAndClusterName,
            Action<Activity, IDictionary<string, object?>> contextInjector,
            Func<IReadOnlyBasicProperties, ActivityContext> contextExtractor)
        {
            UseRoutingKeyAsOperationName = useRoutingKeyAsOperationName;
            UsePublisherAsParent = usePublisherAsParent;
            CaptureVirtualHostAndClusterName = captureVirtualHostAndClusterName;
            ContextInjector = contextInjector;
            ContextExtractor = contextExtractor;
        }

        internal bool UseRoutingKeyAsOperationName { get; }

        internal bool UsePublisherAsParent { get; }

        internal bool CaptureVirtualHostAndClusterName { get; }

        internal Action<Activity, IDictionary<string, object?>> ContextInjector { get; }

        internal Func<IReadOnlyBasicProperties, ActivityContext> ContextExtractor { get; }
    }
}
