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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using RabbitMQ.Client;
using Xunit;

namespace Test.Unit
{
    public class TestConnectionTracingOptionsPropagator
    {
        [Fact]
        public void ClearingThePropagatorWhileResolvingNeverThrows_GH2152()
        {
            var options = new ConnectionTracingOptions { Propagator = DistributedContextPropagator.CreateDefaultPropagator() };
            DistributedContextPropagator propagator = options.Propagator;
            using var started = new ManualResetEventSlim();
            int stop = 0;
            int cleared = 0;

            // A dedicated thread, started before the timed loop, so a busy or single-CPU thread pool
            // cannot keep the toggler from overlapping the resolves.
            var toggler = new Thread(() =>
            {
                started.Set();
                while (Volatile.Read(ref stop) == 0)
                {
                    options.Propagator = null;
                    cleared++;
                    options.Propagator = propagator;
                }
            });
            toggler.IsBackground = true;
            toggler.Start();
            Assert.True(started.Wait(TimeSpan.FromSeconds(10)), "the toggler never started");

            var headers = new Dictionary<string, object?>();
            using var activity = new Activity("publish").Start();
            int resolves = 0;
            var elapsed = Stopwatch.StartNew();
            try
            {
                while (resolves < 100_000 && elapsed.ElapsedMilliseconds < 500)
                {
                    // Inject as a publish does: an adapter built on a cleared propagator fails only here.
                    RabbitMQActivitySource.ResolveTracingOptions(options).ContextInjector(activity, headers);
                    resolves++;
                }
            }
            finally
            {
                Volatile.Write(ref stop, 1);
                toggler.Join();
            }

            Assert.True(cleared > 0, "the toggler never cleared the propagator, so this test proves nothing");
        }
    }
}
