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

using NUnit.Framework;

using System;
using System.Threading;

using RabbitMQ.Util;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestBlockingCell : TimingFixture
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
        public void TestGetValueWhichDoesNotTimeOut()
        {
            BlockingCell k = new BlockingCell();
            k.Value = 123;

            ResetTimer();
            var v = k.GetValue(TimingInterval);
            Assert.Greater(SafetyMargin, ElapsedMs());
            Assert.AreEqual(123, v);
        }

        [Test]
        public void TestGetValueWhichDoesTimeOut()
        {
            BlockingCell k = new BlockingCell();
            ResetTimer();
            Assert.Throws<TimeoutException>(() => k.GetValue(TimingInterval));
        }

        [Test]
        public void TestGetValueWhichDoesTimeOutWithTimeSpan()
        {
            BlockingCell k = new BlockingCell();
            ResetTimer();
            Assert.Throws<TimeoutException>(() => k.GetValue(TimeSpan.FromMilliseconds(TimingInterval)));
        }

        [Test]
        public void TestGetValueWithTimeoutInfinite()
        {
            BlockingCell k = new BlockingCell();
            SetAfter(TimingInterval, k, 123);

            ResetTimer();
            var v = k.GetValue(Timeout.Infinite);
            Assert.Less(TimingInterval - SafetyMargin, ElapsedMs());
            Assert.AreEqual(123, v);
        }

        [Test]
        public void TestBackgroundUpdateSucceeds()
        {
            BlockingCell k = new BlockingCell();
            SetAfter(TimingInterval, k, 123);

            ResetTimer();
            var v = k.GetValue(TimingInterval * 2);
            Assert.Less(TimingInterval - SafetyMargin, ElapsedMs());
            Assert.AreEqual(123, v);
        }

        [Test]
        public void TestBackgroundUpdateSucceedsWithTimeSpan()
        {
            BlockingCell k = new BlockingCell();
            SetAfter(TimingInterval, k, 123);

            ResetTimer();
            var v = k.GetValue(TimeSpan.FromMilliseconds(TimingInterval * 2));
            Assert.Less(TimingInterval - SafetyMargin, ElapsedMs());
            Assert.AreEqual(123, v);
        }

        [Test]
        public void TestBackgroundUpdateSucceedsWithInfiniteTimeout()
        {
            BlockingCell k = new BlockingCell();
            SetAfter(TimingInterval, k, 123);

            ResetTimer();
            var v = k.GetValue(Timeout.Infinite);
            Assert.Less(TimingInterval - SafetyMargin, ElapsedMs());
            Assert.AreEqual(123, v);
        }

        [Test]
        public void TestBackgroundUpdateSucceedsWithInfiniteTimeoutTimeSpan()
        {
            BlockingCell k = new BlockingCell();
            SetAfter(TimingInterval, k, 123);

            ResetTimer();
            var v = k.GetValue(Timeout.Infinite);
            Assert.Less(TimingInterval - SafetyMargin, ElapsedMs());
            Assert.AreEqual(123, v);
        }

        [Test]
        public void TestBackgroundUpdateFails()
        {
            BlockingCell k = new BlockingCell();
            SetAfter(TimingInterval * 2, k, 123);

            ResetTimer();
            Assert.Throws<TimeoutException>(() => k.GetValue(TimingInterval));
        }
    }
}
