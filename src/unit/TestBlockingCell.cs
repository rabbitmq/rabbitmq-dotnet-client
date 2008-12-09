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
using System.IO;
using System.Collections;
using System.Threading;

using RabbitMQ.Util;

[TestFixture]
public class TestBlockingCell {
    public class DelayedSetter {
	public BlockingCell k;
	public int delayMs;
	public object v;
	public void Run() {
	    Thread.Sleep(delayMs);
	    k.Value = v;
	}
    }

    public static void SetAfter(int delayMs, BlockingCell k, object v) {
	DelayedSetter ds = new DelayedSetter();
	ds.k = k;
	ds.delayMs = delayMs;
	ds.v = v;
	new Thread(new ThreadStart(ds.Run)).Start();
    }

    public DateTime startTime;

    public void ResetTimer() {
	startTime = DateTime.Now;
    }

    public int ElapsedMs() {
	return (int) ((DateTime.Now - startTime).TotalMilliseconds);
    }

    [Test]
    public void TestSetBeforeGet() {
	BlockingCell k = new BlockingCell();
	k.Value = 123;
	Assert.AreEqual(123, k.Value);
    }

    [Test]
    public void TestTimeoutShort() {
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
    public void TestTimeoutLong() {
	BlockingCell k = new BlockingCell();

	ResetTimer();
	object v;
	bool r = k.GetValue(250, out v);
	Assert.Greater(ElapsedMs(), 200);
	Assert.IsTrue(!r);
	Assert.AreEqual(null, v);
    }

    [Test]
    public void TestTimeoutNegative() {
	BlockingCell k = new BlockingCell();

	ResetTimer();
	object v;
	bool r = k.GetValue(-10000, out v);
	Assert.Greater(50, ElapsedMs());
	Assert.IsTrue(!r);
	Assert.AreEqual(null, v);
    }

    [Test]
    public void TestTimeoutInfinite() {
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
    public void TestBgShort() {
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
    public void TestBgLong() {
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
