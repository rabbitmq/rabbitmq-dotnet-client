// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2009 LShift Ltd., Cohesive Financial
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
//   Portions created by LShift Ltd are Copyright (C) 2007-2009 LShift
//   Ltd. Portions created by Cohesive Financial Technologies LLC are
//   Copyright (C) 2007-2009 Cohesive Financial Technologies
//   LLC. Portions created by Rabbit Technologies Ltd are Copyright
//   (C) 2007-2009 Rabbit Technologies Ltd.
//
//   All Rights Reserved.
//
//   Contributor(s): ______________________________________.
//
//---------------------------------------------------------------------------
using NUnit.Framework;

using System;
using System.Collections;
using System.Threading;

using RabbitMQ.Util;

[TestFixture]
public class TestSemaphore {
    public ArrayList m_trace;
    public RabbitMQ.Util.Semaphore m_sem;
    public RabbitMQ.Util.Semaphore m_done;

    [SetUp]
    public void SetUp() {
        m_trace = new ArrayList();
        m_sem = new RabbitMQ.Util.Semaphore();
        m_done = new RabbitMQ.Util.Semaphore(-1);
    }

    public void Trace(string marker) {
        lock (this) {
            //Console.Error.WriteLine("Trace: " + marker);
            m_trace.Add(marker);
        }
    }

    public class SingleCycle {
        public TestSemaphore m_semTest;
        public string m_name;
        public int[] m_sleepTimes;

        public SingleCycle(TestSemaphore semTest, string name, int[] sleepTimes) {
            m_semTest = semTest;
            m_name = name;
            m_sleepTimes = sleepTimes;
            new Thread(new ThreadStart(MainLoop)).Start();
        }

        public void MainLoop() {
            m_semTest.SingleCycleMainLoop(m_name, m_sleepTimes);
        }
    }

    public void SingleCycleMainLoop(string name, int[] sleepTimes) {

        Thread.Sleep(sleepTimes[0]);

        Trace(name+" PreAcquire");
        if (!m_sem.TryWait()) {
            Trace(name+" TryWait false");
            m_sem.Wait();
        }
        Trace(name+" PostAcquire");

        Thread.Sleep(sleepTimes[1]);

        Trace(name+" PreRelease");
        m_sem.Release();
        Trace(name+" PostRelease");

        m_done.Release();
    }

    public void AssertTrace(params string[] expectedTrace) {
        string[] actualTrace = (string[]) m_trace.ToArray(typeof(string));
        try {
            Assert.AreEqual(expectedTrace, actualTrace);
        } catch {
            Console.WriteLine();
            Console.WriteLine("EXPECTED ==================================================");
            DebugUtil.DumpProperties(expectedTrace, Console.Out, 0);
            Console.WriteLine("ACTUAL ====================================================");
            DebugUtil.DumpProperties(actualTrace, Console.Out, 0);
            Console.WriteLine("===========================================================");
            throw;
        }
    }

    [Test]
    public void TestSequentialAB() {
        new SingleCycle(this, "A", new int[] { 0, 0, });
        new SingleCycle(this, "B", new int[] { 100, 0 });
        m_done.Wait();
        AssertTrace("A PreAcquire",
                    "A PostAcquire",
                    "A PreRelease",
                    "A PostRelease",
                    "B PreAcquire",
                    "B PostAcquire",
                    "B PreRelease",
                    "B PostRelease");
    }

    [Test]
    public void TestSequentialBA() {
        new SingleCycle(this, "A", new int[] { 100, 0 });
        new SingleCycle(this, "B", new int[] { 0, 0 });
        m_done.Wait();
        AssertTrace("B PreAcquire",
                    "B PostAcquire",
                    "B PreRelease",
                    "B PostRelease",
                    "A PreAcquire",
                    "A PostAcquire",
                    "A PreRelease",
                    "A PostRelease");
    }

    [Test]
    public void TestInterleaveABAB() {
        new SingleCycle(this, "A", new int[] { 0, 200 });
        new SingleCycle(this, "B", new int[] { 100, 200 });
        m_done.Wait();
        AssertTrace("A PreAcquire",
                    "A PostAcquire",
                    "B PreAcquire",
                    "B TryWait false",
                    "A PreRelease",
                    "A PostRelease",
                    "B PostAcquire",
                    "B PreRelease",
                    "B PostRelease");
    }
}
