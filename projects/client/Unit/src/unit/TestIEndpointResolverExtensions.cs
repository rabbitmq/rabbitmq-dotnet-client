// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2016 Pivotal Software, Inc.
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
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;

namespace RabbitMQ.Client.Unit
{
    public class TestEndpointResolver : IEndpointResolver
    {
        private IEnumerable<AmqpTcpEndpoint> endpoints;
        public TestEndpointResolver (IEnumerable<AmqpTcpEndpoint> endpoints)
        {
            this.endpoints = endpoints;
        }

        public IEnumerable<AmqpTcpEndpoint> All()
        {
            return endpoints;
        }
    }

    class TestEndpointException : Exception
    {
        public TestEndpointException(string message) : base(message)
        {
        }
    }

    public class TestIEndpointResolverExtensions
    {
        [Test]
        public void SelectOneShouldReturnDefaultWhenThereAreNoEndpoints()
        {
            var ep = new TestEndpointResolver(new List<AmqpTcpEndpoint>());
            Assert.IsNull(ep.SelectOne<AmqpTcpEndpoint>((x) => null));
        }

        [Test]
        public void SelectOneShouldRaiseThrownExceptionWhenThereAreOnlyInaccessibleEndpoints()
        {
            var ep = new TestEndpointResolver(new List<AmqpTcpEndpoint> { new AmqpTcpEndpoint()});
            Assert.Throws<TestEndpointException>(() => ep.SelectOne<AmqpTcpEndpoint>((x) => { throw new TestEndpointException("bananas"); }));
        }

        [Test]
        public void SelectOneShouldReturnFoundEndpoint()
        {
            var ep = new TestEndpointResolver(new List<AmqpTcpEndpoint> { new AmqpTcpEndpoint()});
            Assert.IsNotNull(ep.SelectOne<AmqpTcpEndpoint>((e) => e));
        }
    }
}