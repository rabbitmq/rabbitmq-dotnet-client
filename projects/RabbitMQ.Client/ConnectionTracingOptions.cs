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

using System.Diagnostics;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Tracing configuration owned by a connection. Set it on
    /// <see cref="ConnectionFactory.TracingOptions"/> to configure the connections that factory
    /// creates.
    /// </summary>
    /// <remarks>
    /// Every member is nullable and <see langword="null"/> means "inherit", resolved per member
    /// rather than per object, so setting one member leaves the others alone. This is the same
    /// convention <see cref="CreateChannelOptions.ConsumerDispatchConcurrency"/> uses.
    /// </remarks>
    public sealed class ConnectionTracingOptions
    {
        /// <summary>
        /// Use the routing key as the operation name of a publish span.
        /// <see langword="null"/> inherits.
        /// </summary>
        public bool? UseRoutingKeyAsOperationName { get; set; }

        /// <summary>
        /// Make the publisher's span the parent of the consumer's span rather than a link.
        /// <see langword="null"/> inherits.
        /// </summary>
        public bool? UsePublisherAsParent { get; set; }

        /// <summary>
        /// Tag publish, fetch, and delivery spans with <c>messaging.rabbitmq.vhost.name</c> and
        /// <c>messaging.rabbitmq.cluster.name</c>. <see langword="null"/> inherits.
        /// </summary>
        /// <remarks>
        /// Neither attribute is part of the OpenTelemetry messaging semantic conventions yet
        /// (open-telemetry/semantic-conventions#3997), which is why this opts in rather than
        /// enabling it unconditionally.
        /// </remarks>
        public bool? CaptureVirtualHostAndClusterName { get; set; }

        /// <summary>
        /// Propagates trace context into published messages and out of received ones.
        /// <see langword="null"/> inherits, which ultimately means
        /// <see cref="DistributedContextPropagator.Current"/>.
        /// </summary>
        /// <remarks>
        /// This is the supported extension point for propagation. To propagate OpenTelemetry
        /// <c>Baggage</c> or a non-W3C wire format, install the propagator supplied by the
        /// <c>RabbitMQ.Client.OpenTelemetry</c> package rather than reaching for OpenTelemetry's own
        /// propagator here: a library stays on <see cref="DistributedContextPropagator"/> and
        /// bridging is the application's concern.
        /// </remarks>
        public DistributedContextPropagator? Propagator { get; set; }

        // Adapting a propagator to the client's injector/extractor pair allocates two closures, so
        // cache them per options instance rather than per operation - these options are captured once
        // per connection, the resolve runs on every publish and delivery.
        private PropagatorAdapter? _adapter;

        internal PropagatorAdapter GetOrCreateAdapter()
        {
            DistributedContextPropagator propagator = Propagator
                ?? throw new System.InvalidOperationException("Propagator is null.");

            PropagatorAdapter? adapter = _adapter;
            if (adapter is null || false == ReferenceEquals(adapter.Propagator, propagator))
            {
                adapter = new PropagatorAdapter(propagator);
                _adapter = adapter;
            }

            return adapter;
        }
    }
}
