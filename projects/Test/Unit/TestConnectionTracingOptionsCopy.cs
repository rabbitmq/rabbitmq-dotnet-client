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


#nullable enable

using System;
using System.Diagnostics;
using System.Reflection;
using RabbitMQ.Client;
using Xunit;

namespace Test.Unit
{
    public class TestConnectionTracingOptionsCopy
    {
        [Fact]
        public void CopyCarriesEveryPublicMember_GH2152()
        {
            var original = new ConnectionTracingOptions
            {
                UseRoutingKeyAsOperationName = false,
                UsePublisherAsParent = false,
                IncludeMessageSizes = true,
                Propagator = DistributedContextPropagator.CreatePassThroughPropagator()
            };

            PropertyInfo[] properties = typeof(ConnectionTracingOptions)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance);
            // A new member must be given a non-default value above, so the copy check below covers it.
            Assert.All(properties, p => Assert.NotNull(p.GetValue(original)));

            ConnectionTracingOptions copy = original.Copy();

            Assert.NotSame(original, copy);
            Assert.All(properties, p => Assert.Equal(p.GetValue(original), p.GetValue(copy)));
        }

        [Fact]
        public void AConnectionKeepsACopyOfTheFactorysTracingOptions_GH2152()
        {
            var cf = new ConnectionFactory { TracingOptions = new ConnectionTracingOptions { UsePublisherAsParent = false } };
            MethodInfo createConfig = typeof(ConnectionFactory).GetMethod("CreateConfig", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ConnectionFactory.CreateConfig not found");
            var config = (ConnectionConfig)createConfig.Invoke(cf, new object?[] { null })!;

            cf.TracingOptions.UsePublisherAsParent = true;

            Assert.NotSame(cf.TracingOptions, config.TracingOptions);
            Assert.False(config.TracingOptions!.UsePublisherAsParent);
        }

        [Fact]
        public void ChangingTheOriginalAfterCopyingDoesNotReachTheCopy_GH2152()
        {
            var original = new ConnectionTracingOptions { UsePublisherAsParent = false };
            ConnectionTracingOptions copy = original.Copy();

            original.UsePublisherAsParent = true;
            original.Propagator = DistributedContextPropagator.CreateNoOutputPropagator();

            Assert.False(copy.UsePublisherAsParent);
            Assert.Null(copy.Propagator);
        }
    }
}
