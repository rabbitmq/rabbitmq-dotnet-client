// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client.OAuth2
{
    public delegate Task NotifyCredentialsRefreshedAsync(Credentials? credentials,
        Exception? exception = null,
        CancellationToken cancellationToken = default);

    public class CredentialsRefresher : IDisposable
    {
        private readonly ICredentialsProvider _credentialsProvider;
        private readonly NotifyCredentialsRefreshedAsync _onRefreshed;

        private readonly CancellationTokenSource _internalCts = new CancellationTokenSource();
        private readonly CancellationTokenSource _linkedCts;

        private readonly Task _refreshTask;

        private Credentials? _credentials;
        private bool _disposedValue = false;

        public CredentialsRefresher(ICredentialsProvider credentialsProvider,
            NotifyCredentialsRefreshedAsync onRefreshed,
            CancellationToken cancellationToken)
        {
            if (credentialsProvider is null)
            {
                throw new ArgumentNullException(nameof(credentialsProvider));
            }

            if (onRefreshed is null)
            {
                throw new ArgumentNullException(nameof(onRefreshed));
            }

            _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_internalCts.Token, cancellationToken);

            _credentialsProvider = credentialsProvider;
            _onRefreshed = onRefreshed;

            _refreshTask = Task.Run(RefreshLoopAsync, _linkedCts.Token);

            CredentialsRefresherEventSource.Log.Started(_credentialsProvider.Name);
        }

        public Credentials? Credentials => _credentials;

        private async Task RefreshLoopAsync()
        {
            while (false == _linkedCts.IsCancellationRequested)
            {
                try
                {
                    _credentials = await _credentialsProvider.GetCredentialsAsync(_linkedCts.Token)
                        .ConfigureAwait(false);

                    if (_linkedCts.IsCancellationRequested)
                    {
                        break;
                    }

                    CredentialsRefresherEventSource.Log.RefreshedCredentials(_credentialsProvider.Name);

                    await _onRefreshed(_credentials, null, _linkedCts.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    await _onRefreshed(null, ex, _linkedCts.Token)
                        .ConfigureAwait(false);
                }

                TimeSpan delaySpan = TimeSpan.FromSeconds(30);
                if (_credentials != null && _credentials.ValidUntil.HasValue)
                {
                    delaySpan = TimeSpan.FromMilliseconds(_credentials.ValidUntil.Value.TotalMilliseconds * (1.0 - (1 / 3.0)));
                }

                await Task.Delay(delaySpan, _linkedCts.Token)
                    .ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _internalCts.Cancel();
                    _refreshTask.Wait(TimeSpan.FromSeconds(5));
                    _internalCts.Dispose();
                    _linkedCts.Dispose();
                    CredentialsRefresherEventSource.Log.Stopped(_credentialsProvider.Name);
                }

                _disposedValue = true;
            }
        }
    }
}
