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

using System.Collections.Concurrent;
using System.Collections.Generic;

namespace RabbitMQ.Util
{
    public class BatchingWorkPool<K, V>
    {
        private ConcurrentDictionary<K, ConcurrentQueue<V>> pool = new ConcurrentDictionary<K, ConcurrentQueue<V>>();
        private ConcurrentQueue<K> ready = new ConcurrentQueue<K>();

        public bool AddWorkItem(K key, V item)
        {
            ConcurrentQueue<V> q;

            if (!pool.TryGetValue(key, out q))
            {
                return false;
            }

            q.Enqueue(item);
            ready.Enqueue(key);

            return true;
        }

        public void RegisterKey(K key)
        {
            var q = new ConcurrentQueue<V>();
            pool.TryAdd(key, q);
        }

        public void UnregisterKey(K key)
        {
            ConcurrentQueue<V> value;
            pool.TryRemove(key, out value);
        }

        public void UnregisterAllKeys()
        {
            pool.Clear();
        }

        public K NextWorkBlock(ref List<V> to, int size)
        {
            K nextKey;
            K result = default(K);
            var dequeue = true;

            while (dequeue)
            {
                dequeue = ready.TryDequeue(out nextKey);

                if (dequeue)
                {
                    ConcurrentQueue<V> q;

                    if (!pool.TryGetValue(nextKey, out q))
                    {
                        continue;
                    }

                    dequeue = false;

                    var count = DrainTo(q, ref to, size);

                    if (count != 0)
                    {
                        result = nextKey;
                    }
                }
            }

            return result;
        }

        public bool FinishWorkBlock(K key)
        {
            ConcurrentQueue<V> q;

            if (!pool.TryGetValue(key, out q))
            {
                return false;
            }

            if (!q.IsEmpty)
            {
                ready.Enqueue(key);
                return true;
            }

            return false;
        }

        private int DrainTo(ConcurrentQueue<V> from, ref List<V> to, int maxElements)
        {
            int n = 0;

            while (n < maxElements)
            {
                V item;

                if (!from.TryDequeue(out item))
                {
                    break;
                }

                to.Add(item);
                ++n;
            }

            return n;
        }
    }
}
