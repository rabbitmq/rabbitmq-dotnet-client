// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2013 GoPivotal, Inc.
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
//  Copyright (c) 2013-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;

namespace RabbitMQ.Util
{
    [Serializable]
    internal class SynchronizedList<T> : IList<T>
    {
        private readonly IList<T> list;
        private readonly object root;

        public int Count
        {
            get
            {
                lock (this.root)
                    return this.list.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return this.list.IsReadOnly;
            }
        }

        public T this[int index]
        {
            get
            {
                lock (this.root)
                    return this.list[index];
            }
            set
            {
                lock (this.root)
                    this.list[index] = value;
            }
        }

        internal SynchronizedList(IList<T> list)
        {
            this.list = list;
            this.root = new Object();
        }

        public void Add(T item)
        {
            lock (this.root)
                this.list.Add(item);
        }

        public void Clear()
        {
            lock (this.root)
                this.list.Clear();
        }

        public bool Contains(T item)
        {
            lock (this.root)
                return this.list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (this.root)
                this.list.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            lock (this.root)
                return this.list.Remove(item);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            lock (this.root)
                return this.list.GetEnumerator();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            lock (this.root)
                return this.list.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            lock (this.root)
                return this.list.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            lock (this.root)
                this.list.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            lock (this.root)
                this.list.RemoveAt(index);
        }
    }
}
