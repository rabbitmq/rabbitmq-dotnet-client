// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
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
// The MPL v2.0:
//
//---------------------------------------------------------------------------
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Diagnostics;

namespace RabbitMQ.Util
{
    /**
   * A class for allocating integer IDs in a given range.
   */
    internal class IntAllocator
    {
        private readonly int[] _unsorted;
        private IntervalList _base;
        private int _unsortedCount = 0;

        /**
     * A class representing a list of inclusive intervals
     */

        /**
     * Creates an IntAllocator allocating integer IDs within the inclusive range [start, end]
     */

        public IntAllocator(int start, int end)
        {
            if (start > end)
            {
                throw new ArgumentException($"illegal range [{start}, {end}]");
            }

            // Fairly arbitrary heuristic for a good size for the unsorted set.
            _unsorted = new int[Math.Max(32, (int)Math.Sqrt(end - start))];
            _base = new IntervalList(start, end);
        }

        /**
     * Allocate a fresh integer from the range, or return -1 if no more integers
     * are available. This operation is guaranteed to run in O(1)
     */

        public int Allocate()
        {
            if (_unsortedCount > 0)
            {
                return _unsorted[--_unsortedCount];
            }
            else if (_base != null)
            {
                int result = _base.Start;
                if (_base.Start == _base.End)
                {
                    _base = _base.Next;
                }
                else
                {
                    _base.Start++;
                }
                return result;
            }
            else
            {
                return -1;
            }
        }

        /**
     * Make the provided integer available for allocation again. This operation
     * runs in amortized O(sqrt(range size)) time: About every sqrt(range size)
     * operations  will take O(range_size + number of intervals) to complete and
     * the rest run in constant time.
     *
     * No error checking is performed, so if you double Free or Free an integer
     * that was not originally Allocated the results are undefined. Sorry.
     */

        public void Free(int id)
        {
            if (_unsortedCount >= _unsorted.Length)
            {
                Flush();
            }
            _unsorted[_unsortedCount++] = id;
        }

        public bool Reserve(int id)
        {
            // We always flush before reserving because the only way to determine
            // if an ID is in the unsorted array is through a linear scan. This leads
            // us to the potentially expensive situation where there is a large unsorted
            // array and we reserve several IDs, incurring the cost of the scan each time.
            // Flushing makes sure the array is always empty and does no additional work if
            // reserve is called twice.
            Flush();

            IntervalList current = _base;

            while (current != null)
            {
                if (current.End < id)
                {
                    current = current.Next;
                    continue;
                }
                else if (current.Start > id)
                {
                    return false;
                }
                else if (current.End == id)
                {
                    current.End--;
                }
                else if (current.Start == id)
                {
                    current.Start++;
                }
                else
                {
                    // The ID is in the middle of this interval.
                    // We need to split the interval into two.
                    var rest = new IntervalList(id + 1, current.End);
                    current.End = id - 1;
                    rest.Next = current.Next;
                    current.Next = rest;
                }
                return true;
            }
            return false;
        }

        private void Flush()
        {
            if (_unsortedCount > 0)
            {
                _base = IntervalList.Merge(_base, IntervalList.FromArray(_unsorted, _unsortedCount));
                _unsortedCount = 0;
            }
        }


        public class IntervalList
        {
            public int End;

            // Invariant: If Next != Null then Next.Start > this.End + 1
            public IntervalList Next;
            public int Start;

            public IntervalList(int start, int end)
            {
                Start = start;
                End = end;
            }

            // Destructively merge two IntervalLists.
            // Invariant: None of the Intervals in the two lists may overlap
            // intervals in this list.

            public static IntervalList FromArray(int[] xs, int length)
            {
                Array.Sort(xs, 0, length);

                IntervalList result = null;
                IntervalList current = null;

                int i = 0;
                while (i < length)
                {
                    int start = i;
                    while ((i < length - 1) && (xs[i + 1] == xs[i] + 1))
                    {
                        i++;
                    }

                    var interval = new IntervalList(xs[start], xs[i]);

                    if (result is null)
                    {
                        result = interval;
                        current = interval;
                    }
                    else
                    {
                        current.Next = interval;
                        current = interval;
                    }
                    i++;
                }
                return result;
            }

            public static IntervalList Merge(IntervalList x, IntervalList y)
            {
                if (x is null)
                {
                    return y;
                }
                if (y is null)
                {
                    return x;
                }

                if (x.End > y.Start)
                {
                    return Merge(y, x);
                }

                Debug.Assert(x.End != y.Start);

                // We now have x, y non-null and x.End < y.Start.

                if (y.Start == x.End + 1)
                {
                    // The two intervals adjoin. Merge them into one and then
                    // merge the tails.
                    x.End = y.End;
                    x.Next = Merge(x.Next, y.Next);
                    return x;
                }

                // y belongs in the tail of x.

                x.Next = Merge(y, x.Next);
                return x;
            }
        }
    }
}
