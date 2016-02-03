// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2016 Pivotal Software, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at http://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace RabbitMQ.Client.Impl
{
    public class RecordedQueue : RecordedNamedEntity
    {
        private IDictionary<string, object> arguments;
        private bool durable;
        private bool exclusive;

        public RecordedQueue(AutorecoveringModel model, string name) : base(model, name)
        {
        }

        public bool IsAutoDelete { get; private set; }
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

        public RecordedQueue Arguments(IDictionary<string, object> value)
        {
            arguments = value;
            return this;
        }

        public RecordedQueue AutoDelete(bool value)
        {
            IsAutoDelete = value;
            return this;
        }

        public RecordedQueue Durable(bool value)
        {
            durable = value;
            return this;
        }

        public RecordedQueue Exclusive(bool value)
        {
            exclusive = value;
            return this;
        }

        public void Recover()
        {
            QueueDeclareOk ok = ModelDelegate.QueueDeclare(NameToUseForRecovery, durable,
                exclusive, IsAutoDelete,
                arguments).GetAwaiter().GetResult();
            Name = ok.QueueName;
        }

        public RecordedQueue ServerNamed(bool value)
        {
            IsServerNamed = value;
            return this;
        }

        public override string ToString()
        {
            return String.Format("{0}: name = '{1}', durable = {2}, exlusive = {3}, autoDelete = {4}, arguments = '{5}'",
                GetType().Name, Name, durable, exclusive, IsAutoDelete, arguments);
        }
    }
}
