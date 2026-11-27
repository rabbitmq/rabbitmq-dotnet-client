// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------


using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using RabbitMQ.Client;
using Xunit;

namespace Test.Unit
{
    public class TestAmqpTcpEndpointClone
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ACloneKeepsEverySetting_GH2127(bool withHostname)
        {
            var original = new AmqpTcpEndpoint("original-host", 1234, new SslOption("original-host", enabled: true), 1024)
            {
                AddressFamily = AddressFamily.InterNetworkV6
            };

            AmqpTcpEndpoint clone = Copy(original, withHostname);

            Assert.NotSame(original, clone);
            Assert.Equal(withHostname ? "other-host" : "original-host", clone.HostName);
            // Shared, not copied, until #2130.
            Assert.Same(original.Ssl, clone.Ssl);

            var defaults = new AmqpTcpEndpoint();
            foreach (PropertyInfo property in ComparedProperties())
            {
                // A property this test leaves at its default could be dropped by a copy unnoticed.
                Assert.False(Equals(property.GetValue(defaults), property.GetValue(original)),
                    $"{property.Name} is at its default on the original; give it a non-default value");
                Assert.True(Equals(property.GetValue(original), property.GetValue(clone)),
                    $"{property.Name}: original {property.GetValue(original)}, clone {property.GetValue(clone)}");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ACloneKeepsAnUnsetPortUnset_GH2127(bool withHostname)
        {
            var original = new AmqpTcpEndpoint("original-host");
            Assert.Equal(AmqpTcpEndpoint.UseDefaultPort, original.PortOrMinusOne);

            Assert.Equal(AmqpTcpEndpoint.UseDefaultPort, Copy(original, withHostname).PortOrMinusOne);
        }

        private static AmqpTcpEndpoint Copy(AmqpTcpEndpoint original, bool withHostname)
            => withHostname ? original.CloneWithHostname("other-host") : (AmqpTcpEndpoint)original.Clone();

        private static PropertyInfo[] ComparedProperties()
            => typeof(AmqpTcpEndpoint).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name is not (nameof(AmqpTcpEndpoint.HostName) or nameof(AmqpTcpEndpoint.Ssl) or nameof(AmqpTcpEndpoint.Protocol)))
                .ToArray();
    }
}
