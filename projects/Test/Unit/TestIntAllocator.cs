// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System.Collections.Generic;
using RabbitMQ.Client.Util;
using Xunit;

namespace Test.Unit
{
    public class TestIntAllocator
    {
        [Fact]
        public void TestRandomAllocation()
        {
            int repeatCount = 10000;
            int range = 2048;
            IList<int> allocated = new List<int>();
            IntAllocator intAllocator = new IntAllocator(0, range);
            while (repeatCount-- > 0)
            {
                if (Util.S_Random.Next(2) == 0)
                {
                    int a = intAllocator.Allocate();
                    if (a > -1)
                    {
                        Assert.False(allocated.Contains(a));
                        allocated.Add(a);
                    }
                }
                else if (allocated.Count > 0)
                {
                    int a = allocated[0];
                    intAllocator.Free(a);
                    allocated.RemoveAt(0);
                }
            }
        }

        [Fact]
        public void TestAllocateAll()
        {
            int range = 2048;
            IList<int> allocated = new List<int>();
            IntAllocator intAllocator = new IntAllocator(0, range);
            for (int i = 0; i <= range; i++)
            {
                int a = intAllocator.Allocate();
                Assert.NotEqual(-1, a);
                Assert.False(allocated.Contains(a));
                allocated.Add(a);
            }
        }
    }
}
