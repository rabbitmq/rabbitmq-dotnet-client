// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2013 VMware, Inc.
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
//  The Initial Developer of the Original Code is VMware, Inc.
//  Copyright (c) 2007-2013 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace RabbitMQ.Util {
    ///<summary>A thread-safe shared queue implementation.</summary>
    public class SharedQueue : SharedQueue<object>
    {}

    ///<summary>A thread-safe shared queue implementation.</summary>
    public class SharedQueue<T> : IEnumerable<T> {
        ///<summary>The shared queue.</summary>
        ///<remarks>
        ///Subclasses must ensure appropriate locking discipline when
        ///accessing this field. See the implementation of Enqueue,
        ///Dequeue.
        ///</remarks>
        protected Queue<T> m_queue = new Queue<T>();

        ///<summary>Flag holding our current status.</summary>
        protected bool m_isOpen = true;

        ///<summary>Construct a fresh, empty SharedQueue.</summary>
        public SharedQueue() {
        }

        ///<summary>Close the queue. Causes all further Enqueue()
        ///operations to throw EndOfStreamException, and all pending
        ///or subsequent Dequeue() operations to throw an
        ///EndOfStreamException once the queue is empty.</summary>
        public void Close() {
            lock (m_queue) {
                m_isOpen = false;
                Monitor.PulseAll(m_queue);
            }
        }

        ///<summary>Call only when the lock on m_queue is held.</summary>
        /// <exception cref="EndOfStreamException" />
        private void EnsureIsOpen() {
            if (!m_isOpen) {
                throw new EndOfStreamException("SharedQueue closed");
            }
        }

        ///<summary>Place an item at the end of the queue.</summary>
        ///<remarks>
        ///If there is a thread waiting for an item to arrive, the
        ///waiting thread will be woken, and the newly Enqueued item
        ///will be passed to it. If the queue is closed on entry to
        ///this method, EndOfStreamException will be thrown.
        ///</remarks>
        public void Enqueue(T o) {
            lock (m_queue) {
                EnsureIsOpen();
                m_queue.Enqueue(o);
                Monitor.Pulse(m_queue);
            }
        }
    
        ///<summary>Retrieve the first item from the queue, or block if none available</summary>
        ///<remarks>
        ///Callers of Dequeue() will block if no items are available
        ///until some other thread calls Enqueue() or the queue is
        ///closed. In the latter case this method will throw
        ///EndOfStreamException.
        ///</remarks>
        public T Dequeue() {
            lock (m_queue) {
                while (m_queue.Count == 0) {
                    EnsureIsOpen();
                    Monitor.Wait(m_queue);
                }
                return m_queue.Dequeue();
            }
        }

        ///<summary>Retrieve the first item from the queue, or return
        ///defaultValue immediately if no items are
        ///available</summary>
        ///<remarks>
        ///<para>
        /// If one or more objects are present in the queue at the
        /// time of the call, the first item is removed from the queue
        /// and returned. Otherwise, the defaultValue that was passed
        /// in is returned immediately. This defaultValue may be null,
        /// or in cases where null is part of the range of the queue,
        /// may be some other sentinel object. The difference between
        /// DequeueNoWait() and Dequeue() is that DequeueNoWait() will
        /// not block when no items are available in the queue,
        /// whereas Dequeue() will.
        ///</para>
        ///<para>
        /// If at the time of call the queue is empty and in a
        /// closed state (following a call to Close()), then this
        /// method will throw EndOfStreamException.
        ///</para>
        ///</remarks>
        public T DequeueNoWait(T defaultValue) {
            lock (m_queue) {
                if (m_queue.Count == 0) {
                    EnsureIsOpen();
                    return defaultValue;
                } else {
                    return m_queue.Dequeue();
                }
            }
        }

        ///<summary>Retrieve the first item from the queue, or return
        ///nothing if no items are available after the given
        ///timeout</summary>
        ///<remarks>
        ///<para>
        /// If one or more items are present on the queue at the time
        /// the call is made, the call will return
        /// immediately. Otherwise, the calling thread blocks until
        /// either an item appears on the queue, or
        /// millisecondsTimeout milliseconds have elapsed.
        ///</para>
        ///<para>
        /// Returns true in the case that an item was available before
        /// the timeout, in which case the out parameter "result" is
        /// set to the item itself.
        ///</para>
        ///<para>
        /// If no items were available before the timeout, returns
        /// false, and sets "result" to null.
        ///</para>
        ///<para>
        /// A timeout of -1 (i.e. System.Threading.Timeout.Infinite)
        /// will be interpreted as a command to wait for an
        /// indefinitely long period of time for an item to become
        /// available. Usage of such a timeout is equivalent to
        /// calling Dequeue() with no arguments. See also the MSDN
        /// documentation for
        /// System.Threading.Monitor.Wait(object,int).
        ///</para>
        ///<para>
        /// If no items are present and the queue is in a closed
        /// state, or if at any time while waiting the queue
        /// transitions to a closed state (by a call to Close()), this
        /// method will throw EndOfStreamException.
        ///</para>
        ///</remarks>
        public bool Dequeue(int millisecondsTimeout, out T result) {
            if (millisecondsTimeout == Timeout.Infinite) {
                result = Dequeue();
                return true;
            }

            DateTime startTime = DateTime.Now;
            lock (m_queue) {
                while (m_queue.Count == 0) {
                    EnsureIsOpen();
                    int elapsedTime = (int) ((DateTime.Now - startTime).TotalMilliseconds);
                    int remainingTime = millisecondsTimeout - elapsedTime;
                    if (remainingTime <= 0) {
                        result = default(T);
                        return false;
                    }

                    Monitor.Wait(m_queue, remainingTime);
                }

                result = m_queue.Dequeue();
                return true;
            }
        }

        ///<summary>Implementation of the IEnumerable interface, for
        ///permitting SharedQueue to be used in foreach
        ///loops.</summary>
        IEnumerator IEnumerable.GetEnumerator() {
            return new SharedQueueEnumerator<T>(this);
        }

        ///<summary>Implementation of the IEnumerable interface, for
        ///permitting SharedQueue to be used in foreach
        ///loops.</summary>
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new SharedQueueEnumerator<T>(this);
        }

    }

    ///<summary>Implementation of the IEnumerator interface, for
    ///permitting SharedQueue to be used in foreach loops.</summary>
    public struct SharedQueueEnumerator<T> : IEnumerator<T> {

        private SharedQueue<T> m_queue;
        private T m_current;

        ///<summary>Construct an enumerator for the given
        ///SharedQueue.</summary>
        public SharedQueueEnumerator(SharedQueue<T> queue) {
            m_queue = queue;
            m_current = default(T);
        }

        object IEnumerator.Current {
            get {
                if (m_current == null) {
                    throw new InvalidOperationException();
                }
                return m_current;
            }
        }

        T IEnumerator<T>.Current
        {
            get
            {
                if (m_current == null)
                {
                    throw new InvalidOperationException();
                }
                return m_current;
            }
        }

        bool IEnumerator.MoveNext() {
            try {
                m_current = m_queue.Dequeue();
                return true;
            } catch (EndOfStreamException) {
                m_current = default(T);
                return false;
            }
        }

        ///<summary>Reset()ting a SharedQueue doesn't make sense, so
        ///this method always throws
        ///InvalidOperationException.</summary>
        void IEnumerator.Reset() {
            throw new InvalidOperationException("SharedQueue.Reset() does not make sense");
        }

        public void Dispose()
        {
        }
    }

}
