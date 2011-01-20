// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2011 VMware, Inc.
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
//  Copyright (c) 2007-2011 VMware, Inc.  All rights reserved.
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
    public class TestBlockingCell
    {
        public class DelayedSetter
        {
            public BlockingCell m_k;
            public int m_delayMs;
            public object m_v;
            public void Run()
            {
                Thread.Sleep(m_delayMs);
                m_k.Value = m_v;
            }
        }

        public static void SetAfter(int delayMs, BlockingCell k, object v)
        {
            DelayedSetter ds = new DelayedSetter();
            ds.m_k = k;
            ds.m_delayMs = delayMs;
            ds.m_v = v;
            new Thread(new ThreadStart(ds.Run)).Start();
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
        public void TestSetBeforeGet()
        {
            BlockingCell k = new BlockingCell();
            k.Value = 123;
            Assert.AreEqual(123, k.Value);
        }

        [Test]
        public void TestTimeoutShort()
        {
            BlockingCell k = new BlockingCell();
            k.Value = 123;

            ResetTimer();
            object v;
            bool r = k.GetValue(250, out v);
            Assert.Greater(50, ElapsedMs());
            Assert.IsTrue(r);
            Assert.AreEqual(123, v);
        }

        [Test]
        public void TestTimeoutLong()
        {
            BlockingCell k = new BlockingCell();

            ResetTimer();
            object v;
            bool r = k.GetValue(250, out v);
            Assert.Greater(ElapsedMs(), 200);
            Assert.IsTrue(!r);
            Assert.AreEqual(null, v);
        }

        [Test]
        public void TestTimeoutNegative()
        {
            BlockingCell k = new BlockingCell();

            ResetTimer();
            object v;
            bool r = k.GetValue(-10000, out v);
            Assert.Greater(50, ElapsedMs());
            Assert.IsTrue(!r);
            Assert.AreEqual(null, v);
        }

        [Test]
        public void TestTimeoutInfinite()
        {
            BlockingCell k = new BlockingCell();
            SetAfter(250, k, 123);

            ResetTimer();
            object v;
            bool r = k.GetValue(Timeout.Infinite, out v);
            Assert.Greater(ElapsedMs(), 200);
            Assert.IsTrue(r);
            Assert.AreEqual(123, v);
        }

        [Test]
        public void TestBgShort()
        {
            BlockingCell k = new BlockingCell();
            SetAfter(50, k, 123);

            ResetTimer();
            object v;
            bool r = k.GetValue(100, out v);
            Assert.Greater(ElapsedMs(), 40);
            Assert.IsTrue(r);
            Assert.AreEqual(123, v);
        }

        [Test]
        public void TestBgLong()
        {
            BlockingCell k = new BlockingCell();
            SetAfter(150, k, 123);

            ResetTimer();
            object v;
            bool r = k.GetValue(100, out v);
            Assert.Greater(110, ElapsedMs());
            Assert.IsTrue(!r);
            Assert.AreEqual(null, v);
        }
    }
}
