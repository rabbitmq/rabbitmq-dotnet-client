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
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Impl
{
    internal delegate Task CommandReceivedAction(IncomingCommand cmd, CancellationToken cancellationToken);

    internal interface ISession
    {
        ushort ChannelNumber { get; }

        ShutdownEventArgs? CloseReason { get; }

        CommandReceivedAction? CommandReceived { get; set; }

        Connection Connection { get; }

        bool IsOpen { get; }

        event AsyncEventHandler<ShutdownEventArgs> SessionShutdownAsync;

        Task CloseAsync(ShutdownEventArgs reason, bool notify = true);

        Task HandleFrameAsync(InboundFrame frame, CancellationToken cancellationToken);

        Task NotifyAsync(CancellationToken cancellationToken);

        ValueTask TransmitAsync<T>(in T cmd, CancellationToken cancellationToken) where T : struct, IOutgoingAmqpMethod;

        ValueTask TransmitAsync<TMethod, THeader>(in TMethod cmd, in THeader header, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
            where TMethod : struct, IOutgoingAmqpMethod
            where THeader : IAmqpHeader;
    }
}
