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

using System.Collections.Generic;

namespace RabbitMQ.Client.Impl
{
    internal class RecordedQueue : RecordedNamedEntity, IRecordedQueue
    {
        public RecordedQueue(string name) : base(name)
        {
        }

        public bool Durable { get; private set; }

        public bool Exclusive { get; private set; }

        public bool AutoDelete { get; private set; }

        public IDictionary<string, object> Arguments { get; private set; }

        public bool IsServerNamed { get; private set; }

        protected string NameToUseForRecovery
        {
            get
            {
                if (IsServerNamed)
                {
                    return string.Empty;
                }
                else
                {
                    return Name;
                }
            }
        }

        public RecordedQueue WithArguments(IDictionary<string, object> value)
        {
            Arguments = value;
            return this;
        }

        public RecordedQueue WithAutoDelete(bool value)
        {
            AutoDelete = value;
            return this;
        }

        public RecordedQueue WithDurable(bool value)
        {
            Durable = value;
            return this;
        }

        public RecordedQueue WithExclusive(bool value)
        {
            Exclusive = value;
            return this;
        }

        public void Recover(IModel model)
        {
            QueueDeclareOk ok = model.QueueDeclare(NameToUseForRecovery, Durable,
                Exclusive, AutoDelete,
                Arguments);
            Name = ok.QueueName;
        }

        public RecordedQueue WithServerNamed(bool value)
        {
            IsServerNamed = value;
            return this;
        }

        public override string ToString()
        {
            return $"{GetType().Name}: name = '{Name}', durable = {Durable}, exlusive = {Exclusive}, autoDelete = {AutoDelete}, arguments = '{Arguments}'";
        }
    }
}
