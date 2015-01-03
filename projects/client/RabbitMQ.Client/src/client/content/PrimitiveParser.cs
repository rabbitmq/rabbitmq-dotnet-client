// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2014 GoPivotal, Inc.
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
using System.Net;

namespace RabbitMQ.Client.Content
{
    /// <summary>
    ///     Utility class for extracting typed values from strings.
    /// </summary>
    public static class PrimitiveParser
    {
        /// <summary>
        /// Creates the protocol violation exception.
        /// </summary>
        /// <param name="targetType">Type of the target.</param>
        /// <param name="source">The source.</param>
        /// <returns>Instance of the <see cref="ProtocolViolationException" />.</returns>
        public static ProtocolViolationException CreateProtocolViolationException(string targetType, object source)
        {
            string message = string.Format("Invalid conversion to {0}: {1}", targetType, source);
            return new ProtocolViolationException(message);
        }

        /// <summary>
        /// Attempt to parse a string representation of a <see cref="bool" />.
        /// </summary>
        /// <exception cref="ProtocolViolationException" />
        public static bool ParseBool(string value)
        {
            bool result;
            if (bool.TryParse(value, out result))
            {
                return result;
            }
            throw CreateProtocolViolationException("bool", value);
        }

        /// <summary>
        /// Attempt to parse a string representation of a <see cref="byte" />.
        /// </summary>
        /// <exception cref="ProtocolViolationException" />
        public static byte ParseByte(string value)
        {
            byte result;
            if (byte.TryParse(value, out result))
            {
                return result;
            }
            throw CreateProtocolViolationException("byte", value);
        }

        /// <summary>
        /// Attempt to parse a string representation of a <see cref="double" />.
        /// </summary>
        /// <exception cref="ProtocolViolationException" />
        public static double ParseDouble(string value)
        {
            double result;
            if (double.TryParse(value, out result))
            {
                return result;
            }
            throw CreateProtocolViolationException("double", value);
        }

        /// <summary>
        /// Attempt to parse a string representation of a <see cref="float" />.
        /// </summary>
        /// <exception cref="ProtocolViolationException" />
        public static float ParseFloat(string value)
        {
            float result;
            if (float.TryParse(value, out result))
            {
                return result;
            }
            throw CreateProtocolViolationException("float", value);
        }

        /// <summary>
        /// Attempt to parse a string representation of an <see cref="int" />.
        /// </summary>
        /// <exception cref="ProtocolViolationException" />
        public static int ParseInt(string value)
        {
            int result;
            if (int.TryParse(value, out result))
            {
                return result;
            }
            throw CreateProtocolViolationException("int", value);
        }

        /// <summary>
        /// Attempt to parse a string representation of a <see cref="long" />.
        /// </summary>
        /// <exception cref="ProtocolViolationException" />
        public static long ParseLong(string value)
        {
            long result;
            if (long.TryParse(value, out result))
            {
                return result;
            }
            throw CreateProtocolViolationException("long", value);
        }

        /// <summary>
        /// Attempt to parse a string representation of a <see cref="short" />.
        /// </summary>
        /// <exception cref="ProtocolViolationException" />
        public static short ParseShort(string value)
        {
            short result;
            if (short.TryParse(value, out result))
            {
                return result;
            }
            throw CreateProtocolViolationException("short", value);
        }
    }
}
