using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    public class ConsumerWorkService
    {
        private readonly ConcurrentDictionary<IModel, WorkPool> _workPools = new ConcurrentDictionary<IModel, WorkPool>();

        public void AddWork(IModel model, Action fn)
        {
            _workPools.GetOrAdd(model, StartNewWorkPool).Enqueue(fn);
        }

        private WorkPool StartNewWorkPool(IModel model)
        {
            var newWorkPool = new WorkPool();
            newWorkPool.Start();
            return newWorkPool;
        }

        public void StopWork(IModel model)
        {
            if (_workPools.TryRemove(model, out WorkPool workPool))
            {
                workPool.Stop();
            }
        }

        public void StopWork()
        {
            foreach (IModel model in _workPools.Keys)
            {
                StopWork(model);
            }
        }

        class WorkPool
        {
            readonly ConcurrentQueue<Action> _actions;
            readonly CancellationTokenSource _tokenSource;
            readonly CancellationTokenRegistration _tokenRegistration;
            volatile TaskCompletionSource<bool> _syncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private Task _worker;

            public WorkPool()
            {
                _actions = new ConcurrentQueue<Action>();
                _tokenSource = new CancellationTokenSource();
                _tokenRegistration = _tokenSource.Token.Register(() => _syncSource.TrySetCanceled());
            }

            public void Start()
            {
                _worker = Task.Run(Loop, CancellationToken.None);
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

            public void Stop()
            {
                _tokenSource.Cancel();
                _tokenRegistration.Dispose();
            }
        }
    }
}
