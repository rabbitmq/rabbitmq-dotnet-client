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
using System.Threading;
using RabbitMQ.Client;
using Xunit.Abstractions;

namespace Test.SequentialIntegration
{
    public class SequentialIntegrationFixture : IntegrationFixture
    {
        public SequentialIntegrationFixture(ITestOutputHelper output) : base(output)
        {
        }

        public void Block()
        {
            _rabbitMQCtl.ExecRabbitMQCtl("set_vm_memory_high_watermark 0.000000001");
            // give rabbitmqctl some time to do its job
            Thread.Sleep(TimeSpan.FromSeconds(2));
            Publish();
        }

        public void Unblock()
        {
            _rabbitMQCtl.ExecRabbitMQCtl("set_vm_memory_high_watermark 0.4");
        }

        public void RestartRabbitMQ()
        {
            StopRabbitMQ();
            Thread.Sleep(TimeSpan.FromMilliseconds(500));
            StartRabbitMQ();
            AwaitRabbitMQ();
        }

        public void StopRabbitMQ()
        {
            _rabbitMQCtl.ExecRabbitMQCtl("stop_app");
        }

        public void StartRabbitMQ()
        {
            _rabbitMQCtl.ExecRabbitMQCtl("start_app");
        }

        private void AwaitRabbitMQ()
        {
            _rabbitMQCtl.ExecRabbitMQCtl("await_startup");
        }

        private void Publish()
        {
            using (IChannel ch = _conn.CreateChannel())
            {
                ch.BasicPublish("amq.fanout", "", _encoding.GetBytes("message"));
            }
        }
    }
}
