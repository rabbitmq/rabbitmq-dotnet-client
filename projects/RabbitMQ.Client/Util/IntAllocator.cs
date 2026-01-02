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

using System;
using System.Collections;

namespace RabbitMQ.Client.Util
{
    /// <summary>
    /// <see href="https://github.com/rabbitmq/rabbitmq-java-client/blob/main/src/main/java/com/rabbitmq/utility/IntAllocator.java"/>
    /// </summary>
    internal class IntAllocator
    {
        private readonly int _loRange; // the integer that bit 0 represents
        private readonly int _hiRange; // one more than the integer the highest bit represents
        private readonly int _numberOfBits; //

        /// <summary>
        /// A bit is SET/true in _freeSet if the corresponding integer is FREE
        /// A bit is UNSET/false in freeSet if the corresponding integer is ALLOCATED
        /// </summary>
        private readonly BitArray _freeSet;

        /// <summary>
        /// Creates an IntAllocator allocating integer IDs within the
        /// inclusive range [<c>bottom</c>, <c>top</c>].
        /// </summary>
        /// <param name="bottom">lower end of range</param>
        /// <param name="top">upper end of range (incusive)</param>
        /// <exception cref="ArgumentException"></exception>
        public IntAllocator(int bottom, int top)
        {
            if (bottom > top)
            {
                throw new ArgumentException($"illegal range [{bottom}, {top}]");
            }

            _loRange = bottom;
            _hiRange = top + 1;
            _numberOfBits = _hiRange - _loRange;
            _freeSet = new BitArray(_numberOfBits, true); // All integers are FREE initially
        }

        public int Allocate()
        {
            int setIndex = nextSetBit();
            if (setIndex < 0) // no free integers are available
            {
                return -1;
            }
            _freeSet.Set(setIndex, false);
            return setIndex + _loRange;
        }

        /// <summary>
        /// Makes the provided integer available for allocation again.
        /// </summary>
        /// <param name="reservation">the previously allocated integer to free</param>
        public void Free(int reservation)
        {
            int setIndex = reservation - _loRange;
            _freeSet.Set(setIndex, true); // true means "unallocated"
        }

        /// <summary>
        /// Note: this is different than the Java implementation, because we need to
        /// preserve the prior behavior of always reserving low integers, if available.
        /// See <c>Test.Integration.AllocateAfterFreeingMany</c>
        /// </summary>
        /// <returns>index of the next unallocated bit</returns>
        private int nextSetBit()
        {
            for (int i = 0; i < _freeSet.Count; i++)
            {
                if (_freeSet.Get(i)) // true means "unallocated"
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
