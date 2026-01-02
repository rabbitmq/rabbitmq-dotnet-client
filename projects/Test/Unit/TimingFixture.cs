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

namespace Test.Unit
{
    public class TimingFixture
    {
        public static readonly TimeSpan TimingInterval = TimeSpan.FromMilliseconds(300);
        public static readonly TimeSpan TimingInterval_2X = TimeSpan.FromMilliseconds(600);
        public static readonly TimeSpan TimingInterval_4X = TimeSpan.FromMilliseconds(1200);
        public static readonly TimeSpan TimingInterval_8X = TimeSpan.FromMilliseconds(2400);
        public static readonly TimeSpan TimingInterval_16X = TimeSpan.FromMilliseconds(4800);
        public static readonly TimeSpan SafetyMargin = TimeSpan.FromMilliseconds(150);
        public static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    }
}
