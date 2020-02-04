// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at https://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.Text;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestUpdateSecret : IntegrationFixture {

        [Test]
        public void TestUpdatingConnectionSecret()
        {
            if (!this.RabbitMQ380OrHigher())
            {
                Console.WriteLine("Not connected to RabbitMQ 3.8 or higher, skipping test");
                return;
            }

            this.Conn.UpdateSecret("new-secret", "Test Case");

            Assert.AreEqual("new-secret", this.ConnFactory.Password);
        }

        private bool RabbitMQ380OrHigher()
        {
            var properties = this.Conn.ServerProperties;

            if (properties.TryGetValue("version", out var versionVal))
            {
                var versionStr = Encoding.UTF8.GetString((byte[])versionVal);
                if (Version.TryParse(versionStr, out var version))
                {
                    return version >= new Version(3, 8);
                }
            }

            return false;
        }
    }
}
