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

namespace RabbitMQ.Client.Impl
{
    internal class RecordedConsumer : IRecordedConsumer
    {
        public RecordedConsumer(AutorecoveringModel model, string queue, string consumerTag)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }
            else
            {
                Model = model ?? throw new ArgumentNullException(nameof(model));
            }

            if (string.IsNullOrEmpty(queue))
            {
                throw new ArgumentNullException(nameof(consumerTag));
            }
            else
            {
                Queue = queue;
            }

            if (string.IsNullOrEmpty(consumerTag))
            {
                throw new ArgumentNullException(nameof(consumerTag));
            }
            else
            {
                ConsumerTag = consumerTag;
            }
        }

        public AutorecoveringModel Model { get; }
        public IDictionary<string, object> Arguments { get; set; }
        public bool AutoAck { get; set; }
        public IBasicConsumer Consumer { get; set; }
        public string ConsumerTag { get; set; }
        public bool Exclusive { get; set; }
        public string Queue { get; set; }

        public string Recover(IModel channelToUse)
        {
            ConsumerTag = channelToUse.BasicConsume(Queue, AutoAck,
                ConsumerTag, false, Exclusive,
                Arguments, Consumer);

            return ConsumerTag;
        }

        public RecordedConsumer WithArguments(IDictionary<string, object> value)
        {
            Arguments = value;
            return this;
        }

        public RecordedConsumer WithAutoAck(bool value)
        {
            AutoAck = value;
            return this;
        }

        public RecordedConsumer WithConsumer(IBasicConsumer value)
        {
            Consumer = value ?? throw new System.ArgumentNullException(nameof(value));
            return this;
        }

        public RecordedConsumer WithConsumerTag(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new System.ArgumentNullException(nameof(value));
            }
            else
            {
                ConsumerTag = value;
            }
            return this;
        }

        public RecordedConsumer WithExclusive(bool value)
        {
            Exclusive = value;
            return this;
        }

        public RecordedConsumer WithQueue(string value)
        {
            Queue = value ?? throw new System.ArgumentNullException(nameof(value));
            return this;
        }
    }
}
