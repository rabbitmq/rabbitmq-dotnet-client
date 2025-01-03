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
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace RabbitMQ.Client.Impl
{
    sealed class AsyncManualResetEvent : IValueTaskSource
    {
        private ManualResetValueTaskSourceCore<bool> _valueTaskSource;
        private bool _isSet;

        public AsyncManualResetEvent(bool initialState = false)
        {
            _isSet = initialState;
            _valueTaskSource.Reset();
            if (initialState)
            {
                _valueTaskSource.SetResult(true);
            }
        }

        public bool IsSet => Volatile.Read(ref _isSet);

        public async ValueTask WaitAsync(CancellationToken cancellationToken)
        {
            if (IsSet)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            CancellationTokenRegistration tokenRegistration =
#if NET
                cancellationToken.UnsafeRegister(
                    static state =>
                    {
                        var (source, token) = ((ManualResetValueTaskSourceCore<bool>, CancellationToken))state!;
                        source.SetException(new OperationCanceledException(token));
                    }, (_valueTaskSource, cancellationToken));
#else
                cancellationToken.Register(
                    static state =>
                    {
                        var (source, token) = ((ManualResetValueTaskSourceCore<bool>, CancellationToken))state!;
                        source.SetException(new OperationCanceledException(token));
                    },
                    state: (_valueTaskSource, cancellationToken), useSynchronizationContext: false);
#endif
            try
            {
                await new ValueTask(this, _valueTaskSource.Version)
                    .ConfigureAwait(false);
            }
            finally
            {
#if NET
                await tokenRegistration.DisposeAsync()
                    .ConfigureAwait(false);
#else
                tokenRegistration.Dispose();
#endif
            }
        }

        public void Set()
        {
            if (IsSet)
            {
                return;
            }

            Volatile.Write(ref _isSet, true);
            _valueTaskSource.SetResult(true);
        }

        public void Reset()
        {
            if (!IsSet)
            {
                return;
            }

            Volatile.Write(ref _isSet, false);
            _valueTaskSource.Reset();
        }

        void IValueTaskSource.GetResult(short token)
        {
            if (token != _valueTaskSource.Version)
            {
                ThrowIncorrectTokenException();
            }

            _valueTaskSource.GetResult(token);
        }

        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
        {
            if (token != _valueTaskSource.Version)
            {
                ThrowIncorrectTokenException();
            }

            return _valueTaskSource.GetStatus(token);
        }

        void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            if (token != _valueTaskSource.Version)
            {
                ThrowIncorrectTokenException();
            }

            _valueTaskSource.OnCompleted(continuation, state, token, flags);
        }

        [DoesNotReturn]
        static void ThrowIncorrectTokenException() =>
            throw new InvalidOperationException("ValueTask cannot be awaited multiple times.");
    }
}
