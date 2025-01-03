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

using System;

namespace RabbitMQ.Client
{
    public static class Constants
    {
        ///<summary>(= 1)</summary>
        public const int FrameMethod = 1;
        ///<summary>(= 2)</summary>
        public const int FrameHeader = 2;
        ///<summary>(= 3)</summary>
        public const int FrameBody = 3;
        ///<summary>(= 8)</summary>
        public const int FrameHeartbeat = 8;
        ///<summary>(= 4096)</summary>
        public const int FrameMinSize = 4096;
        ///<summary>(= 206)</summary>
        public const int FrameEnd = 206;
        ///<summary>(= 200)</summary>
        public const int ReplySuccess = 200;
        ///<summary>(= 311)</summary>
        public const int ContentTooLarge = 311;
        ///<summary>(= 312)</summary>
        public const int NoRoute = 312;
        ///<summary>(= 313)</summary>
        public const int NoConsumers = 313;
        ///<summary>(= 320)</summary>
        public const int ConnectionForced = 320;
        ///<summary>(= 402)</summary>
        public const int InvalidPath = 402;
        ///<summary>(= 403)</summary>
        public const int AccessRefused = 403;
        ///<summary>(= 404)</summary>
        public const int NotFound = 404;
        ///<summary>(= 405)</summary>
        public const int ResourceLocked = 405;
        ///<summary>(= 406)</summary>
        public const int PreconditionFailed = 406;
        ///<summary>(= 501)</summary>
        public const int FrameError = 501;
        ///<summary>(= 502)</summary>
        public const int SyntaxError = 502;
        ///<summary>(= 503)</summary>
        public const int CommandInvalid = 503;
        ///<summary>(= 504)</summary>
        public const int ChannelError = 504;
        ///<summary>(= 505)</summary>
        public const int UnexpectedFrame = 505;
        ///<summary>(= 506)</summary>
        public const int ResourceError = 506;
        ///<summary>(= 530)</summary>
        public const int NotAllowed = 530;
        ///<summary>(= 540)</summary>
        public const int NotImplemented = 540;
        ///<summary>(= 541)</summary>
        public const int InternalError = 541;

        /// <summary>
        /// The default consumer dispatch concurrency. See <see cref="IConnectionFactory.ConsumerDispatchConcurrency"/>
        /// to set this value for every channel created on a connection,
        /// and <see cref="IConnection.CreateChannelAsync(CreateChannelOptions?, System.Threading.CancellationToken)" />
        /// for setting this value for a particular channel.
        /// </summary>
        public const ushort DefaultConsumerDispatchConcurrency = 1;

        /// <summary>
        /// The message header used to track publish sequence numbers, to allow correlation when
        /// <c>basic.return</c> is sent via the broker.
        /// </summary>
        public const string PublishSequenceNumberHeader = "x-dotnet-pub-seq-no";

        /// <summary>
        /// The default timeout for initial AMQP handshake
        /// </summary>
        public static readonly TimeSpan DefaultHandshakeContinuationTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The default timeout for RPC methods
        /// </summary>
        public static readonly TimeSpan DefaultContinuationTimeout = TimeSpan.FromSeconds(20);
    }
}
