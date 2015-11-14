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

using System;

namespace RabbitMQ.Util
{
    ///<summary>Used internally by class Either.</summary>
    public enum EitherAlternative
    {
        Left,
        Right
    }


    ///<summary>Models the disjoint union of two alternatives, a
    ///"left" alternative and a "right" alternative.</summary>
    ///<remarks>Borrowed from ML, Haskell etc.</remarks>
    public class Either
    {
        ///<summary>Private constructor. Use the static methods Left, Right instead.</summary>
        private Either(EitherAlternative alternative, object value)
        {
            Alternative = alternative;
            Value = value;
        }

        ///<summary>Retrieve the alternative represented by this instance.</summary>
        public EitherAlternative Alternative { get; private set; }

        ///<summary>Retrieve the value carried by this instance.</summary>
        public object Value { get; private set; }

        ///<summary>Constructs an Either instance representing a Left alternative.</summary>
        public static Either Left(object value)
        {
            return new Either(EitherAlternative.Left, value);
        }

        ///<summary>Constructs an Either instance representing a Right alternative.</summary>
        public static Either Right(object value)
        {
            return new Either(EitherAlternative.Right, value);
        }
    }
}
