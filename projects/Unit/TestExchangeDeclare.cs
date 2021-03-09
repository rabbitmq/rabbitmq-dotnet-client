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
using System.Collections.Generic;
using System.Threading;

using Xunit;

namespace RabbitMQ.Client.Unit
{

    public class TestExchangeDeclare : IntegrationFixture {

        [Fact]
        public void TestConcurrentExchangeDeclare()
        {
            string x = GenerateExchangeName();
            Random rnd = new Random();

            List<Thread> ts = new List<Thread>();
            NotSupportedException nse = null;
            for(int i = 0; i < 256; i++)
            {
                Thread t = new Thread(() =>
                        {
                            try
                            {
                                // sleep for a random amount of time to increase the chances
                                // of thread interleaving. MK.
                                Thread.Sleep(rnd.Next(5, 500));
                                _model.ExchangeDeclare(x, "fanout", false, false, null);
                            } catch (NotSupportedException e)
                            {
                                nse = e;
                            }
                        });
                ts.Add(t);
                t.Start();
            }

            foreach (Thread t in ts)
            {
                t.Join();
            }

            Assert.Null(nse);
            _model.ExchangeDelete(x);
        }
    }
}
