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

namespace RabbitMQ.Client;

// time representations in mainstream languages: the horror, the horror
// see in particular the difference between .NET 1.x and .NET 2.0's versions of DateTime

/// <summary>
/// Structure holding an AMQP timestamp, a posix 64-bit time_t.</summary>
/// <remarks>
/// <para>
/// When converting between an AmqpTimestamp and a System.DateTime,
/// be aware of the effect of your local timezone. In particular,
/// different versions of the .NET framework assume different
/// defaults.
/// </para>
/// <para>
/// We have chosen a signed 64-bit time_t here, since the AMQP
/// specification through versions 0-9 is silent on whether
/// timestamps are signed or unsigned.
/// </para>
/// </remarks>
public readonly struct AmqpTimestamp : IEquatable<AmqpTimestamp>
{
    /// <summary>
    /// Construct an <see cref="AmqpTimestamp"/>.
    /// </summary>
    /// <param name="unixTime">Unix time.</param>
    public AmqpTimestamp(long unixTime) : this()
    {
        UnixTime = unixTime;
    }

    /// <summary>
    /// Unix time.
    /// </summary>
    public long UnixTime { get; }

    public bool Equals(AmqpTimestamp other) => UnixTime == other.UnixTime;

    public override bool Equals(object obj) => obj is AmqpTimestamp other && Equals(other);

    public override int GetHashCode() => UnixTime.GetHashCode();

    public static bool operator ==(AmqpTimestamp left, AmqpTimestamp right) => left.Equals(right);

    public static bool operator !=(AmqpTimestamp left, AmqpTimestamp right) => !left.Equals(right);

    /// <summary>
    /// Provides a debugger-friendly display.
    /// </summary>
    public override string ToString()
    {
        return $"((time_t){UnixTime})";
    }
}
