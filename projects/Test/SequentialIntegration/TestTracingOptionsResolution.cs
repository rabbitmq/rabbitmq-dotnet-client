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
using RabbitMQ.Client;
using Xunit;

namespace Test.SequentialIntegration
{
    /*
     * rabbitmq/rabbitmq-dotnet-client#1981
     *
     * How a connection's tracing configuration resolves against the process-wide default. This is
     * the defect #2009 exists to fix: the fallback used to be whole-object, so setting one
     * span-shaping flag on a factory also replaced the propagation delegates with a fresh object's
     * defaults, silently switching that connection off whatever had been installed process-wide.
     *
     * Each case below states the whole expected outcome, both flags and both delegates, so a
     * regression that fixes one member by clobbering another cannot pass. Verified by two mutations,
     * because the flags and the delegates fall back independently and one mutation does not reach
     * both: making the flags whole-object fails PropagatorOnlyKeepsProcessWideFlags and
     * OneFlagDoesNotResetTheOther, and making the delegates whole-object fails
     * FlagOnlyKeepsProcessWideDelegates. ResolvingTwiceReusesTheSamePropagatorAdapter earned its
     * place by failing against a first implementation that cached the adapter but still bound a new
     * delegate pair on every resolve.
     *
     * No broker needed, but these mutate process-global state, so they belong here rather than in
     * Unit: this project serializes every test class in the assembly, Unit runs them in parallel.
     */
    /*
     * The deprecated process-wide statics are written on purpose - they are the layer being resolved
     * against. The suppression covers the class body rather than each site.
     */
#pragma warning disable CS0618
    public class TestTracingOptionsResolution
    {
        private static void MarkerInjector(Activity activity, IDictionary<string, object> headers)
        {
        }

        private static ActivityContext MarkerExtractor(IReadOnlyBasicProperties properties) => default;

        private sealed class MarkerPropagator : DistributedContextPropagator
        {
            public override IReadOnlyCollection<string> Fields { get; } = new[] { "marker" };

            public override void Inject(Activity activity, object carrier, PropagatorSetterCallback setter)
            {
            }

            public override void ExtractTraceIdAndState(object carrier, PropagatorGetterCallback getter,
                out string traceId, out string traceState)
            {
                traceId = null;
                traceState = null;
            }

            public override IEnumerable<KeyValuePair<string, string>> ExtractBaggage(object carrier,
                PropagatorGetterCallback getter) => null;
        }

        // Row 1: nothing configured anywhere.
        [Fact]
        public void NothingConfiguredUsesTheLibraryDefaults()
        {
            using var scope = new TracingConfigurationScope();

            ResolvedTracingOptions resolved = RabbitMQActivitySource.ResolveTracingOptions(null);

            Assert.True(resolved.UseRoutingKeyAsOperationName);
            Assert.True(resolved.UsePublisherAsParent);
            Assert.Same(RabbitMQActivitySource.ContextInjector, resolved.ContextInjector);
            Assert.Same(RabbitMQActivitySource.ContextExtractor, resolved.ContextExtractor);
        }

        // Row 2: process-wide only, no factory options. The deprecated path must keep working.
        [Fact]
        public void ProcessWideAppliesWhenTheFactorySetNothing()
        {
            using var scope = new TracingConfigurationScope();
            RabbitMQActivitySource.TracingOptions.UseRoutingKeyAsOperationName = false;
            RabbitMQActivitySource.ContextInjector = MarkerInjector;
            RabbitMQActivitySource.ContextExtractor = MarkerExtractor;

            ResolvedTracingOptions resolved = RabbitMQActivitySource.ResolveTracingOptions(null);

            Assert.False(resolved.UseRoutingKeyAsOperationName);
            Assert.True(resolved.UsePublisherAsParent);
            Assert.Same((Action<Activity, IDictionary<string, object>>)MarkerInjector, resolved.ContextInjector);
            Assert.Same((Func<IReadOnlyBasicProperties, ActivityContext>)MarkerExtractor, resolved.ContextExtractor);
        }

        // Row 3, and the defect. A propagator on the factory must not disturb the process-wide flags.
        [Fact]
        public void PropagatorOnlyKeepsProcessWideFlags()
        {
            using var scope = new TracingConfigurationScope();
            RabbitMQActivitySource.TracingOptions.UseRoutingKeyAsOperationName = false;
            RabbitMQActivitySource.TracingOptions.UsePublisherAsParent = false;

            var tracing = new ConnectionTracingOptions { Propagator = new MarkerPropagator() };
            ResolvedTracingOptions resolved = RabbitMQActivitySource.ResolveTracingOptions(tracing);

            Assert.False(resolved.UseRoutingKeyAsOperationName);
            Assert.False(resolved.UsePublisherAsParent);
            Assert.NotSame(RabbitMQActivitySource.ContextInjector, resolved.ContextInjector);
        }

        // Row 4: precedence. An owner's propagator beats process-wide delegates, as the flags do.
        [Fact]
        public void OwnerPropagatorBeatsProcessWideDelegates()
        {
            using var scope = new TracingConfigurationScope();
            RabbitMQActivitySource.ContextInjector = MarkerInjector;
            RabbitMQActivitySource.ContextExtractor = MarkerExtractor;

            var tracing = new ConnectionTracingOptions { Propagator = new MarkerPropagator() };
            ResolvedTracingOptions resolved = RabbitMQActivitySource.ResolveTracingOptions(tracing);

            Assert.NotSame((Action<Activity, IDictionary<string, object>>)MarkerInjector, resolved.ContextInjector);
            Assert.NotSame((Func<IReadOnlyBasicProperties, ActivityContext>)MarkerExtractor, resolved.ContextExtractor);
        }

        // Row 7, and the defect from the other direction: a flag must not replace the delegates.
        [Fact]
        public void FlagOnlyKeepsProcessWideDelegates()
        {
            using var scope = new TracingConfigurationScope();
            RabbitMQActivitySource.ContextInjector = MarkerInjector;
            RabbitMQActivitySource.ContextExtractor = MarkerExtractor;

            var tracing = new ConnectionTracingOptions { UsePublisherAsParent = false };
            ResolvedTracingOptions resolved = RabbitMQActivitySource.ResolveTracingOptions(tracing);

            Assert.False(resolved.UsePublisherAsParent);
            Assert.Same((Action<Activity, IDictionary<string, object>>)MarkerInjector, resolved.ContextInjector);
            Assert.Same((Func<IReadOnlyBasicProperties, ActivityContext>)MarkerExtractor, resolved.ContextExtractor);
        }

        // Row 8: members resolve independently of one another.
        [Fact]
        public void OneFlagDoesNotResetTheOther()
        {
            using var scope = new TracingConfigurationScope();
            RabbitMQActivitySource.TracingOptions.UseRoutingKeyAsOperationName = false;

            var tracing = new ConnectionTracingOptions { UsePublisherAsParent = false };
            ResolvedTracingOptions resolved = RabbitMQActivitySource.ResolveTracingOptions(tracing);

            Assert.False(resolved.UseRoutingKeyAsOperationName);
            Assert.False(resolved.UsePublisherAsParent);
        }

        // The adapter is cached, because the resolve runs on every publish and delivery.
        [Fact]
        public void ResolvingTwiceReusesTheSamePropagatorAdapter()
        {
            using var scope = new TracingConfigurationScope();
            var tracing = new ConnectionTracingOptions { Propagator = new MarkerPropagator() };

            ResolvedTracingOptions first = RabbitMQActivitySource.ResolveTracingOptions(tracing);
            ResolvedTracingOptions second = RabbitMQActivitySource.ResolveTracingOptions(tracing);

            Assert.Same(first.ContextInjector, second.ContextInjector);
            Assert.Same(first.ContextExtractor, second.ContextExtractor);
        }

        // Replacing the propagator must take effect rather than serve a stale adapter.
        [Fact]
        public void ReplacingThePropagatorReplacesTheAdapter()
        {
            using var scope = new TracingConfigurationScope();
            var tracing = new ConnectionTracingOptions { Propagator = new MarkerPropagator() };
            ResolvedTracingOptions first = RabbitMQActivitySource.ResolveTracingOptions(tracing);

            tracing.Propagator = new MarkerPropagator();
            ResolvedTracingOptions second = RabbitMQActivitySource.ResolveTracingOptions(tracing);

            Assert.NotSame(first.ContextInjector, second.ContextInjector);
        }
    }
}
