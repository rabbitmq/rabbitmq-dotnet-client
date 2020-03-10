// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at https://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client.src.util
{
    /// <summary>
    /// A concurrent queue where Dequeue waits for something to be inserted if the queue is empty and Enqueue signals
    ///   that something has been added. Similar in function to a BlockingCollection but with async/await
    ///   support.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class AsyncConcurrentQueue<T>
    {
        private readonly ConcurrentQueue<T> _internalQueue = new ConcurrentQueue<T>();
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);

        /// <summary>
        /// Returns a Task that is completed when an object can be returned from the beginning of the queue.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<T> DequeueAsync(CancellationToken token = default)
        {
            await _semaphore.WaitAsync(token).ConfigureAwait(false);

            if (!_internalQueue.TryDequeue(out var command))
            {
                throw new InvalidOperationException("Internal queue empty despite signaled enqueue.");
            }

            return command;
        }

        /// <summary>
        /// Add an object to the end of the queue.
        /// </summary>
        /// <param name="item"></param>
        public void Enqueue(T item)
        {
            _internalQueue.Enqueue(item);
            _semaphore.Release();
        }
    }
}
