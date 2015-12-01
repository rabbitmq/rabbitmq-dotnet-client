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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace RabbitMQ.Util
{
    public class BatchingWorkPool<K, V>
    {
        private object lockObject = new object();
        private IDictionary<K, BlockingCollection<V>> pool =
            new Dictionary<K, BlockingCollection<V>>();
        private SetQueue<K> ready = new SetQueue<K>();
        private HashSet<K> inProgress = new HashSet<K>();

        public BatchingWorkPool() {}

        public bool AddWorkItem(K key, V item)
        {
            BlockingCollection<V> q;
            lock(lockObject)
            {
                try
                {
                    q = this.pool[key];
                }
                catch (KeyNotFoundException knfe)
                {
                    return false;
                }
            }

#if NETFX_CORE
            q.Add(item);
#else
            try
            {
                q.Add(item);
            }
            catch (ThreadInterruptedException tie)
            {
                // most likely due to shutdown. ok.
            }
#endif

            lock (lockObject)
            {
                if (IsDormant(key))
                {
                    DormantToReady(key);
                    return true;
                }
            }
            return false;
        }

        public void RegisterKey(K key)
        {
            lock(lockObject)
            {
                var q = new BlockingCollection<V>(new ConcurrentQueue<V>());
                this.pool.Add(key, q);
            }
        }

        public void UnregisterKey(K key)
        {
            lock (lockObject)
            {
                this.pool.Remove(key);
                this.ready.Remove(key);
                this.inProgress.Remove(key);
            }
        }

        public void UnregisterAllKeys()
        {
            lock(lockObject)
            {
                this.pool.Clear();
                this.ready.Clear();
                this.inProgress.Clear();
            }
        }

        public K NextWorkBlock(ref List<V> to, int size)
        {
            lock(lockObject)
            {
                K nextKey = this.ReadyToInProgress();
                if(nextKey != null)
                {
                    var q = this.pool[nextKey];
                    DrainTo(q, ref to, size);
                }
                return nextKey;
            }
        }

        public bool FinishWorkBlock(K key)
        {
            lock (lockObject)
            {
                if (!this.IsRegistered(key))
                {
                    return false;
                }
                if (!this.inProgress.Contains(key))
                {
                    throw new ArgumentException(String.Format("Client {0} not in progress"));
                }

                if (MoreWorkItems(key))
                {
                    InProgressToReady(key);
                    return true;
                }
                else
                {
                    InProgressToDormant(key);
                    return false;
                }
            }
        }

        private int DrainTo(BlockingCollection<V> from, ref List<V> to, int maxElements)
        {
            int n = 0;
            while(n < maxElements)
            {
                V item;
                if(!from.TryTake(out item))
                {
                    break;
                }
                else
                {
                    to.Add(item);
                    ++n;
                }
            }
            return n;
        }

        private bool IsReady(K key)
        {
            return this.ready.Contains(key);
        }

        private bool IsInProgress(K key)
        {
            return this.inProgress.Contains(key);
        }

        private bool IsRegistered(K key)
        {
            return this.pool.ContainsKey(key);
        }

        private bool IsDormant(K key)
        {
            return IsRegistered(key) && !IsInProgress(key) && !IsReady(key);
        }

        private K ReadyToInProgress()
        {
            K key = this.ready.Dequeue();
            if(key == null)
            {
                return default(K);
            } 
            else
            {
                this.inProgress.Add(key);
                return key;
            }
        }

        private void InProgressToReady(K key)
        {
            this.inProgress.Remove(key);
            this.ready.Enqueue(key);
        }

        private void DormantToReady(K key)
        {
            this.ready.Enqueue(key);
        }

        private void InProgressToDormant(K key)
        {
            this.inProgress.Remove(key);
        }

        private bool MoreWorkItems(K key)
        {
            var xs = this.pool[key];
            return ((xs != null) && !(xs.Count == 0));
        }
    }
}
