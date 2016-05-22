// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2011-2016 Pivotal Software, Inc.
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
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2011-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;

using RabbitMQ.Util;
using NUnit.Framework;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestBatchingWorkPool
    {
        protected BatchingWorkPool<string, object> workPool = new BatchingWorkPool<string, object>();

        [SetUp]
        public void Init()
        {
            this.workPool = new BatchingWorkPool<string, object>();
        }

        [Test]
        public void TestUnknownKey()
        {
            Assert.IsFalse(workPool.AddWorkItem("unknown-key", new object()), "AddWorkItem should return false for unregistered keys");
        }

        [Test]
        public void TestBasicInOut()
        {
            var one = new object();
            var two = new object();
            var k = "TestBasicInOut";

            this.workPool.RegisterKey(k);
            Assert.IsTrue(this.workPool.AddWorkItem(k, one), "AddWorkItem should return true for new registered keys");
            Assert.IsFalse(this.workPool.AddWorkItem(k, two), "AddWorkItem should return false for existent keys");

            var workList = new List<object>(16);
            var key = this.workPool.NextWorkBlock(ref workList, 1);
            Assert.AreEqual(k, key);
            Assert.AreEqual(1, workList.Count, "Work list length should be 1");
            Assert.AreEqual(one, workList[0]);
            Assert.IsTrue(workPool.FinishWorkBlock(key));

            workList.Clear();
            key = this.workPool.NextWorkBlock(ref workList, 1);
            Assert.AreEqual(k, key);
            Assert.AreEqual(1, workList.Count, "Work list length should be 1");
            Assert.AreEqual(two, workList[0]);

            Assert.IsFalse(workPool.FinishWorkBlock(key));
            Assert.IsNull(workPool.NextWorkBlock(ref workList, 1));
        }

        [Test]
        public void TestWorkInWhileInProgress()
        {
            var one = new object();
            var two = new object();
            var k   = "TestWorkInWhileInProgress";

            this.workPool.RegisterKey(k);
            Assert.IsTrue(this.workPool.AddWorkItem(k, one));

            var workList = new List<object>(16);
            var key = this.workPool.NextWorkBlock(ref workList, 1);
            Assert.AreEqual(k, key);
            Assert.AreEqual(1, workList.Count, "Work list length should be 1");
            Assert.AreEqual(one, workList[0]);

            Assert.IsFalse(this.workPool.AddWorkItem(k, two));
            Assert.IsTrue(workPool.FinishWorkBlock(key));

            workList.Clear();

            key = this.workPool.NextWorkBlock(ref workList, 1);
            Assert.AreEqual(k, key);
            Assert.AreEqual(1, workList.Count, "Work list length should be 1");
            Assert.AreEqual(two, workList[0]);
        }

        [Test]
        public void TestInterleavingKeys()
        {
            var one = new object();
            var two = new object();
            var three = new object();
            var k1 = "TestInterleavingKeys1";
            var k2 = "TestInterleavingKeys2";

            this.workPool.RegisterKey(k1);
            this.workPool.RegisterKey(k2);

            Assert.IsTrue(this.workPool.AddWorkItem(k1, one));
            Assert.IsTrue(this.workPool.AddWorkItem(k2, two));
            Assert.IsFalse(this.workPool.AddWorkItem(k1, three));

            var workList = new List<object>(16);
            var key = this.workPool.NextWorkBlock(ref workList, 3);
            Assert.AreEqual(k1, key);
            Assert.AreEqual(2, workList.Count, "Work list length should be 1");
            Assert.AreEqual(one, workList[0]);
            Assert.AreEqual(three, workList[1]);

            workList.Clear();

            key = this.workPool.NextWorkBlock(ref workList, 2);
            Assert.AreEqual(k2, key);
            Assert.AreEqual(1, workList.Count, "Work list length should be 1");
            Assert.AreEqual(two, workList[0]);
        }

        [Test]
        public void TestUnregisterKey()
        {
            var one = new object();
            var two = new object();
            var three = new object();
            var k1 = "TestUnregisterKey1";
            var k2 = "TestUnregisterKey2";

            this.workPool.RegisterKey(k1);
            this.workPool.RegisterKey(k2);

            Assert.IsTrue(this.workPool.AddWorkItem(k1, one));
            Assert.IsTrue(this.workPool.AddWorkItem(k2, two));
            Assert.IsFalse(this.workPool.AddWorkItem(k1, three));

            this.workPool.UnregisterKey(k1);

            var workList = new List<object>(16);
            var key = this.workPool.NextWorkBlock(ref workList, 3);
            Assert.AreEqual(k2, key);
            Assert.AreEqual(1, workList.Count, "Work list length should be 1");
            Assert.AreEqual(two, workList[0]);
        }

        [Test]
        public void TestUnregisterAllKeys()
        {
            var one = new object();
            var two = new object();
            var three = new object();
            var k1 = "TestUnregisterKey1";
            var k2 = "TestUnregisterKey2";

            this.workPool.RegisterKey(k1);
            this.workPool.RegisterKey(k2);

            Assert.IsTrue(this.workPool.AddWorkItem(k1, one));
            Assert.IsTrue(this.workPool.AddWorkItem(k2, two));
            Assert.IsFalse(this.workPool.AddWorkItem(k1, three));

            this.workPool.UnregisterAllKeys();

            var workList = new List<object>(16);
            Assert.IsNull(this.workPool.NextWorkBlock(ref workList, 1));
            Assert.AreEqual(0, workList.Count);
        }
    }
}
