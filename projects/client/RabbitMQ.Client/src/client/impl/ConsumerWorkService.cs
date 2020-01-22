using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client
{
    public class ConsumerWorkService
    {
        private readonly ConcurrentDictionary<IModel, WorkPool> workPools = new ConcurrentDictionary<IModel, WorkPool>();

        public void AddWork(IModel model, Action fn)
        {
            workPools.GetOrAdd(model, StartNewWorkPool).Enqueue(fn);
        }

        private WorkPool StartNewWorkPool(IModel model)
        {
            var newWorkPool = new WorkPool(model);
            newWorkPool.Start();
            return newWorkPool;
        }

        public void StopWork(IModel model)
        {
            if (workPools.TryRemove(model, out WorkPool workPool))
            {
                workPool.Stop();
            }
        }

        public void StopWork()
        {
            foreach (IModel model in workPools.Keys)
            {
                StopWork(model);
            }
        }

        class WorkPool
        {
            readonly ConcurrentQueue<Action> actions;
            readonly SemaphoreSlim semaphore = new SemaphoreSlim(0);
            readonly CancellationTokenSource tokenSource;
            private Task worker;

            public WorkPool(IModel model)
            {
                actions = new ConcurrentQueue<Action>();
                tokenSource = new CancellationTokenSource();
            }

            public void Start()
            {
                worker = Task.Run(Loop);
            }

            public void Enqueue(Action action)
            {
                actions.Enqueue(action);
                semaphore.Release();
            }

            async Task Loop()
            {
                while (tokenSource.IsCancellationRequested == false)
                {
                    try
                    {
                        await semaphore.WaitAsync(tokenSource.Token).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        // Swallowing the task cancellation exception for the semaphore in case we are stopping.
                    }

                    if (!tokenSource.IsCancellationRequested && actions.TryDequeue(out Action action))
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
                tokenSource.Cancel();
            }
        }
    }
}
