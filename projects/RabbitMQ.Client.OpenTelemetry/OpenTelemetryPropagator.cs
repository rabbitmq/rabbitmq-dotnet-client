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
using System.Linq;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Bridges OpenTelemetry's configured <see cref="TextMapPropagator"/> to the
    /// <see cref="DistributedContextPropagator"/> the client propagates with. Assign it to
    /// <see cref="ConnectionTracingOptions.Propagator"/> to give a connection OpenTelemetry
    /// propagation, including <c>Baggage</c> and any non-W3C wire format the application has
    /// configured.
    /// </summary>
    /// <remarks>
    /// This exists because a library propagates through <see cref="DistributedContextPropagator"/> and
    /// bridging OpenTelemetry's own propagator is the application's concern. That is what keeps the
    /// core client free of an OpenTelemetry dependency; this type lives in the integration package
    /// where the dependency belongs.
    /// </remarks>
    public sealed class OpenTelemetryPropagator : DistributedContextPropagator
    {
        /// <inheritdoc />
        /// <remarks>
        /// Empty rather than throwing when no OpenTelemetry SDK is present:
        /// <c>Propagators.DefaultTextMapPropagator</c> is then a no-op propagator whose
        /// <c>Fields</c> is <see langword="null"/>.
        /// </remarks>
        public override IReadOnlyCollection<string> Fields =>
            Propagators.DefaultTextMapPropagator.Fields?.ToList() ?? (IReadOnlyCollection<string>)Array.Empty<string>();

        /// <inheritdoc />
        public override void Inject(Activity activity, object carrier, PropagatorSetterCallback setter)
        {
            if (activity is null || setter is null)
            {
                return;
            }

            Propagators.DefaultTextMapPropagator.Inject(
                new PropagationContext(activity.Context, Baggage.Current),
                carrier,
                (c, key, value) => setter(c, key, value));
        }

        /// <inheritdoc />
        /// <remarks>
        /// Also sets <c>Baggage.Current</c>, which is the OpenTelemetry state the client itself cannot
        /// reach. It is <c>AsyncLocal</c>-backed and the consumer dispatcher processes deliveries
        /// sequentially on one flow, so a message with no context must reset it rather than inherit
        /// the previous message's baggage. See rabbitmq/rabbitmq-dotnet-client#1967.
        /// </remarks>
        public override void ExtractTraceIdAndState(object carrier, PropagatorGetterCallback getter,
            out string traceId, out string traceState)
        {
            if (carrier is null || getter is null)
            {
                Baggage.Current = default;
                traceId = null;
                traceState = null;
                return;
            }

            PropagationContext parentContext = Propagators.DefaultTextMapPropagator.Extract(default, carrier,
                (c, key) =>
                {
                    getter(c, key, out string value, out IEnumerable<string> values);
                    return values ?? (value is null ? Enumerable.Empty<string>() : new[] { value });
                });

            Baggage.Current = parentContext.Baggage;

            ActivityContext context = parentContext.ActivityContext;
            if (context == default)
            {
                traceId = null;
                traceState = null;
                return;
            }

            /*
             * The client reparses this with ActivityContext.TryParse, so emit W3C traceparent form.
             * The two values W3C defines take a literal, since this runs once per delivery. The
             * fallback carries any other bit rather than flattening it to Recorded: OpenTelemetry's
             * own W3C propagator masks those away, but a custom TextMapPropagator need not, and
             * TryParse round-trips whatever it is given.
             */
            string flags = context.TraceFlags switch
            {
                ActivityTraceFlags.None => "00",
                ActivityTraceFlags.Recorded => "01",
                _ => ((int)context.TraceFlags).ToString("x2")
            };
            traceId = $"00-{context.TraceId}-{context.SpanId}-{flags}";
            traceState = context.TraceState;
        }

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, string>> ExtractBaggage(object carrier,
            PropagatorGetterCallback getter) =>
            Baggage.Current.GetBaggage().Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value));
    }
}
