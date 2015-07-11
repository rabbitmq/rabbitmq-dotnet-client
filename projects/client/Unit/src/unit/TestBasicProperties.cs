// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2011-2014 GoPivotal, Inc.
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
//  Copyright (c) 2011-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;

namespace RabbitMQ.Client.Unit
{

    [TestFixture]
    class TestBasicProperties
    {
        [Test]
        public void TestPersistentPropertyChangesDeliveryMode_PersistentTrueDelivery2()
        {
            // Arrange
            var subject = new Framing.BasicProperties();

            // Act
            subject.Persistent = true;

            // Assert
            Assert.AreEqual(2, subject.DeliveryMode);
            Assert.AreEqual(true, subject.Persistent);
        }

        [Test]
        public void TestPersistentPropertyChangesDeliveryMode_PersistentFalseDelivery1()
        {
            // Arrange
            var subject = new Framing.BasicProperties();

            // Act
            subject.Persistent = false;

            // Assert
            Assert.AreEqual(1, subject.DeliveryMode);
            Assert.AreEqual(false, subject.Persistent);
        }


#pragma warning disable CS0618 // Type or member is obsolete
        [Test]
        public void TestSetPersistentMethodChangesDeliveryMode_PersistentTrueDelivery2()
        {
            // Arrange
            var subject = new Framing.BasicProperties();

            // Act
            subject.SetPersistent(true);

            // Assert
            Assert.AreEqual(2, subject.DeliveryMode);
            Assert.AreEqual(true, subject.Persistent);
        }


        [Test]
        public void TestSetPersistentMethodChangesDeliveryMode_PersistentFalseDelivery1()
        {
            // Arrange
            var subject = new Framing.BasicProperties();

            // Act
            subject.SetPersistent(false);

            // Assert
            Assert.AreEqual(1, subject.DeliveryMode);
            Assert.AreEqual(false, subject.Persistent);
        }
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
