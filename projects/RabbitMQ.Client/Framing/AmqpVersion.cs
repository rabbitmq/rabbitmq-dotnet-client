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

namespace RabbitMQ.Client.Framing
{
    /// <summary>Represents a version of the AMQP specification.</summary>
    /// <remarks>
    /// <para>
    /// Vendor-specific variants of particular official specification
    /// versions exist: this class simply represents the AMQP
    /// specification version, and does not try to represent
    /// information about any custom variations involved.
    /// </para>
    /// <para>
    /// AMQP version 0-8 peers sometimes advertise themselves as
    /// version 8-0: for this reason, this class's constructor
    /// special-cases 8-0, rewriting it at construction time to be 0-8 instead.
    /// </para>
    /// </remarks>
    internal readonly struct AmqpVersion : IEquatable<AmqpVersion>
    {
        /// <summary>
        /// Construct an <see cref="AmqpVersion"/> from major and minor version numbers.
        /// </summary>
        /// <remarks>
        /// Converts major=8 and minor=0 into major=0 and minor=8. Please see the class comment.
        /// </remarks>
        public AmqpVersion(int major, int minor)
        {
            if (major == 8 && minor == 0)
            {
                // The AMQP 0-8 spec confusingly defines the version
                // as 8-0. This maps the latter to the former, for
                // cases where our peer might be confused.
                major = 0;
                minor = 8;
            }
            Major = major;
            Minor = minor;
        }

        /// <summary>
        /// The AMQP specification major version number.
        /// </summary>
        public int Major { get; }

        /// <summary>
        /// The AMQP specification minor version number.
        /// </summary>
        public int Minor { get; }

        /// <summary>
        /// Implement value-equality comparison.
        /// </summary>
        public override bool Equals(object? other)
        {
            return other is AmqpVersion version && Equals(version);
        }

        public bool Equals(AmqpVersion other) => Major == other.Major && Minor == other.Minor;

        public static bool operator ==(AmqpVersion left, AmqpVersion right) => left.Equals(right);

        public static bool operator !=(AmqpVersion left, AmqpVersion right) => !left.Equals(right);

        /// <summary>
        /// Implement hashing as for value-equality.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (Major * 397) ^ Minor;
            }
        }

        /// <summary>
        /// Format appropriately for display.
        /// </summary>
        /// <remarks>
        /// The specification currently uses "MAJOR-MINOR" as a display format.
        /// </remarks>
        public override string ToString()
        {
            return $"{Major}-{Minor}";
        }
    }
}
