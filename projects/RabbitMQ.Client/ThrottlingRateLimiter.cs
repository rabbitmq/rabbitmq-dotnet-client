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
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace RabbitMQ.Client
{
    public class ThrottlingRateLimiter : RateLimiter
    {
        public const int DefaultThrottlingPercentage = 50;

        private readonly ConcurrencyLimiter _concurrencyLimiter;
        private readonly int _maxConcurrency;
        private readonly int _throttlingThreshold;

        public ThrottlingRateLimiter(int maxConcurrentCalls, int? throttlingPercentage = DefaultThrottlingPercentage)
        {
            _maxConcurrency = maxConcurrentCalls;
            _throttlingThreshold = _maxConcurrency * throttlingPercentage.GetValueOrDefault(DefaultThrottlingPercentage) / 100;

            ConcurrencyLimiterOptions limiterOptions = new()
            {
                QueueLimit = _maxConcurrency,
                PermitLimit = _maxConcurrency
            };

            _concurrencyLimiter = new ConcurrencyLimiter(limiterOptions);
        }

        public override TimeSpan? IdleDuration => null;

        public override RateLimiterStatistics? GetStatistics() => _concurrencyLimiter.GetStatistics();

        protected override RateLimitLease AttemptAcquireCore(int permitCount)
        {
            RateLimitLease lease = _concurrencyLimiter.AttemptAcquire(permitCount);

            ThrottleIfNeeded();

            return lease;
        }

        protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
        {
            RateLimitLease lease = await _concurrencyLimiter.AcquireAsync(permitCount, cancellationToken).ConfigureAwait(false);

            await ThrottleIfNeededAsync(cancellationToken).ConfigureAwait(false);

            return lease;
        }

        private void ThrottleIfNeeded()
        {
            int delay = CalculateDelay();
            if (delay > 0)
            {
                Thread.Sleep(delay);
            }
        }

        private Task ThrottleIfNeededAsync(CancellationToken cancellationToken = default)
        {
            int delay = CalculateDelay();
            if (delay > 0)
            {
                return Task.Delay(delay, cancellationToken);
            }
            else
            {
                return Task.CompletedTask;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _concurrencyLimiter.Dispose();
            }

            base.Dispose(disposing);
        }

        private int CalculateDelay()
        {
            long? availablePermits = _concurrencyLimiter.GetStatistics()?.CurrentAvailablePermits;
            if (!(availablePermits < _throttlingThreshold))
            {
                return 0;
            }

            return (int)((1.0 - availablePermits / (double)_maxConcurrency) * 1000);
        }
    }
}
