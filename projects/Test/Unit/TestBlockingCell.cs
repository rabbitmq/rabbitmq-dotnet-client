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
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Threading;
using RabbitMQ.Util;
using Xunit;

namespace Test.Unit
{
    public class TestBlockingCell : TimingFixture
    {
        internal class DelayedSetter<T>
        {
            public BlockingCell<T> m_k;
            public TimeSpan m_delay;
            public T m_v;
            public void Run()
            {
                Thread.Sleep(m_delay);
                m_k.ContinueWithValue(m_v);
            }
        }

        internal static void SetAfter<T>(TimeSpan delay, BlockingCell<T> k, T v)
        {
            var ds = new DelayedSetter<T>
            {
                m_k = k,
                m_delay = delay,
                m_v = v
            };
            new Thread(new ThreadStart(ds.Run)).Start();
        }

        public DateTime m_startTime;

        private void ResetTimer()
        {
            m_startTime = DateTime.Now;
        }

        public TimeSpan ElapsedMs()
        {
            return DateTime.Now - m_startTime;
        }

        [Fact]
        public void TestSetBeforeGet()
        {
            var k = new BlockingCell<int>();
            k.ContinueWithValue(123);
            Assert.Equal(123, k.WaitForValue());
        }

        [Fact]
        public void TestGetValueWhichDoesNotTimeOut()
        {
            var k = new BlockingCell<int>();
            k.ContinueWithValue(123);

            ResetTimer();
            int v = k.WaitForValue(TimingInterval);
            Assert.True(SafetyMargin > ElapsedMs());
            Assert.Equal(123, v);
        }

        [Fact]
        public void TestGetValueWhichDoesTimeOut()
        {
            var k = new BlockingCell<int>();
            ResetTimer();
            Assert.Throws<TimeoutException>(() => k.WaitForValue(TimingInterval));
        }

        [Fact]
        public void TestGetValueWhichDoesTimeOutWithTimeSpan()
        {
            var k = new BlockingCell<int>();
            ResetTimer();
            Assert.Throws<TimeoutException>(() => k.WaitForValue(TimingInterval));
        }

        [Fact]
        public void TestBackgroundUpdateSucceedsWithTimeSpan()
        {
            var k = new BlockingCell<int>();
            SetAfter(TimingInterval, k, 123);

            ResetTimer();
            int v = k.WaitForValue(TimingInterval_2X);
            Assert.True(TimingInterval - SafetyMargin < ElapsedMs());
            Assert.Equal(123, v);
        }

        [Fact]
        public void TestBackgroundUpdateSucceedsWithInfiniteTimeoutTimeSpan()
        {
            var k = new BlockingCell<int>();
            SetAfter(TimingInterval, k, 123);

            ResetTimer();
            TimeSpan infiniteTimeSpan = Timeout.InfiniteTimeSpan;
            int v = k.WaitForValue(infiniteTimeSpan);
            Assert.True(TimingInterval - SafetyMargin < ElapsedMs());
            Assert.Equal(123, v);
        }

        [Fact]
        public void TestBackgroundUpdateFails()
        {
            var k = new BlockingCell<int>();
            SetAfter(TimingInterval_16X, k, 123);

            ResetTimer();
            Assert.Throws<TimeoutException>(() => k.WaitForValue(TimingInterval));
        }
    }
}
