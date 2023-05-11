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
using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace RabbitMQ.Client
{
    internal static class ClientArrayPool
    {
        private static readonly ConcurrentDictionary<byte[], bool> _checkouts;

        private static readonly bool s_useArrayPool = true;
        private static readonly bool s_trackCheckouts = false;

        static ClientArrayPool()
        {
            if (false == bool.TryParse(
                Environment.GetEnvironmentVariable("RABBITMQ_CLIENT_USE_ARRAY_POOL"),
                out s_useArrayPool))
            {
                s_useArrayPool = true;
            }

            if (bool.TryParse(
                Environment.GetEnvironmentVariable("RABBITMQ_CLIENT_TRACK_ARRAY_POOL_CHECKOUTS"),
                out s_trackCheckouts))
            {
                if (s_trackCheckouts)
                {
                    _checkouts = new ConcurrentDictionary<byte[], bool>();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static byte[] Rent(int minimumLength)
        {
            byte[] rv;

            if (s_useArrayPool)
            {
                rv = ArrayPool<byte>.Shared.Rent(minimumLength);
            }
            else
            {
                rv = new byte[minimumLength];
            }

            if (s_trackCheckouts)
            {
                if (_checkouts.ContainsKey(rv))
                {
                    throw new InvalidOperationException("ARRAY ALREADY RENTED");
                }
                else
                {
                    _checkouts[rv] = true;
                }
            }

            return rv;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Return(byte[] array)
        {
            if (array != null && array.Length > 0)
            {
                if (s_trackCheckouts && array.Length > 0)
                {
                    if (false == _checkouts.TryRemove(array, out _))
                    {
                        throw new InvalidOperationException("ARRAY NOT RENTED");
                    }
                }
                if (s_useArrayPool)
                {
                    ArrayPool<byte>.Shared.Return(array, clearArray: true);
                }
            }
        }
    }
}
