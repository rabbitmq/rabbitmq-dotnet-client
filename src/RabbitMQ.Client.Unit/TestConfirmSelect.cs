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
using System.Text;

namespace RabbitMQ.Client.Unit {
    [TestFixture]
    public class TestConfirmSelect : IntegrationFixture {

        [Test]
        public void TestConfirmSelectIdempotency()
        {
            Model.ConfirmSelect();
            Assert.AreEqual(1, Model.NextPublishSeqNo);
            Publish();
            Assert.AreEqual(2, Model.NextPublishSeqNo);
            Publish();
            Assert.AreEqual(3, Model.NextPublishSeqNo);

            Model.ConfirmSelect();
            Publish();
            Assert.AreEqual(4, Model.NextPublishSeqNo);
            Publish();
            Assert.AreEqual(5, Model.NextPublishSeqNo);
            Publish();
            Assert.AreEqual(6, Model.NextPublishSeqNo);
        }

        protected void Publish()
        {
            Model.BasicPublish("", "amq.fanout", null, encoding.GetBytes("message"));
        }
    }
}
