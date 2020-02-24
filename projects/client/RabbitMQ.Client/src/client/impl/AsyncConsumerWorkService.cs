using RabbitMQ.Client.Impl;

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client
{
    internal class AsyncConsumerWorkService : ConsumerWorkService
    {
        private readonly ConcurrentDictionary<IModel, WorkPool> workPools = new ConcurrentDictionary<IModel, WorkPool>();

        public void Schedule<TWork>(ModelBase model, TWork work) where TWork : Work
        {
            workPools.GetOrAdd(model, StartNewWorkPool).Enqueue(work);
        }

        private WorkPool StartNewWorkPool(IModel model)
        {
            var newWorkPool = new WorkPool(model as ModelBase);
            newWorkPool.Start();
            return newWorkPool;
        }

        public void Stop(IModel model)
        {
            if (workPools.TryRemove(model, out WorkPool workPool))
            {
                workPool.Stop();
            }
        }

        public void Stop()
        {
            foreach (IModel model in workPools.Keys)
            {
                Stop(model);
            }
        }

        class WorkPool
        {
            readonly ConcurrentQueue<Work> workQueue;
            readonly CancellationTokenSource tokenSource;
            readonly ModelBase model;
            readonly SemaphoreSlim semaphore = new SemaphoreSlim(0);
            private Task task;

            public WorkPool(ModelBase model)
            {
                this.model = model;
                workQueue = new ConcurrentQueue<Work>();
                tokenSource = new CancellationTokenSource();
            }

            public void Start()
            {
                task = Task.Run(Loop, CancellationToken.None);
            }

            public void Enqueue(Work work)
            {
                workQueue.Enqueue(work);
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
                        // Swallowing the task cancellation in case we are stopping work.
                    }

                    if (!tokenSource.IsCancellationRequested && workQueue.TryDequeue(out Work work))
                    {
                        await work.Execute(model).ConfigureAwait(false);
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
