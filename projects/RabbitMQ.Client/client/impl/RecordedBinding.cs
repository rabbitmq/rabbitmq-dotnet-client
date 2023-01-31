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
#nullable enable
    internal readonly struct RecordedBinding : IEquatable<RecordedBinding>
    {
        private readonly bool _isQueueBinding;
        private readonly string _destination;
        private readonly string _source;
        private readonly string _routingKey;
        private readonly IReadOnlyDictionary<string, object>? _arguments;

        public string Destination => _destination;
        public string Source => _source;

        public RecordedBinding(bool isQueueBinding, string destination, string source, string routingKey, IReadOnlyDictionary<string, object>? arguments)
        {
            _isQueueBinding = isQueueBinding;
            _destination = destination;
            _source = source;
            _routingKey = routingKey;
            _arguments = arguments;
        }

        public RecordedBinding(string destination, in RecordedBinding old)
        {
            _isQueueBinding = old._isQueueBinding;
            _destination = destination;
            _source = old._source;
            _routingKey = old._routingKey;
            _arguments = old._arguments;
        }

        public void Recover(IModel channel)
        {
            if (_isQueueBinding)
            {
                channel.QueueBind(_destination, _source, _routingKey, _arguments);
            }
            else
            {
                channel.ExchangeBind(_destination, _source, _routingKey, _arguments);
            }
        }

        public bool Equals(RecordedBinding other)
        {
            return _isQueueBinding == other._isQueueBinding && _destination == other._destination && _source == other._source &&
                   _routingKey == other._routingKey && _arguments == other._arguments;
        }

        public override bool Equals(object? obj)
        {
            return obj is RecordedBinding other && Equals(other);
        }

        public override int GetHashCode()
        {
#if NETSTANDARD
            unchecked
            {
                int hashCode = _isQueueBinding.GetHashCode();
                hashCode = (hashCode * 397) ^ _destination.GetHashCode();
                hashCode = (hashCode * 397) ^ _source.GetHashCode();
                hashCode = (hashCode * 397) ^ _routingKey.GetHashCode();
                hashCode = (hashCode * 397) ^ (_arguments != null ? _arguments.GetHashCode() : 0);
                return hashCode;
            }
#else
            return HashCode.Combine(_isQueueBinding, _destination, _source, _routingKey, _arguments);
#endif
        }

        public override string ToString()
        {
            return $"{nameof(RecordedBinding)}: isQueueBinding = '{_isQueueBinding}', source = '{_isQueueBinding}', destination = '{_destination}', routingKey = '{_routingKey}', arguments = '{_arguments}'";
        }
    }
}
