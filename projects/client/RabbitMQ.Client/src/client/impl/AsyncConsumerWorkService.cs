using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client
{
    internal class AsyncConsumerWorkService : ConsumerWorkService
    {
        readonly ConcurrentDictionary<IModel, WorkPool> workPools = new ConcurrentDictionary<IModel, WorkPool>();

        public void Schedule<TWork>(ModelBase model, TWork work)
            where TWork : Work
        {
            // two step approach is taken, as TryGetValue does not aquire locks
            // if this fails, GetOrAdd is called, which takes a lock

            WorkPool workPool;
            if (workPools.TryGetValue(model, out workPool) == false)
            {
                var newWorkPool = new WorkPool(model);
                workPool = workPools.GetOrAdd(model, newWorkPool);

                // start if it's only the workpool that has been just created
                if (newWorkPool == workPool)
                {
                    newWorkPool.Start();
                }
            }

            workPool.Enqueue(work);
        }

        public async Task Stop(IModel model)
        {
            WorkPool workPool;
            if (workPools.TryRemove(model, out workPool))
            {
                await workPool.Stop().ConfigureAwait(false);
            }
        }

        public async Task Stop()
        {
            foreach (var model in workPools.Keys)
            {
                await Stop(model).ConfigureAwait(false);
            }
        }

        class WorkPool
        {
            readonly BlockingCollection<Work> workQueue;
            readonly CancellationTokenSource tokenSource;
            readonly ModelBase model;
            private Task task;

            public WorkPool(ModelBase model)
            {
                this.model = model;
                workQueue = new BlockingCollection<Work>();
                tokenSource = new CancellationTokenSource();
            }

            public void Start()
            {
                task = Task.Run(Loop, CancellationToken.None);
            }

            public void Enqueue(Work work)
            {
                workQueue.Add(work);
            }

            async Task Loop()
            {
                foreach (var work in workQueue.GetConsumingEnumerable(tokenSource.Token))
                {
                    await work.Execute(model).ConfigureAwait(false);
                }
            }

            public Task Stop()
            {
                tokenSource.Cancel();
                return task;
            }
        }
    }
}