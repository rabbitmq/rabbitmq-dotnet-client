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
using RabbitMQ.Client;
using Xunit;

// This file deliberately exercises the deprecated process-wide tracing configuration on
// RabbitMQActivitySource, which is retained for back-compat behind [Obsolete]. See issue #1981.
#pragma warning disable CS0618

namespace Test.Unit
{
    public class TestRabbitMQActivitySource
    {
        // These setters guard against null so that a null assignment fails at the
        // point of the mistake, rather than arming a NullReferenceException on the
        // next publish or delivery. The failing assignment never mutates the static,
        // so these tests do not disturb the process-global tracing configuration.
        // See issue #1981.

        [Fact]
        public void ContextInjectorSetterRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => RabbitMQActivitySource.ContextInjector = null!);
        }

        [Fact]
        public void ContextExtractorSetterRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => RabbitMQActivitySource.ContextExtractor = null!);
        }

        [Fact]
        public void TracingOptionsSetterRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => RabbitMQActivitySource.TracingOptions = null!);
        }

        // -----------------------------------------------------------------------------------------
        // Characterization of the deprecated process-wide tracing statics.
        //
        // These pin the observable behavior of the deprecated statics so that the per-connection
        // ownership rework (issue #1981) cannot silently regress the legacy global path. Before the
        // rework, ContextInjector/ContextExtractor were process-wide statics independent of the
        // TracingOptions object, while UseRoutingKeyAsOperationName was a shortcut into it. The
        // rework unified all of these onto a single RabbitMQTracingOptions instance; the tests below
        // fix the contract that unification must uphold, and mark the one place it intentionally
        // diverges (in-place assignment vs. reference swap).
        //
        // The statics are process-wide, so each test snapshots and restores them via
        // SaveTracingState(); xUnit runs the tests within a class sequentially, and no other Unit
        // test type touches these members. var + target-typed lambdas avoid naming the delegates'
        // nullable-annotated generic types, which the client declares under #nullable enable but
        // this test project does not.
        // -----------------------------------------------------------------------------------------

        [Fact]
        public void ContextInjectorRoundTripsToTheSameInstance()
        {
            using (SaveTracingState())
            {
                RabbitMQActivitySource.ContextInjector = (activity, headers) => { };
                var injector = RabbitMQActivitySource.ContextInjector;

                Assert.Same(injector, RabbitMQActivitySource.ContextInjector);
            }
        }

        [Fact]
        public void ContextExtractorRoundTripsToTheSameInstance()
        {
            using (SaveTracingState())
            {
                RabbitMQActivitySource.ContextExtractor = props => default;
                var extractor = RabbitMQActivitySource.ContextExtractor;

                Assert.Same(extractor, RabbitMQActivitySource.ContextExtractor);
            }
        }

        [Fact]
        public void UseRoutingKeyAsOperationNameStaticAndTracingOptionsShareOneSlot()
        {
            using (SaveTracingState())
            {
                RabbitMQActivitySource.UseRoutingKeyAsOperationName = false;
                Assert.False(RabbitMQActivitySource.TracingOptions.UseRoutingKeyAsOperationName);

                RabbitMQActivitySource.TracingOptions.UseRoutingKeyAsOperationName = true;
                Assert.True(RabbitMQActivitySource.UseRoutingKeyAsOperationName);
            }
        }

        [Fact]
        public void AssigningTracingOptionsCopiesBothSpanShapingOptions()
        {
            using (SaveTracingState())
            {
                RabbitMQActivitySource.UseRoutingKeyAsOperationName = true;
                RabbitMQActivitySource.TracingOptions.UsePublisherAsParent = true;

                RabbitMQActivitySource.TracingOptions = new RabbitMQTracingOptions
                {
                    UseRoutingKeyAsOperationName = false,
                    UsePublisherAsParent = false
                };

                Assert.False(RabbitMQActivitySource.TracingOptions.UseRoutingKeyAsOperationName);
                Assert.False(RabbitMQActivitySource.TracingOptions.UsePublisherAsParent);
            }
        }

        [Fact]
        public void AssigningTracingOptionsPreservesConfiguredDelegates()
        {
            using (SaveTracingState())
            {
                RabbitMQActivitySource.ContextInjector = (activity, headers) => { };
                RabbitMQActivitySource.ContextExtractor = props => default;
                var customInjector = RabbitMQActivitySource.ContextInjector;
                var customExtractor = RabbitMQActivitySource.ContextExtractor;

                RabbitMQActivitySource.TracingOptions = new RabbitMQTracingOptions { UsePublisherAsParent = false };

                Assert.Same(customInjector, RabbitMQActivitySource.ContextInjector);
                Assert.Same(customExtractor, RabbitMQActivitySource.ContextExtractor);
            }
        }

        [Fact]
        public void AssigningTracingOptionsDoesNotAdoptTheInstanceDelegates()
        {
            using (SaveTracingState())
            {
                RabbitMQActivitySource.ContextInjector = (activity, headers) => { };
                var configuredInjector = RabbitMQActivitySource.ContextInjector;

                var incoming = new RabbitMQTracingOptions();
                incoming.ContextInjector = (activity, headers) => { };
                RabbitMQActivitySource.TracingOptions = incoming;

                // The configured injector is kept; the assigned instance's own injector is ignored.
                // Delegates must be set through ContextInjector/ContextExtractor, not by assignment.
                Assert.Same(configuredInjector, RabbitMQActivitySource.ContextInjector);
                Assert.NotSame(incoming.ContextInjector, RabbitMQActivitySource.ContextInjector);
            }
        }

        [Fact]
        public void AssigningTracingOptionsMutatesTheExistingInstanceInPlace()
        {
            using (SaveTracingState())
            {
                var before = RabbitMQActivitySource.TracingOptions;

                RabbitMQActivitySource.TracingOptions = new RabbitMQTracingOptions();

                // Intentionally divergent from the pre-#1981 behavior, which swapped the reference.
                // The delegates now live on this same instance, so assignment copies the span-shaping
                // options into it rather than replacing it, which is what keeps a configured
                // injector/extractor from being reset. See AssigningTracingOptionsPreservesConfiguredDelegates.
                Assert.Same(before, RabbitMQActivitySource.TracingOptions);
            }
        }

        private static IDisposable SaveTracingState() => new TracingStateScope();

        private sealed class TracingStateScope : IDisposable
        {
            private readonly Action _restore;

            public TracingStateScope()
            {
                var injector = RabbitMQActivitySource.ContextInjector;
                var extractor = RabbitMQActivitySource.ContextExtractor;
                bool useRoutingKeyAsOperationName = RabbitMQActivitySource.UseRoutingKeyAsOperationName;
                bool usePublisherAsParent = RabbitMQActivitySource.TracingOptions.UsePublisherAsParent;
                _restore = () =>
                {
                    RabbitMQActivitySource.ContextInjector = injector;
                    RabbitMQActivitySource.ContextExtractor = extractor;
                    RabbitMQActivitySource.UseRoutingKeyAsOperationName = useRoutingKeyAsOperationName;
                    RabbitMQActivitySource.TracingOptions.UsePublisherAsParent = usePublisherAsParent;
                };
            }

            public void Dispose() => _restore();
        }
    }
}
#pragma warning restore CS0618
