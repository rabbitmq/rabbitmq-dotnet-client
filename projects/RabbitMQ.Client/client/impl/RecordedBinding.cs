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
    internal abstract class RecordedBinding : IRecordedBinding
    {
        public IDictionary<string, object> Arguments { get; protected set; }
        public string Destination { get; set; }
        public string RoutingKey { get; protected set; }
        public string Source { get; protected set; }

        public bool Equals(RecordedBinding other)
        {
            return other != null &&
                Source.Equals(other.Source) &&
                Destination.Equals(other.Destination) &&
                RoutingKey.Equals(other.RoutingKey) &&
                (Arguments == other.Arguments);
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            var other = obj as RecordedBinding;

            return Equals(other);
        }

        public override int GetHashCode()
        {
            return Source.GetHashCode() ^
                   Destination.GetHashCode() ^
                   RoutingKey.GetHashCode() ^
                   (Arguments != null ? Arguments.GetHashCode() : 0);
        }

        public virtual void Recover(IModel model)
        {
        }

        public override string ToString()
        {
            return $"{GetType().Name}: source = '{Source}', destination = '{Destination}', routingKey = '{RoutingKey}', arguments = '{Arguments}'";
        }

        public RecordedBinding WithArguments(IDictionary<string, object> value)
        {
            Arguments = value;
            return this;
        }

        public RecordedBinding WithDestination(string value)
        {
            Destination = value;
            return this;
        }

        public RecordedBinding WithRoutingKey(string value)
        {
            RoutingKey = value;
            return this;
        }

        public RecordedBinding WithSource(string value)
        {
            Source = value;
            return this;
        }
    }

    internal sealed class RecordedQueueBinding : RecordedBinding
    {
        public override void Recover(IModel model)
        {
            model.QueueBind(Destination, Source, RoutingKey, Arguments);
        }
    }


    internal sealed class RecordedExchangeBinding : RecordedBinding
    {
        public override void Recover(IModel model)
        {
            model.ExchangeBind(Destination, Source, RoutingKey, Arguments);
        }
    }
}
