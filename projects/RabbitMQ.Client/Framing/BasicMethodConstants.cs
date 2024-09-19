// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

namespace RabbitMQ.Client.Framing
{
    internal static class BasicMethodConstants
    {
        internal const ushort Qos = 10;
        internal const ushort QosOk = 11;
        internal const ushort Consume = 20;
        internal const ushort ConsumeOk = 21;
        internal const ushort Cancel = 30;
        internal const ushort CancelOk = 31;
        internal const ushort Publish = 40;
        internal const ushort Return = 50;
        internal const ushort Deliver = 60;
        internal const ushort Get = 70;
        internal const ushort GetOk = 71;
        internal const ushort GetEmpty = 72;
        internal const ushort Ack = 80;
        internal const ushort Reject = 90;
        internal const ushort RecoverAsync = 100;
        internal const ushort Recover = 110;
        internal const ushort RecoverOk = 111;
        internal const ushort Nack = 120;
    }
}
