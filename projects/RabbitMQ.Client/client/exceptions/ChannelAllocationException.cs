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

namespace RabbitMQ.Client.Exceptions
{
    /// <summary> Thrown when a SessionManager cannot allocate a new
    /// channel number, or the requested channel number is already in
    /// use. </summary>
    [Serializable]
    public class ChannelAllocationException : ProtocolViolationException
    {
        /// <summary>
        /// Indicates that there are no more free channels.
        /// </summary>
        public ChannelAllocationException()
            : base("The connection cannot support any more channels. Consider creating a new connection")
        {
            Channel = -1;
        }

        /// <summary>
        /// Indicates that the specified channel is in use
        /// </summary>
        /// <param name="channel">The requested channel number</param>
        public ChannelAllocationException(int channel)
            : base($"The Requested Channel ({channel}) is already in use.")
        {
            Channel = channel;
        }

        ///<summary>Retrieves the channel number concerned; will
        ///return -1 in the case where "no more free channels" is
        ///being signaled, or a non-negative integer when "channel is
        ///in use" is being signaled.</summary>
        public int Channel { get; private set; }
    }
}
