namespace RabbitMQ.Util
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

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
            this.root = ((ICollection)list).SyncRoot;
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