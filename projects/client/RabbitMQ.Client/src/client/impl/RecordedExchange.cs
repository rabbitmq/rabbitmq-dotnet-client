// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2015 Pivotal Software, Inc.
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
//  Copyright (c) 2007-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace RabbitMQ.Client.Impl
{
    public class RecordedExchange : RecordedNamedEntity
    {
        private string type;

        public RecordedExchange(AutorecoveringModel model, string name) : base(model, name)
        {
        }

        public IDictionary<string, object> Arguments { get; private set; }
        public bool Durable { get; private set; }
        public bool IsAutoDelete { get; private set; }

        public string Type
        {
            get { return type; }
        }

        public void Recover()
        {
            ModelDelegate.ExchangeDeclare(Name, type, Durable, IsAutoDelete, Arguments);
        }

        public override string ToString()
        {
            return String.Format("{0}: name = '{1}', type = '{2}', durable = {3}, autoDelete = {4}, arguments = '{5}'",
                GetType().Name, Name, type, Durable, IsAutoDelete, Arguments);
        }

        public RecordedExchange WithArguments(IDictionary<string, object> value)
        {
            Arguments = value;
            return this;
        }

        public RecordedExchange WithAutoDelete(bool value)
        {
            IsAutoDelete = value;
            return this;
        }

        public RecordedExchange WithDurable(bool value)
        {
            Durable = value;
            return this;
        }

        public RecordedExchange WithType(string value)
        {
            type = value;
            return this;
        }
    }
}
