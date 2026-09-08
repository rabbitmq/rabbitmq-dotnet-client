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

using RabbitMQ.Client;
using RabbitMQ.Client.ConsumerDispatching;
using Xunit;

namespace Test.Unit
{
    /// <summary>
    /// rabbitmq/rabbitmq-dotnet-client#2035. A dispatch concurrency of zero built a consumer
    /// dispatcher with no reader loops, so nothing drained the work channel. See
    /// <c>docs/internal/consumer-dispatch-concurrency.md</c> for the mechanism.
    ///
    /// These assert on <see cref="IConsumerDispatcher.Concurrency"/>, which is the state the broken
    /// loop count is derived from, rather than on the options resolution that feeds it. The dispatcher
    /// is where the invariant lives and can be constructed directly, so no broker is needed.
    /// </summary>
    public class TestConsumerDispatchConcurrency
    {
        [Theory]
        [InlineData((ushort)0, (ushort)1)]  // the bug: zero would build no reader loops
        [InlineData((ushort)1, (ushort)1)]
        [InlineData((ushort)2, (ushort)2)]
        [InlineData((ushort)9, (ushort)9)]
        public void DispatcherNeverHasZeroReaderLoops_GH2035(ushort requested, ushort expected)
        {
            /*
             * Expectations are written out per row rather than computed, so the assertion cannot
             * restate the implementation it is checking.
             */
            using var dispatcher = new AsyncConsumerDispatcher(null, requested);

            Assert.Equal(expected, dispatcher.Concurrency);
        }

        [Theory]
        [InlineData((ushort)0, (ushort)0)]
        [InlineData((ushort)4, (ushort)4)]
        public void OptionsResolveTheCallersValueVerbatim_GH2035(ushort requested, ushort expected)
        {
            /*
             * The options layer deliberately does NOT correct zero: it reports what was asked for, so
             * the public field and this resolution agree, and the dispatcher applies the floor. This
             * pins that split so a future change does not quietly move the guard back up a layer and
             * leave the dispatcher unprotected against its other callers.
             */
            var options = new CreateChannelOptions(publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false, consumerDispatchConcurrency: requested);

            Assert.Equal(expected, options.InternalConsumerDispatchConcurrency);
        }
    }
}
