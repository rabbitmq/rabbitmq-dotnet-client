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
using OpenTelemetry;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using Test;
using Xunit;

namespace Test.SequentialIntegration
{
    /*
     * rabbitmq/rabbitmq-dotnet-client#1981
     *
     * The process-wide tracing configuration on RabbitMQActivitySource. These tests need no broker,
     * but they mutate process-global state, so they belong here rather than in Unit: this project
     * injects Xunit.CollectionBehavior.CollectionPerAssembly, which serializes every test class in
     * the assembly. Unit does not, so its classes run in parallel and would race.
     */
    /*
     * Every test here reads or writes the deprecated process-wide statics on purpose - pinning their
     * behaviour is the point, and #2009 deprecates them in favour of ConnectionFactory.TracingOptions
     * without changing what they do. The suppression covers the class body rather than each site.
     */
#pragma warning disable CS0618
    public class TestTracingConfiguration
    {
        private static void NoopInjector(Activity activity, IDictionary<string, object> headers)
        {
        }

        private static ActivityContext NoopExtractor(IReadOnlyBasicProperties properties) => default;

        [Fact]
        public void AssigningNullContextInjectorThrows()
        {
            /*
             * Previously these setters accepted null, and the failure surfaced as a
             * NullReferenceException thrown from inside the client on the next publish or
             * delivery - arbitrarily far from the assignment that caused it, and on a path the
             * caller does not own. Throwing at the assignment puts the exception where the
             * mistake is.
             */
            using var scope = new TracingConfigurationScope();

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () => RabbitMQActivitySource.ContextInjector = null);
            Assert.Equal("value", ex.ParamName);

            // The rejected assignment must not have damaged the existing configuration.
            Assert.NotNull(RabbitMQActivitySource.ContextInjector);
        }

        [Fact]
        public void AssigningNullContextExtractorThrows()
        {
            using var scope = new TracingConfigurationScope();

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () => RabbitMQActivitySource.ContextExtractor = null);
            Assert.Equal("value", ex.ParamName);

            Assert.NotNull(RabbitMQActivitySource.ContextExtractor);
        }

        [Fact]
        public void AssigningNullTracingOptionsThrows()
        {
            /*
             * TracingOptions is read on every traced publish, get and delivery, and
             * UseRoutingKeyAsOperationName forwards to it, so a null here broke more paths than
             * either delegate did.
             */
            using var scope = new TracingConfigurationScope();

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () => RabbitMQActivitySource.TracingOptions = null);
            Assert.Equal("value", ex.ParamName);

            Assert.NotNull(RabbitMQActivitySource.TracingOptions);
            // Reachable only because the assignment was rejected; it would throw otherwise.
            Assert.True(RabbitMQActivitySource.UseRoutingKeyAsOperationName ||
                        false == RabbitMQActivitySource.UseRoutingKeyAsOperationName);
        }

        [Fact]
        public void AssigningContextInjectorTakesEffect()
        {
            /*
             * Asserts against a delegate this test owns, not against a second read of the getter:
             * comparing two reads of one property passes even if the setter is a no-op, so it
             * cannot fail for the reason such a test claims to check.
             */
            using var scope = new TracingConfigurationScope();

            Action<Activity, IDictionary<string, object>> injector = NoopInjector;
            RabbitMQActivitySource.ContextInjector = injector;

            Assert.Same(injector, RabbitMQActivitySource.ContextInjector);
        }

        [Fact]
        public void AssigningContextExtractorTakesEffect()
        {
            using var scope = new TracingConfigurationScope();

            Func<IReadOnlyBasicProperties, ActivityContext> extractor = NoopExtractor;
            RabbitMQActivitySource.ContextExtractor = extractor;

            Assert.Same(extractor, RabbitMQActivitySource.ContextExtractor);
        }

        [Fact]
        public void AssigningTracingOptionsAdoptsTheInstance()
        {
            /*
             * Pins reference-swap semantics: the property holds the instance it was given, so a
             * caller that keeps a reference can still mutate the live configuration through it.
             * Code written against 7.2.x relies on this, and an implementation that copied values
             * out of the instance instead would break two things at once - the held reference would
             * silently stop working, and save-then-restore would become a no-op, because the getter
             * would hand back the same singleton the setter had copied into.
             */
            using var scope = new TracingConfigurationScope();

            var options = new RabbitMQTracingOptions { UseRoutingKeyAsOperationName = false };
            RabbitMQActivitySource.TracingOptions = options;

            Assert.Same(options, RabbitMQActivitySource.TracingOptions);
            Assert.False(RabbitMQActivitySource.UseRoutingKeyAsOperationName);

            // Mutating the instance the caller still holds reaches the client.
            options.UseRoutingKeyAsOperationName = true;
            Assert.True(RabbitMQActivitySource.UseRoutingKeyAsOperationName);
        }

        [Fact]
        public void SaveAndRestoreRoundTripsTheConfiguration()
        {
            /*
             * The idiom the parameterized tracing tests depend on, and the one an application uses
             * to configure tracing temporarily. It only works while the setter adopts the instance;
             * see AssigningTracingOptionsAdoptsTheInstance.
             */
            using var scope = new TracingConfigurationScope();

            RabbitMQTracingOptions original = RabbitMQActivitySource.TracingOptions;
            bool originalFlag = original.UseRoutingKeyAsOperationName;

            RabbitMQActivitySource.TracingOptions =
                new RabbitMQTracingOptions { UseRoutingKeyAsOperationName = false == originalFlag };
            Assert.NotSame(original, RabbitMQActivitySource.TracingOptions);

            RabbitMQActivitySource.TracingOptions = original;

            Assert.Same(original, RabbitMQActivitySource.TracingOptions);
            Assert.Equal(originalFlag, RabbitMQActivitySource.UseRoutingKeyAsOperationName);
        }

        [Fact]
        public void UseRoutingKeyAsOperationNameForwardsToTheCurrentOptionsInstance()
        {
            using var scope = new TracingConfigurationScope();

            var options = new RabbitMQTracingOptions();
            RabbitMQActivitySource.TracingOptions = options;

            RabbitMQActivitySource.UseRoutingKeyAsOperationName = false;
            Assert.False(options.UseRoutingKeyAsOperationName);

            options.UseRoutingKeyAsOperationName = true;
            Assert.True(RabbitMQActivitySource.UseRoutingKeyAsOperationName);
        }

        [Fact]
        public void ConfigurationIsProcessWideAcrossTracerProviders()
        {
            /*
             * Characterizes what the documentation on these members now states plainly, rather than
             * asserting a scope the platform cannot deliver. Two TracerProviders configure the
             * client differently; the second wins for both, and disposing it restores nothing.
             *
             * This is not a defect that a lock or a reference count could fix. One ActivitySource
             * produces a single Activity shared by every listener, and one publish injects a single
             * set of headers, so neither span shape nor propagation can differ per provider. The fix
             * is configuration owned by something narrower than the process, which is tracked on
             * #1981 and is not what these members are.
             */
            using var scope = new TracingConfigurationScope();

            using (TracerProvider first = Sdk.CreateTracerProviderBuilder()
                       .AddRabbitMQInstrumentation(options => options.UseRoutingKeyAsOperationName = true)
                       .Build())
            {
                Assert.True(RabbitMQActivitySource.UseRoutingKeyAsOperationName);

                using (TracerProvider second = Sdk.CreateTracerProviderBuilder()
                           .AddRabbitMQInstrumentation(options => options.UseRoutingKeyAsOperationName = false)
                           .Build())
                {
                    // The first provider's configuration is gone, and it is still alive.
                    Assert.False(RabbitMQActivitySource.UseRoutingKeyAsOperationName);
                }

                // Disposing the second restored nothing.
                Assert.False(RabbitMQActivitySource.UseRoutingKeyAsOperationName);
            }
        }
    }
#pragma warning restore CS0618
}
