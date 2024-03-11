﻿// This source code is dual-licensed under the Apache License, version
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

namespace RabbitMQ.Client
{
    internal struct RentedMemory : IDisposable
    {
        private bool _disposedValue;

        internal RentedMemory(byte[] rentedArray)
            : this(new ReadOnlyMemory<byte>(rentedArray), rentedArray)
        {
        }

        internal RentedMemory(ReadOnlyMemory<byte> memory, byte[] rentedArray)
        {
            Memory = memory;
            RentedArray = rentedArray;
            _disposedValue = false;
        }

        internal readonly ReadOnlyMemory<byte> Memory;

        internal readonly byte[] ToArray()
        {
            return Memory.ToArray();
        }

        internal readonly int Size => Memory.Length;

        internal readonly ReadOnlySpan<byte> Span => Memory.Span;

        internal readonly byte[] RentedArray;

        internal readonly ReadOnlyMemory<byte> CopyToMemory()
        {
            return new ReadOnlyMemory<byte>(Memory.ToArray());
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing && RentedArray != null)
                {
                    ArrayPool<byte>.Shared.Return(RentedArray);
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
