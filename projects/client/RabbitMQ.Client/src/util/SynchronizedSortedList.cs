using System;
using System.Collections;
using System.Collections.Generic;

namespace RabbitMQ.Util
{
    [Serializable]
    internal class SynchronizedSortedList<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private readonly SortedList<TKey, TValue> list;
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
                return ((ICollection<KeyValuePair<TKey, TValue>>)this.list).IsReadOnly;
            }
        }

        public ICollection<TKey> Keys
        {
            get
            {
                lock (this.root)
                    return this.list.Keys;
            }
        }

        public ICollection<TValue> Values
        {
            get
            {
                lock (this.root)
                    return this.list.Values;
            }
        }

        public object SyncRoot 
        {
            get
            {
                return this.root;
            }
        }

        internal SynchronizedSortedList(SortedList<TKey, TValue> list)
        {
            this.list = list;
            this.root = ((ICollection)list).SyncRoot;
        }

        public TValue this[TKey key]
        {
            get
            {
                lock (this.root)
                    return this.list[key];
            }
            set
            {
                lock (this.root)
                    this.list[key] = value;
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (this.root)
                return this.list.TryGetValue(key, out value);
        }

        public bool ContainsKey(TKey key)
        {
            lock (this.root)
                return this.list.ContainsKey(key);
        }

        public bool ContainsValue(TValue value)
        {
            lock (this.root)
                return this.list.ContainsValue(value);
        }

        public void Add(TKey key, TValue value)
        {
            lock (this.root)
                this.list.Add(key, value);
        }

        public bool Remove(TKey key)
        {
            lock (this.root)
                return this.list.Remove(key);
        }

        public void RemoveAt(int index)
        {
            lock (this.root)
                this.list.RemoveAt(index);
        }

        public void Clear()
        {
            lock (this.root)
                this.list.Clear();
        }

        public TKey GetKey(int index)
        {
            lock (this.root)
                return this.list.Keys[index];
        }

        public int IndexOfKey(TKey key)
        {
            lock (this.root)
                return this.list.IndexOfKey(key);
        }

        public int IndexOfValue(TValue value)
        {
            lock (this.root)
                return this.list.IndexOfValue(value);
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
        {
            lock (this.root)
                ((ICollection<KeyValuePair<TKey, TValue>>)this.list).Add(item);
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
        {
            lock (this.root)
                return ((ICollection<KeyValuePair<TKey, TValue>>)this.list).Contains(item);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            lock (this.root)
                return this.list.GetEnumerator();
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            lock (this.root)
                return this.list.GetEnumerator();
        }

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            lock (this.root)
                ((ICollection<KeyValuePair<TKey, TValue>>)this.list).CopyTo(array, arrayIndex);
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
        {
            lock (this.root)
                return ((ICollection<KeyValuePair<TKey, TValue>>)this.list).Remove(item);
        }
    }
}