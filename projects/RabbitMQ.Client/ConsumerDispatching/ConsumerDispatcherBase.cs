// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.ConsumerDispatching
{
    internal abstract class ConsumerDispatcherBase
    {
        private static readonly FallbackConsumer s_fallbackConsumer = new FallbackConsumer();
        private readonly ConcurrentDictionary<string, IAsyncBasicConsumer> _consumers = new ConcurrentDictionary<string, IAsyncBasicConsumer>();

        public IAsyncBasicConsumer? DefaultConsumer { get; set; }

        protected ConsumerDispatcherBase()
        {
        }

        protected void AddConsumer(IAsyncBasicConsumer consumer, string tag)
        {
            _consumers[tag] = consumer;
        }

        protected IAsyncBasicConsumer GetConsumerOrDefault(string tag)
        {
            return _consumers.TryGetValue(tag, out IAsyncBasicConsumer? consumer) ? consumer : GetDefaultOrFallbackConsumer();
        }

        public IAsyncBasicConsumer GetAndRemoveConsumer(string tag)
        {
            return _consumers.Remove(tag, out IAsyncBasicConsumer? consumer) ? consumer : GetDefaultOrFallbackConsumer();
        }

        public Task ShutdownAsync(ShutdownEventArgs reason)
        {
            DoShutdownConsumers(reason);
            return InternalShutdownAsync();
        }

        private void DoShutdownConsumers(ShutdownEventArgs reason)
        {
            foreach (KeyValuePair<string, IAsyncBasicConsumer> pair in _consumers.ToArray())
            {
                ShutdownConsumer(pair.Value, reason);
            }
            _consumers.Clear();
        }

        protected abstract void ShutdownConsumer(IAsyncBasicConsumer consumer, ShutdownEventArgs reason);

        protected abstract Task InternalShutdownAsync();

        // Do not inline as it's not the default case on a hot path
        [MethodImpl(MethodImplOptions.NoInlining)]
        private IAsyncBasicConsumer GetDefaultOrFallbackConsumer()
        {
            return DefaultConsumer ?? s_fallbackConsumer;
        }
    }
}
