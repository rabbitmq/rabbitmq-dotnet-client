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
using System.Text;

namespace RabbitMQ.Client
{
    // Drives a DistributedContextPropagator through the client's header table, which stores values as
    // UTF-8 byte arrays rather than strings.
    //
    // Deliberately `//` and not `///`: `///` on an internal member ships in RabbitMQ.Client.xml.
    internal sealed class PropagatorAdapter
    {
        internal PropagatorAdapter(DistributedContextPropagator propagator)
        {
            Propagator = propagator;

            // Bind once. A method group converts to a fresh delegate at every conversion site, so
            // handing out `Inject` directly would allocate two delegates per resolve - and the resolve
            // runs on every publish and delivery.
            Injector = InjectCore;
            Extractor = ExtractCore;
        }

        internal DistributedContextPropagator Propagator { get; }

        internal Action<Activity, IDictionary<string, object?>> Injector { get; }

        internal Func<IReadOnlyBasicProperties, ActivityContext> Extractor { get; }

        private void InjectCore(Activity activity, IDictionary<string, object?> headers)
        {
            Propagator.Inject(activity, headers, static (carrier, name, value) =>
            {
                if (carrier is IDictionary<string, object?> dictionary)
                {
                    dictionary[name] = Encoding.UTF8.GetBytes(value);
                }
            });
        }

        private ActivityContext ExtractCore(IReadOnlyBasicProperties properties)
        {
            // A propagator supplied by an integration package may carry per-message state - resetting
            // OpenTelemetry's Baggage.Current is the known case - so it is called even when there are
            // no headers to read, with a null carrier it is required to tolerate.
            Propagator.ExtractTraceIdAndState(properties.Headers, static (object? carrier, string name,
                out string? value, out IEnumerable<string>? values) =>
            {
                values = null;
                value = null;
                if (carrier is IDictionary<string, object?> dictionary &&
                    dictionary.TryGetValue(name, out object? raw) && raw is byte[] bytes)
                {
                    value = Encoding.UTF8.GetString(bytes);
                }
            }, out string? traceParent, out string? traceState);

            return ActivityContext.TryParse(traceParent, traceState, out ActivityContext context)
                ? context
                : default;
        }
    }
}
