// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2013 GoPivotal, Inc.
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
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;

namespace RabbitMQ.Client.Exceptions
{
    /// <summary> Thrown when a SessionManager cannot allocate a new
    /// channel number, or the requested channel number is already in
    /// use. </summary>
    public class ChannelAllocationException : Exception
    {
        private readonly int m_channel;

        /// <summary>
        /// Indicates that there are no more free channels.
        /// </summary>
        public ChannelAllocationException()
            : base("The connection cannot support any more channels. Consider creating a new connection")
        {
            m_channel = -1;
        }

        /// <summary>
        /// Indicates that the specified channel is in use
        /// </summary>
        /// <param name="channel">The requested channel number</param>
        public ChannelAllocationException(int channel)
            : base(string.Format("The Requested Channel ({0}) is already in use.", channel))
        {
            m_channel = channel;
        }

        ///<summary>Retrieves the channel number concerned; will
        ///return -1 in the case where "no more free channels" is
        ///being signalled, or a non-negative integer when "channel is
        ///in use" is being signalled.</summary>
        public int Channel { get { return m_channel; } }
    }
}
