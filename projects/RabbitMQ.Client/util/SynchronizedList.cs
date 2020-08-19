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
//  Copyright (c) 2013-2020 VMware, Inc. or its affiliates.  All rights reserved.
//---------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;

namespace RabbitMQ.Util
{
    internal class SynchronizedList<T> : IList<T>
    {
        private readonly IList<T> _list;

        internal SynchronizedList()
            : this(new List<T>())
        {
        }

        internal SynchronizedList(IList<T> list)
        {
            _list = list;
            SyncRoot = new object();
        }

        public int Count
        {
            get
            {
                lock (SyncRoot)
                    return _list.Count;
            }
        }

        public bool IsReadOnly
        {
            get { return _list.IsReadOnly; }
        }

        public T this[int index]
        {
            get
            {
                lock (SyncRoot)
                    return _list[index];
            }
            set
            {
                lock (SyncRoot)
                    _list[index] = value;
            }
        }

        public object SyncRoot { get; }

        public void Add(T item)
        {
            lock (SyncRoot)
                _list.Add(item);
        }

        public void Clear()
        {
            lock (SyncRoot)
                _list.Clear();
        }

        public bool Contains(T item)
        {
            lock (SyncRoot)
                return _list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (SyncRoot)
                _list.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            lock (SyncRoot)
                return _list.Remove(item);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            lock (SyncRoot)
                return _list.GetEnumerator();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            lock (SyncRoot)
                return _list.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            lock (SyncRoot)
                return _list.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            lock (SyncRoot)
                _list.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            lock (SyncRoot)
                _list.RemoveAt(index);
        }
    }
}
