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

namespace RabbitMQ.Util
{
    ///<summary>Used internally by class Either.</summary>
    internal enum EitherAlternative
    {
        Left,
        Right
    }


    ///<summary>Models the disjoint union of two alternatives, a
    ///"left" alternative and a "right" alternative.</summary>
    ///<remarks>Borrowed from ML, Haskell etc.</remarks>
    internal class Either<L, R>
    {
        ///<summary>Private constructor. Use the static methods Left, Right instead.</summary>
        private Either(EitherAlternative alternative, L valueL, R valueR)
        {
            Alternative = alternative;
            LeftValue = valueL;
            RightValue = valueR;
        }

        ///<summary>Retrieve the alternative represented by this instance.</summary>
        public EitherAlternative Alternative { get; private set; }

        ///<summary>Retrieve the value carried by this instance.</summary>
        public L LeftValue { get; private set; }
        public R RightValue { get; private set; }

        ///<summary>Constructs an Either instance representing a Left alternative.</summary>
        public static Either<L, R> Left(L value)
        {
            return new Either<L, R>(EitherAlternative.Left, value, default);
        }

        ///<summary>Constructs an Either instance representing a Right alternative.</summary>
        public static Either<L, R> Right(R value)
        {
            return new Either<L, R>(EitherAlternative.Right, default, value);
        }
    }
}
