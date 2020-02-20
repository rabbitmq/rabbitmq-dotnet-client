using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client
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
            var newWorkPool = new WorkPool(model);
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
            readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);
            readonly CancellationTokenSource _tokenSource;
            private Task _worker;

            public WorkPool(IModel model)
            {
                _actions = new ConcurrentQueue<Action>();
                _tokenSource = new CancellationTokenSource();
            }

            public void Start()
            {
                _worker = Task.Run(Loop);
            }

            public void Enqueue(Action action)
            {
                _actions.Enqueue(action);
                _semaphore.Release();
            }

            async Task Loop()
            {
                while (_tokenSource.IsCancellationRequested == false)
                {
                    try
                    {
                        await _semaphore.WaitAsync(_tokenSource.Token).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        // Swallowing the task cancellation exception for the semaphore in case we are stopping.
                    }

                    if (!_tokenSource.IsCancellationRequested && _actions.TryDequeue(out Action action))
                    {
                        try
                        {
                            action();
                        }
                        catch (Exception)
                        {
                        }
                    }

                }
            }

            public void Stop()
            {
                _tokenSource.Cancel();
            }
        }
    }
}
