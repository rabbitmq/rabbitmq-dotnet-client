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

namespace Test
{
    /// <summary>
    /// Captures every process-wide tracing setting on <see cref="RabbitMQActivitySource"/> and
    /// restores all of them on dispose.
    /// </summary>
    /// <remarks>
    /// Tracing configuration is process-wide, so a test that changes any of it leaves that change in
    /// force for whatever runs next in the same process. That has already produced order-dependent
    /// failures in this suite, which is what this type exists to prevent - see the public-API
    /// discussion on rabbitmq/rabbitmq-dotnet-client#1967 and #1981.
    /// <para>
    /// It covers all five settings on purpose. A scope that saved only the flags let a test that
    /// installed its own propagation delegates leak them for the rest of the run, and one that saved
    /// only the options instance missed a test that mutated the instance in place. Restoring both the
    /// instance and the values it held covers a test that replaced the instance, mutated the original,
    /// or did both.
    /// </para>
    /// </remarks>
    /*
     * Reads and writes the deprecated process-wide statics deliberately: saving and restoring them
     * is the whole purpose of this type, and it is what lets the tests that exercise the deprecated
     * path leave no trace. The suppression covers the class body rather than each site.
     */
#pragma warning disable CS0618
    public sealed class TracingConfigurationScope : IDisposable
    {
        private readonly RabbitMQTracingOptions _options;
        private readonly bool _useRoutingKeyAsOperationName;
        private readonly bool _usePublisherAsParent;
        private readonly Action<Activity, IDictionary<string, object>> _contextInjector;
        private readonly Func<IReadOnlyBasicProperties, ActivityContext> _contextExtractor;

        public TracingConfigurationScope()
        {
            _options = RabbitMQActivitySource.TracingOptions;
            _useRoutingKeyAsOperationName = _options.UseRoutingKeyAsOperationName;
            _usePublisherAsParent = _options.UsePublisherAsParent;
            _contextInjector = RabbitMQActivitySource.ContextInjector;
            _contextExtractor = RabbitMQActivitySource.ContextExtractor;
        }

        /// <summary>
        /// Sets <see cref="RabbitMQTracingOptions.UseRoutingKeyAsOperationName"/> to
        /// <see langword="false"/>, so span names are the bare operation and assertions do not have to
        /// account for a generated queue name.
        /// </summary>
        public TracingConfigurationScope WithPlainOperationNames()
        {
            RabbitMQActivitySource.UseRoutingKeyAsOperationName = false;
            return this;
        }

        public void Dispose()
        {
            RabbitMQActivitySource.TracingOptions = _options;
            _options.UseRoutingKeyAsOperationName = _useRoutingKeyAsOperationName;
            _options.UsePublisherAsParent = _usePublisherAsParent;
            RabbitMQActivitySource.ContextInjector = _contextInjector;
            RabbitMQActivitySource.ContextExtractor = _contextExtractor;
        }
    }
#pragma warning restore CS0618
}
