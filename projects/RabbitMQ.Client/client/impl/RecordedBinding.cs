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
using System.Threading.Tasks;
using RabbitMQ.Client.client.impl.Channel;

namespace RabbitMQ.Client.Impl
{
    #nullable enable
    internal abstract class RecordedBinding : RecordedEntity
    {
        protected RecordedBinding(AutorecoveringChannel channel, string destination, string source, string routingKey, IDictionary<string, object>? arguments)
            : base(channel)
        {
            Destination = destination;
            Source = source;
            RoutingKey = routingKey;
            Arguments = arguments;
        }

        public IDictionary<string, object>? Arguments { get; }
        public string Destination { get; set; }
        public string RoutingKey { get; }
        public string Source { get; }

        protected bool Equals(RecordedBinding other)
        {
            return Equals(Arguments, other.Arguments) && Destination == other.Destination && RoutingKey == other.RoutingKey && Source == other.Source;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((RecordedBinding) obj);
        }

        public override int GetHashCode()
        {
#if NETSTANDARD
            unchecked
            {
                int hashCode = (Arguments != null ? Arguments.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Destination.GetHashCode();
                hashCode = (hashCode * 397) ^ RoutingKey.GetHashCode();
                hashCode = (hashCode * 397) ^ Source.GetHashCode();
                return hashCode;
            }
#else
            return HashCode.Combine(Arguments, Destination, RoutingKey, Source);
#endif
        }

        public override string ToString()
        {
            return $"{GetType().Name}: source = '{Source}', destination = '{Destination}', routingKey = '{RoutingKey}', arguments = '{Arguments}'";
        }
    }

    internal sealed class RecordedQueueBinding : RecordedBinding
    {
        public RecordedQueueBinding(AutorecoveringChannel channel, string destination, string source, string routingKey, IDictionary<string, object>? arguments)
            : base(channel, destination,  source, routingKey, arguments)
        {
        }

        public override ValueTask RecoverAsync()
        {
            return Channel.BindQueueAsync(Destination, Source, RoutingKey, Arguments);
        }
    }

    internal sealed class RecordedExchangeBinding : RecordedBinding
    {
        public RecordedExchangeBinding(AutorecoveringChannel channel, string destination, string source, string routingKey, IDictionary<string, object>? arguments)
            : base(channel, destination,  source, routingKey, arguments)
        {
        }

        public override ValueTask RecoverAsync()
        {
            return Channel.BindExchangeAsync(Destination, Source, RoutingKey, Arguments);
        }
    }
}
