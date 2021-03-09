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

using Xunit;

namespace RabbitMQ.Client.Unit
{

    public class TestNoWait : IntegrationFixture {
        [Fact]
        public void TestQueueDeclareNoWait()
        {
            string q = GenerateQueueName();
            _model.QueueDeclareNoWait(q, false, true, false, null);
            _model.QueueDeclarePassive(q);
        }

        [Fact]
        public void TestQueueBindNoWait()
        {
            string q = GenerateQueueName();
            _model.QueueDeclareNoWait(q, false, true, false, null);
            _model.QueueBindNoWait(q, "amq.fanout", "", null);
        }

        [Fact]
        public void TestQueueDeleteNoWait()
        {
            string q = GenerateQueueName();
            _model.QueueDeclareNoWait(q, false, true, false, null);
            _model.QueueDeleteNoWait(q, false, false);
        }

        [Fact]
        public void TestExchangeDeclareNoWait()
        {
            string x = GenerateExchangeName();
            try
            {
                _model.ExchangeDeclareNoWait(x, "fanout", false, true, null);
                _model.ExchangeDeclarePassive(x);
            } finally {
                _model.ExchangeDelete(x);
            }
        }

        [Fact]
        public void TestExchangeBindNoWait()
        {
            string x = GenerateExchangeName();
            try
            {
                _model.ExchangeDeclareNoWait(x, "fanout", false, true, null);
                _model.ExchangeBindNoWait(x, "amq.fanout", "", null);
            } finally {
                _model.ExchangeDelete(x);
            }
        }

        [Fact]
        public void TestExchangeUnbindNoWait()
        {
            string x = GenerateExchangeName();
            try
            {
                _model.ExchangeDeclare(x, "fanout", false, true, null);
                _model.ExchangeBind(x, "amq.fanout", "", null);
                _model.ExchangeUnbindNoWait(x, "amq.fanout", "", null);
            } finally {
                _model.ExchangeDelete(x);
            }
        }

        [Fact]
        public void TestExchangeDeleteNoWait()
        {
            string x = GenerateExchangeName();
            _model.ExchangeDeclareNoWait(x, "fanout", false, true, null);
            _model.ExchangeDeleteNoWait(x, false);
        }
    }
}
