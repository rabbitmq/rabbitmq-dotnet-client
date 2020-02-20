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
//  Copyright (c) 2013-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;

namespace RabbitMQ.Util
{
    internal class SynchronizedList<T> : IList<T>
    {
        private readonly IList<T> _list;
        private readonly object _root;

        internal SynchronizedList()
            : this(new List<T>())
        {
        }

        internal SynchronizedList(IList<T> list)
        {
            _list = list;
            _root = new object();
        }

        public int Count
        {
            get
            {
                lock (_root)
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
                lock (_root)
                    return _list[index];
            }
            set
            {
                lock (_root)
                    _list[index] = value;
            }
        }

        public object SyncRoot
        {
            get { return _root; }
        }

        public void Add(T item)
        {
            lock (_root)
                _list.Add(item);
        }

        public void Clear()
        {
            lock (_root)
                _list.Clear();
        }

        public bool Contains(T item)
        {
            lock (_root)
                return _list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (_root)
                _list.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            lock (_root)
                return _list.Remove(item);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            lock (_root)
                return _list.GetEnumerator();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            lock (_root)
                return _list.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            lock (_root)
                return _list.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            lock (_root)
                _list.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            lock (_root)
                _list.RemoveAt(index);
        }
    }
}
