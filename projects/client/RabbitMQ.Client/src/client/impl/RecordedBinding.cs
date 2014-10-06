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
    public abstract class RecordedBinding : RecordedEntity
    {
        protected string source;
        protected string destination;
        protected string routingKey;
        protected IDictionary<string, object> arguments;

        public RecordedBinding(AutorecoveringModel model) : base(model) {}

        public string Source
        {
            get { return this.source; }
        }

        public string Destination
        {
            get { return this.destination; }
            set { this.destination = value; }
        }

        public string RoutingKey
        {
            get { return this.routingKey; }
        }

        public IDictionary<string, object> Arguments
        {
            get { return this.arguments; }
        }

        public RecordedBinding WithSource(string value)
        {
            this.source = value;
            return this;
        }

        public RecordedBinding WithDestination(string value)
        {
            this.destination = value;
            return this;
        }

        public RecordedBinding WithRoutingKey(string value)
        {
            this.routingKey = value;
            return this;
        }

        public RecordedBinding WithArguments(IDictionary<string, object> value)
        {
            this.arguments = value;
            return this;
        }

        public bool Equals(RecordedBinding other)
        {
            if (Object.ReferenceEquals(other, null))
            {
                return false;
            }

            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            return (this.Source.Equals(other.Source)) &&
                (this.Destination.Equals(other.Destination)) &&
                (this.RoutingKey.Equals(other.RoutingKey)) &&
                (this.arguments == other.arguments);
        }

        public virtual void Recover() {}

        public override int GetHashCode()
        {
            return source.GetHashCode() ^
                destination.GetHashCode() ^
                routingKey.GetHashCode() ^
                (arguments != null ? arguments.GetHashCode() : 0);
        }

        public override string ToString()
        {
            return String.Format("{0}: source = '{1}', destination = '{2}', routingKey = '{3}', arguments = '{4}'",
                                 this.GetType().Name, source, destination, routingKey, arguments);
        }
    }

    public class RecordedQueueBinding : RecordedBinding
    {
        public RecordedQueueBinding(AutorecoveringModel model) : base(model) {}

        public override void Recover()
        {
            this.ModelDelegate.QueueBind(this.destination, this.source,
                                         this.routingKey, this.arguments);
        }
    }

    public class RecordedExchangeBinding : RecordedBinding
    {
        public RecordedExchangeBinding(AutorecoveringModel model) : base(model) {}

        public override void Recover()
        {
            this.ModelDelegate.ExchangeBind(this.destination, this.source,
                                            this.routingKey, this.arguments);
        }
    }
}