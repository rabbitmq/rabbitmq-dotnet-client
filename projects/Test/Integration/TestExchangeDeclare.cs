// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2020 VMware, Inc.
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
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestExchangeDeclare : IntegrationFixture
    {
        private readonly Random _rnd = new Random();

        public TestExchangeDeclare(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestConcurrentExchangeDeclareAndBind()
        {
            var exchangeNames = new ConcurrentBag<ExchangeName>();
            var tasks = new List<Task>();

            ExchangeName ex_destination = GenerateExchangeName();
            await _channel.ExchangeDeclareAsync(exchange: ex_destination, type: ExchangeType.Fanout, false, false);

            NotSupportedException nse = null;
            for (int i = 0; i < 256; i++)
            {
                async Task f()
                {
                    try
                    {
                        await Task.Delay(S_Random.Next(5, 50));
                        ExchangeName exchangeName = GenerateExchangeName();
                        await _channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Fanout, false, false);
                        await _channel.ExchangeBindAsync(destination: ex_destination, source: exchangeName, routingKey: "unused");
                        exchangeNames.Add(exchangeName);
                    }
                    catch (NotSupportedException e)
                    {
                        nse = e;
                    }
                }
                var t = Task.Run(f);
                tasks.Add(t);
            }

            await AssertRanToCompletion(tasks);
            Assert.Null(nse);
            tasks.Clear();

            foreach (ExchangeName exchangeName in exchangeNames)
            {
                async Task f()
                {
                    try
                    {
                        await Task.Delay(S_Random.Next(5, 50));
                        await _channel.ExchangeUnbindAsync(destination: ex_destination, source: exchangeName, routingKey: "unused",
                            noWait: false, arguments: null);
                        await _channel.ExchangeDeleteAsync(exchange: exchangeName, ifUnused: false);
                    }
                    catch (NotSupportedException e)
                    {
                        nse = e;
                    }
                }
                var t = Task.Run(f);
                tasks.Add(t);
            }

            await AssertRanToCompletion(tasks);
            Assert.Null(nse);
            await _channel.ExchangeDeleteAsync(exchange: ex_destination);
        }

        [Fact]
        public async Task TestConcurrentExchangeDeclareAndDelete()
        {
            var exchangeNames = new ConcurrentBag<ExchangeName>();
            var tasks = new List<Task>();
            NotSupportedException nse = null;
            for (int i = 0; i < 256; i++)
            {
                var t = Task.Run(async () =>
                        {
                            try
                            {
                                // sleep for a random amount of time to increase the chances
                                // of thread interleaving. MK.
                                await Task.Delay(_rnd.Next(5, 500));
                                ExchangeName exchangeName = GenerateExchangeName();
                                await _channel.ExchangeDeclareAsync(exchange: exchangeName, ExchangeType.Fanout, false, false);
                                exchangeNames.Add(exchangeName);
                            }
                            catch (NotSupportedException e)
                            {
                                nse = e;
                            }
                        });
                tasks.Add(t);
            }

            await Task.WhenAll(tasks);

            Assert.Null(nse);
            tasks.Clear();

            foreach (ExchangeName exchangeName in exchangeNames)
            {
                ExchangeName ex = exchangeName;
                var t = Task.Run(async () =>
                        {
                            try
                            {
                                // sleep for a random amount of time to increase the chances
                                // of thread interleaving. MK.
                                await Task.Delay(_rnd.Next(5, 500));
                                await _channel.ExchangeDeleteAsync(ex);
                            }
                            catch (NotSupportedException e)
                            {
                                nse = e;
                            }
                        });
                tasks.Add(t);
            }

            await Task.WhenAll(tasks);

            Assert.Null(nse);
        }
    }
}
