// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2010 LShift Ltd., Cohesive Financial
//   Technologies LLC., and Rabbit Technologies Ltd.
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
//   The contents of this file are subject to the Mozilla Public License
//   Version 1.1 (the "License"); you may not use this file except in
//   compliance with the License. You may obtain a copy of the License at
//   http://www.rabbitmq.com/mpl.html
//
//   Software distributed under the License is distributed on an "AS IS"
//   basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
//   License for the specific language governing rights and limitations
//   under the License.
//
//   The Original Code is The RabbitMQ .NET Client.
//
//   The Initial Developers of the Original Code are LShift Ltd,
//   Cohesive Financial Technologies LLC, and Rabbit Technologies Ltd.
//
//   Portions created before 22-Nov-2008 00:00:00 GMT by LShift Ltd,
//   Cohesive Financial Technologies LLC, or Rabbit Technologies Ltd
//   are Copyright (C) 2007-2008 LShift Ltd, Cohesive Financial
//   Technologies LLC, and Rabbit Technologies Ltd.
//
//   Portions created by LShift Ltd are Copyright (C) 2007-2010 LShift
//   Ltd. Portions created by Cohesive Financial Technologies LLC are
//   Copyright (C) 2007-2010 Cohesive Financial Technologies
//   LLC. Portions created by Rabbit Technologies Ltd are Copyright
//   (C) 2007-2010 Rabbit Technologies Ltd.
//
//   All Rights Reserved.
//
//   Contributor(s): ______________________________________.
//
//---------------------------------------------------------------------------
using NUnit.Framework;

using System;
using System.IO;
using System.Collections;
using System.Threading;

using RabbitMQ.Util;

namespace RabbitMQ.Client.Unit
{

    [TestFixture]
    public class TestSharedQueue
    {

        public delegate void Thunk();

        //wrapper to work around C#'s lack of local volatiles
        public class VolatileInt {
            public volatile int v = 0;
        }

        public class DelayedEnqueuer
        {
            public SharedQueue m_q;
            public int m_delayMs;
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
                ExpectEof(new Thunk(this.Dequeue));
            }
        }

        public static void EnqueueAfter(int delayMs, SharedQueue q, object v)
        {
            DelayedEnqueuer de = new DelayedEnqueuer(q, delayMs, v);
            new Thread(new ThreadStart(de.EnqueueValue)).Start();
        }

        public static void ExpectEof(Thunk thunk)
        {
            try
            {
                thunk();
                Assert.Fail("expected System.IO.EndOfStreamException");
            }
            catch (System.IO.EndOfStreamException) { }
        }

        public DateTime m_startTime;

        public void ResetTimer()
        {
            m_startTime = DateTime.Now;
        }

        public int ElapsedMs()
        {
            return (int)((DateTime.Now - m_startTime).TotalMilliseconds);
        }

        [Test]
        public void TestDequeueNoWait1()
        {
            SharedQueue q = new SharedQueue();
            q.Enqueue(1);
            Assert.AreEqual(1, q.DequeueNoWait(0));
            Assert.AreEqual(0, q.DequeueNoWait(0));
        }

        [Test]
        public void TestDequeueNoWait2()
        {
            SharedQueue q = new SharedQueue();
            q.Enqueue(1);
            Assert.AreEqual(1, q.Dequeue());
            Assert.AreEqual(0, q.DequeueNoWait(0));
        }

        [Test]
        public void TestDequeueNoWait3()
        {
            SharedQueue q = new SharedQueue();
            Assert.AreEqual(null, q.DequeueNoWait(null));
        }

        [Test]
        public void TestDelayedEnqueue()
        {
            SharedQueue q = new SharedQueue();
            ResetTimer();
            EnqueueAfter(50, q, 1);
            Assert.AreEqual(0, q.DequeueNoWait(0));
            Assert.Greater(45, ElapsedMs());
            Assert.AreEqual(1, q.Dequeue());
            Assert.Greater(ElapsedMs(), 45);
        }

        [Test]
        public void TestTimeoutShort()
        {
            SharedQueue q = new SharedQueue();
            q.Enqueue(123);

            ResetTimer();
            object v;
            bool r = q.Dequeue(250, out v);
            Assert.Greater(50, ElapsedMs());
            Assert.IsTrue(r);
            Assert.AreEqual(123, v);
        }

        [Test]
        public void TestTimeoutLong()
        {
            SharedQueue q = new SharedQueue();

            ResetTimer();
            object v;
            bool r = q.Dequeue(250, out v);
            Assert.Greater(ElapsedMs(), 200);
            Assert.IsTrue(!r);
            Assert.AreEqual(null, v);
        }

        [Test]
        public void TestTimeoutNegative()
        {
            SharedQueue q = new SharedQueue();

            ResetTimer();
            object v;
            bool r = q.Dequeue(-10000, out v);
            Assert.Greater(50, ElapsedMs());
            Assert.IsTrue(!r);
            Assert.AreEqual(null, v);
        }

        [Test]
        public void TestTimeoutInfinite()
        {
            SharedQueue q = new SharedQueue();
            EnqueueAfter(250, q, 123);

            ResetTimer();
            object v;
            bool r = q.Dequeue(Timeout.Infinite, out v);
            Assert.Greater(ElapsedMs(), 200);
            Assert.IsTrue(r);
            Assert.AreEqual(123, v);
        }

        [Test]
        public void TestBgShort()
        {
            SharedQueue q = new SharedQueue();
            EnqueueAfter(50, q, 123);

            ResetTimer();
            object v;
            bool r = q.Dequeue(100, out v);
            Assert.Greater(ElapsedMs(), 40);
            Assert.IsTrue(r);
            Assert.AreEqual(123, v);
        }

        [Test]
        public void TestBgLong()
        {
            SharedQueue q = new SharedQueue();
            EnqueueAfter(150, q, 123);

            ResetTimer();
            object v;
            bool r = q.Dequeue(100, out v);
            Assert.Greater(110, ElapsedMs());
            Assert.IsTrue(!r);
            Assert.AreEqual(null, v);
        }

        [Test]
        public void TestDoubleBg()
        {
            SharedQueue q = new SharedQueue();
            EnqueueAfter(100, q, 123);
            EnqueueAfter(200, q, 234);

            ResetTimer();
            object v;
            bool r;

            r = q.Dequeue(200, out v);
            Assert.Greater(ElapsedMs(), 80);
            Assert.Greater(120, ElapsedMs());
            Assert.IsTrue(r);
            Assert.AreEqual(123, v);

            r = q.Dequeue(200, out v);
            Assert.Greater(ElapsedMs(), 180);
            Assert.Greater(220, ElapsedMs());
            Assert.IsTrue(r);
            Assert.AreEqual(234, v);
        }

        [Test]
        public void TestDoublePoll()
        {
            SharedQueue q = new SharedQueue();
            EnqueueAfter(100, q, 123);

            ResetTimer();
            object v;
            bool r;

            r = q.Dequeue(50, out v);
            Assert.Greater(ElapsedMs(), 30);
            Assert.Greater(70, ElapsedMs());
            Assert.IsTrue(!r);
            Assert.AreEqual(null, v);

            r = q.Dequeue(100, out v);
            Assert.Greater(ElapsedMs(), 80);
            Assert.Greater(120, ElapsedMs());
            Assert.IsTrue(r);
            Assert.AreEqual(123, v);
        }

        [Test]
        public void TestCloseWhenEmpty()
        {
            DelayedEnqueuer de = new DelayedEnqueuer(new SharedQueue(), 0, 1);
            de.m_q.Close();
            ExpectEof(new Thunk(de.EnqueueValue));
            ExpectEof(new Thunk(de.Dequeue));
            ExpectEof(new Thunk(de.DequeueNoWaitZero));
            ExpectEof(new Thunk(de.DequeueAfterOneIntoV));
        }

        [Test]
        public void TestCloseWhenFull()
        {
            SharedQueue q = new SharedQueue();
            object v;
            q.Enqueue(1);
            q.Enqueue(2);
            q.Enqueue(3);
            q.Close();
            DelayedEnqueuer de = new DelayedEnqueuer(q, 0, 4);
            ExpectEof(new Thunk(de.EnqueueValue));
            Assert.AreEqual(1, q.Dequeue());
            Assert.AreEqual(2, q.DequeueNoWait(0));
            bool r = q.Dequeue(1, out v);
            Assert.IsTrue(r);
            Assert.AreEqual(3, v);
            ExpectEof(new Thunk(de.Dequeue));
        }

        [Test]
        public void TestCloseWhenWaiting()
        {
            SharedQueue q = new SharedQueue();
            DelayedEnqueuer de = new DelayedEnqueuer(q, 0, null);
            Thread t =
            new Thread(new ThreadStart(de.BackgroundEofExpectingDequeue));
            t.Start();
            Thread.Sleep(10);
            q.Close();
            t.Join();
        }

        [Test]
        public void TestEnumerator()
        {
            SharedQueue q = new SharedQueue();
            VolatileInt c1 = new VolatileInt();
            VolatileInt c2 = new VolatileInt();
            Thread t1 = new Thread(delegate() {
                    foreach (int v in q) c1.v+=v;
                });
            Thread t2 = new Thread(delegate() {
                    foreach (int v in q) c2.v+=v;
                });
            t1.Start();
            t2.Start();
            q.Enqueue(1);
            q.Enqueue(2);
            q.Enqueue(3);
            q.Close();
            t1.Join();
            t2.Join();
            Assert.AreEqual(6, c1.v + c2.v);
        }

    }

}
