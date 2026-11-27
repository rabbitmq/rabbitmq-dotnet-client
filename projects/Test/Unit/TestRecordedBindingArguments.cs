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


using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client.Impl;
using Xunit;

namespace Test.Unit
{
    public class TestRecordedBindingArguments
    {
        public static TheoryData<string, IDictionary<string, object>, IDictionary<string, object>, bool> Cases => new()
        {
            { "same content", new Dictionary<string, object> { ["k"] = "v" }, new Dictionary<string, object> { ["k"] = "v" }, true },
            { "key order", new Dictionary<string, object> { ["a"] = 1, ["b"] = 2 }, new Dictionary<string, object> { ["b"] = 2, ["a"] = 1 }, true },
            // Both encode as the same AMQP long string, so the broker sees one binding.
            { "string and its bytes", new Dictionary<string, object> { ["k"] = "v" }, new Dictionary<string, object> { ["k"] = Encoding.UTF8.GetBytes("v") }, true },
            { "null and empty", null, new Dictionary<string, object>(), true },
            { "different value", new Dictionary<string, object> { ["k"] = "v" }, new Dictionary<string, object> { ["k"] = "w" }, false },
            { "different type", new Dictionary<string, object> { ["k"] = 1 }, new Dictionary<string, object> { ["k"] = 1L }, false },
            { "extra key", new Dictionary<string, object> { ["k"] = "v" }, new Dictionary<string, object> { ["k"] = "v", ["j"] = "v" }, false },
        };

        [Theory]
        [MemberData(nameof(Cases))]
        public void BindingsCompareTheirArgumentsByEncodedContent_GH2147(string description, IDictionary<string, object> left,
            IDictionary<string, object> right, bool equal)
        {
            var a = new RecordedBinding(true, "q", "x", "rk", left);
            var b = new RecordedBinding(true, "q", "x", "rk", right);

            Assert.True(equal == a.Equals(b), description);
            if (equal)
            {
                Assert.Equal(a.GetHashCode(), b.GetHashCode());
                Assert.False(new HashSet<RecordedBinding> { a }.Add(b), description);
            }
        }
    }
}
