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
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestSharedQueue : TimingFixture
    {
        //wrapper to work around C#'s lack of local volatiles
        public class VolatileInt
        {
            public volatile int v = 0;
        }

        public class DelayedEnqueuer
        {
            public int m_delayMs;
            public SharedQueue m_q;
            public object m_v;

            public DelayedEnqueuer(SharedQueue q, int delayMs, object v)
            {
                m_q = q;
                m_delayMs = delayMs;
                m_v = v;
            }

            public void EnqueueValue()
            {
                Thread.Sleep(m_delayMs);
                m_q.Enqueue(m_v);
            }

            public void Dequeue()
            {
                m_q.Dequeue();
            }

            public void DequeueNoWaitZero()
            {
                m_q.DequeueNoWait(0);
            }

            public void DequeueAfterOneIntoV()
            {
                m_q.Dequeue(1, out m_v);
            }

            public void BackgroundEofExpectingDequeue()
            {
                ExpectEof(Dequeue);
            }
        }

        public static void EnqueueAfter(int delayMs, SharedQueue queue, object v)
        {
            var enqueuer = new DelayedEnqueuer(queue, delayMs, v);
            new Thread(enqueuer.EnqueueValue).Start();
        }

        public static void ExpectEof(Action action)
        {
            try
            {
                action();
                Assert.Fail("expected System.IO.EndOfStreamException");
            }
            catch (EndOfStreamException)
            {
            }
        }

        public DateTime m_startTime;

        public void ResetTimer()
        {
            m_startTime = DateTime.Now;
        }

        public int ElapsedMs()
        {
            return (int) ((DateTime.Now - m_startTime).TotalMilliseconds);
        }

        [Test]
        public void TestBgLong()
        {
            var q = new SharedQueue();
            EnqueueAfter(TimingInterval*2, q, 123);

            ResetTimer();
            object v;
            bool r = q.Dequeue(TimingInterval, out v);
            Assert.Greater(TimingInterval + SafetyMargin, ElapsedMs());
            Assert.IsTrue(!r);
            Assert.AreEqual(null, v);
        }

        [Test]
        public void TestBgShort()
        {
            var q = new SharedQueue();
            EnqueueAfter(TimingInterval, q, 123);

            ResetTimer();
            object v;
            bool r = q.Dequeue(TimingInterval*2, out v);
            Assert.Less(TimingInterval - SafetyMargin, ElapsedMs());
            Assert.IsTrue(r);
            Assert.AreEqual(123, v);
        }

        [Test]
        public void TestCloseWhenEmpty()
        {
            var de = new DelayedEnqueuer(new SharedQueue(), 0, 1);
            de.m_q.Close();
            ExpectEof(de.EnqueueValue);
            ExpectEof(de.Dequeue);
            ExpectEof(de.DequeueNoWaitZero);
            ExpectEof(de.DequeueAfterOneIntoV);
        }

        [Test]
        public void TestCloseWhenFull()
        {
            var q = new SharedQueue();
            object v;
            q.Enqueue(1);
            q.Enqueue(2);
            q.Enqueue(3);
            q.Close();
            var de = new DelayedEnqueuer(q, 0, 4);
            ExpectEof(de.EnqueueValue);
            Assert.AreEqual(1, q.Dequeue());
            Assert.AreEqual(2, q.DequeueNoWait(0));
            bool r = q.Dequeue(1, out v);
            Assert.IsTrue(r);
            Assert.AreEqual(3, v);
            ExpectEof(de.Dequeue);
        }

        [Test]
        public void TestCloseWhenWaiting()
        {
            var q = new SharedQueue();
            var de = new DelayedEnqueuer(q, 0, null);
            var t =
                new Thread(de.BackgroundEofExpectingDequeue);
            t.Start();
            Thread.Sleep(SafetyMargin);
            q.Close();
            t.Join();
        }

        [Test]
        public void TestDelayedEnqueue()
        {
            var q = new SharedQueue();
            ResetTimer();
            EnqueueAfter(TimingInterval, q, 1);
            Assert.AreEqual(0, q.DequeueNoWait(0));
            Assert.Greater(TimingInterval - SafetyMargin, ElapsedMs());
            Assert.AreEqual(1, q.Dequeue());
            Assert.Less(TimingInterval - SafetyMargin, ElapsedMs());
        }

        [Test]
        public void TestDequeueNoWait1()
        {
            var queue = new SharedQueue();
            queue.Enqueue(1);
            Assert.AreEqual(1, queue.DequeueNoWait(0));
            Assert.AreEqual(0, queue.DequeueNoWait(0));
        }

        [Test]
        public void TestDequeueNoWait2()
        {
            var q = new SharedQueue();
            q.Enqueue(1);
            Assert.AreEqual(1, q.Dequeue());
            Assert.AreEqual(0, q.DequeueNoWait(0));
        }

        [Test]
        public void TestDequeueNoWait3()
        {
            var q = new SharedQueue();
            Assert.AreEqual(null, q.DequeueNoWait(null));
        }

        [Test]
        public void TestDoubleBg()
        {
            var q = new SharedQueue();
            EnqueueAfter(TimingInterval, q, 123);
            EnqueueAfter(TimingInterval*2, q, 234);

            ResetTimer();
            object v;
            bool r;

            r = q.Dequeue(TimingInterval*2, out v);
            Assert.Less(TimingInterval - SafetyMargin, ElapsedMs());
            Assert.Greater(TimingInterval + SafetyMargin, ElapsedMs());
            Assert.IsTrue(r);
            Assert.AreEqual(123, v);

            r = q.Dequeue(TimingInterval*2, out v);
            Assert.Less(TimingInterval*2 - SafetyMargin, ElapsedMs());
            Assert.Greater(TimingInterval*2 + SafetyMargin, ElapsedMs());
            Assert.IsTrue(r);
            Assert.AreEqual(234, v);
        }

        [Test]
        public void TestDoublePoll()
        {
            var q = new SharedQueue();
            EnqueueAfter(TimingInterval*2, q, 123);

            ResetTimer();
            object v;
            bool r;

            r = q.Dequeue(TimingInterval, out v);
            Assert.Less(TimingInterval - SafetyMargin, ElapsedMs());
            Assert.Greater(TimingInterval + SafetyMargin, ElapsedMs());
            Assert.IsTrue(!r);
            Assert.AreEqual(null, v);

            r = q.Dequeue(TimingInterval*2, out v);
            Assert.Less(TimingInterval*2 - SafetyMargin, ElapsedMs());
            Assert.Greater(TimingInterval*2 + SafetyMargin, ElapsedMs());
            Assert.IsTrue(r);
            Assert.AreEqual(123, v);
        }

        [Test]
        public void TestEnumerator()
        {
            var queue = new SharedQueue();
            var c1 = new VolatileInt();
            var c2 = new VolatileInt();
            var thread1 = new Thread(() => { foreach (int v in queue) c1.v += v; });
            var thread2 = new Thread(() => { foreach (int v in queue) c2.v += v; });
            thread1.Start();
            thread2.Start();
            queue.Enqueue(1);
            queue.Enqueue(2);
            queue.Enqueue(3);
            queue.Close();
            thread1.Join();
            thread2.Join();
            Assert.AreEqual(6, c1.v + c2.v);
        }

        [Test]
        public void TestTimeoutInfinite()
        {
            var q = new SharedQueue();
            EnqueueAfter(TimingInterval, q, 123);

            ResetTimer();
            object v;
            bool r = q.Dequeue(Timeout.Infinite, out v);
            Assert.Less(TimingInterval - SafetyMargin, ElapsedMs());
            Assert.IsTrue(r);
            Assert.AreEqual(123, v);
        }

        [Test]
        public void TestTimeoutLong()
        {
            var q = new SharedQueue();

            ResetTimer();
            object v;
            bool r = q.Dequeue(TimingInterval, out v);
            Assert.Less(TimingInterval - SafetyMargin, ElapsedMs());
            Assert.IsTrue(!r);
            Assert.AreEqual(null, v);
        }

        [Test]
        public void TestTimeoutNegative()
        {
            var q = new SharedQueue();

            ResetTimer();
            object v;
            bool r = q.Dequeue(-10000, out v);
            Assert.Greater(SafetyMargin, ElapsedMs());
            Assert.IsTrue(!r);
            Assert.AreEqual(null, v);
        }

        [Test]
        public void TestTimeoutShort()
        {
            var q = new SharedQueue();
            q.Enqueue(123);

            ResetTimer();
            object v;
            bool r = q.Dequeue(TimingInterval, out v);
            Assert.Greater(SafetyMargin, ElapsedMs());
            Assert.IsTrue(r);
            Assert.AreEqual(123, v);
        }
    }
}
