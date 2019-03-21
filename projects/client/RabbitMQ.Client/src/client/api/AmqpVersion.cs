// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2016 Pivotal Software, Inc.
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
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

namespace RabbitMQ.Client
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
    public class AmqpVersion
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
        public int Major { get; private set; }

        /// <summary>
        /// The AMQP specification minor version number.
        /// </summary>
        public int Minor { get; private set; }

        /// <summary>
        /// Implement value-equality comparison.
        /// </summary>
        public override bool Equals(object other)
        {
            var version = other as AmqpVersion;
            return (version != null) && (version.Major == Major) && (version.Minor == Minor);
        }

        /// <summary>
        /// Implement hashing as for value-equality.
        /// </summary>
        public override int GetHashCode()
        {
            return 31*Major.GetHashCode() + Minor.GetHashCode();
        }

        /// <summary>
        /// Format appropriately for display.
        /// </summary>
        /// <remarks>
        /// The specification currently uses "MAJOR-MINOR" as a display format.
        /// </remarks>
        public override string ToString()
        {
            return Major + "-" + Minor;
        }
    }
}
