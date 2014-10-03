// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2014 GoPivotal, Inc.
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
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;

using RabbitMQ.Client;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    public class RecordedQueue : RecordedNamedEntity
    {
        private bool durable;
        private bool exclusive;
        private bool autoDelete;
        private IDictionary<string, object> arguments;

        private bool serverNamed;


        public RecordedQueue(AutorecoveringModel model, string name) : base(model, name) {}


        public bool IsServerNamed
        {
            get { return this.serverNamed; }
        }

        public bool IsAutoDelete
        {
            get { return this.autoDelete; }
        }

        protected string NameToUseForRecovery
        {
            get
            {
                if(IsServerNamed)
                {
                    return string.Empty;
                } else
                {
                    return this.name;
                }
            }
        }

        public RecordedQueue Durable(bool value)
        {
            this.durable = value;
            return this;
        }

        public RecordedQueue Exclusive(bool value)
        {
            this.exclusive = value;
            return this;
        }

        public RecordedQueue AutoDelete(bool value)
        {
            this.autoDelete = value;
            return this;
        }

        public RecordedQueue Arguments(IDictionary<string, object> value)
        {
            this.arguments = value;
            return this;
        }

        public RecordedQueue ServerNamed(bool value)
        {
            this.serverNamed = value;
            return this;
        }

        public void Recover()
        {
            var ok    = ModelDelegate.QueueDeclare(NameToUseForRecovery, this.durable,
                                                   this.exclusive, this.autoDelete,
                                                   this.arguments);
            this.name = ok.QueueName;
        }

        public override string ToString()
        {
            return String.Format("{0}: name = '{1}', durable = {2}, exlusive = {3}, autoDelete = {4}, arguments = '{5}'",
                                 this.GetType().Name, name, durable, exclusive, autoDelete, arguments);
        }
    }
}