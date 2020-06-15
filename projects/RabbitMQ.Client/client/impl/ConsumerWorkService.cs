using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    internal class ConsumerWorkService
    {
        private readonly ConcurrentDictionary<IModel, WorkPool> _workPools = new ConcurrentDictionary<IModel, WorkPool>();
        private readonly Func<IModel, WorkPool> _startNewWorkPoolFunc;
        protected readonly int _concurrency;

        public ConsumerWorkService(int concurrency)
        {
            _concurrency = concurrency;

            _startNewWorkPoolFunc = model => StartNewWorkPool(model);
        }

        public void AddWork(IModel model, Action fn)
        {
            /*
             * rabbitmq/rabbitmq-dotnet-client#841
             * https://docs.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.getoradd
             * Note that the value delegate is not atomic but the AddWork method will not be called concurrently.
             */
            WorkPool workPool = _workPools.GetOrAdd(model, _startNewWorkPoolFunc);
            workPool.Enqueue(fn);
        }

        private WorkPool StartNewWorkPool(IModel model)
        {
            var newWorkPool = new WorkPool(_concurrency);
            newWorkPool.Start();
            return newWorkPool;
        }

        public void StopWork()
        {
            foreach (IModel model in _workPools.Keys)
            {
                StopWork(model);
            }
        }

        public void StopWork(IModel model)
        {
            StopWorkAsync(model).GetAwaiter().GetResult();
        }

        internal Task StopWorkAsync(IModel model)
        {
            if (_workPools.TryRemove(model, out WorkPool workPool))
            {
                return workPool.Stop();
            }

            return Task.CompletedTask;
        }

        class WorkPool
        {
            readonly ConcurrentQueue<Action> _actions;
            readonly CancellationTokenSource _tokenSource;
            readonly CancellationTokenRegistration _tokenRegistration;
            volatile TaskCompletionSource<bool> _syncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly int _concurrency;
            private Task _worker;
            private SemaphoreSlim _limiter;

            public WorkPool(int concurrency)
            {
                _concurrency = concurrency;
                _actions = new ConcurrentQueue<Action>();
                _tokenSource = new CancellationTokenSource();
                _tokenRegistration = _tokenSource.Token.Register(() => _syncSource.TrySetCanceled());
            }

            public void Start()
            {
                if (_concurrency == 1)
                {
                    _worker = Task.Run(Loop, CancellationToken.None);
                }
                else
                {
                    _limiter = new SemaphoreSlim(_concurrency);
                    _worker = Task.Run(() => LoopWithConcurrency(_tokenSource.Token), CancellationToken.None);
                }
            }

            public void Enqueue(Action action)
            {
                _actions.Enqueue(action);
                _syncSource.TrySetResult(true);
            }

            async Task Loop()
            {
                while (_tokenSource.IsCancellationRequested == false)
                {
                    try
                    {
                        await _syncSource.Task.ConfigureAwait(false);
                        _syncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    }
                    catch (TaskCanceledException)
                    {
                        // Swallowing the task cancellation exception for the semaphore in case we are stopping.
                    }

                    while (_actions.TryDequeue(out Action action))
                    {
                        try
                        {
                            action();
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }
                }
            }

            async Task LoopWithConcurrency(CancellationToken cancellationToken)
            {
                while (_tokenSource.IsCancellationRequested == false)
                {
                    try
                    {
                        await _syncSource.Task.ConfigureAwait(false);
                        _syncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    }
                    catch (TaskCanceledException)
                    {
                        // Swallowing the task cancellation exception for the semaphore in case we are stopping.
                    }

                    while (_actions.TryDequeue(out Action action))
                    {
                        // Do a quick synchronous check before we resort to async/await with the state-machine overhead.
                        if(!_limiter.Wait(0))
                        {
                            await _limiter.WaitAsync(cancellationToken).ConfigureAwait(false);
                        }

                        _ = OffloadToWorkerThreadPool(action, _limiter);
                    }
                }
            }

            static async Task OffloadToWorkerThreadPool(Action action, SemaphoreSlim limiter)
            {
                try
                {
                    await Task.Run(() => action());
                }
                catch (Exception)
                {
                    // ignored
                }
                finally
                {
                    limiter.Release();
                }
            }

            public Task Stop()
            {
                _tokenSource.Cancel();
                _tokenRegistration.Dispose();
                _limiter?.Dispose();
                return _worker;
            }
        }
    }
}
