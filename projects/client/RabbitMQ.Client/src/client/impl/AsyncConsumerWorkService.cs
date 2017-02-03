using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client
{
    internal class AsyncConsumerWorkService
    {
        readonly ConcurrentDictionary<IModel, WorkPool> workPools = new ConcurrentDictionary<IModel, WorkPool>();

        public void AddWork(IModel model, Func<Task> fn)
        {
            // two step approach is taken, as TryGetValue does not aquire locks
            // if this fails, GetOrAdd is called, which takes a lock

            WorkPool workPool;
            if (workPools.TryGetValue(model, out workPool) == false)
            {
                var newWorkPool = new WorkPool();
                workPool = workPools.GetOrAdd(model, newWorkPool);

                // start if it's only the workpool that has been just created
                if (newWorkPool == workPool)
                {
                    newWorkPool.Start();
                }
            }

            workPool.Enqueue(fn);
        }

        public async Task StopWork(IModel model)
        {
            WorkPool workPool;
            if (workPools.TryRemove(model, out workPool))
            {
                await workPool.Stop().ConfigureAwait(false);
            }
        }

        public async Task StopWork()
        {
            foreach (var model in workPools.Keys)
            {
                await StopWork(model).ConfigureAwait(false);
            }
        }

        class WorkPool
        {
            readonly ConcurrentQueue<Func<Task>> actions;
            readonly TimeSpan waitTime;
            readonly CancellationTokenSource tokenSource;
            TaskCompletionSource<bool> messageArrived;
            private Task task;

            public WorkPool()
            {
                actions = new ConcurrentQueue<Func<Task>>();
                messageArrived = new TaskCompletionSource<bool>();
                waitTime = TimeSpan.FromMilliseconds(100);
                tokenSource = new CancellationTokenSource();
            }

            public void Start()
            {
                task = Task.Run(Loop, CancellationToken.None);
            }

            public void Enqueue(Func<Task> action)
            {
                actions.Enqueue(action);
                messageArrived.TrySetResult(true);
            }

            async Task Loop()
            {
                while (tokenSource.IsCancellationRequested == false)
                {
                    Func<Task> action;
                    while (actions.TryDequeue(out action))
                    {
                        try
                        {
                            await action().ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                        }
                    }

                    await Task.WhenAny(Task.Delay(waitTime, tokenSource.Token), messageArrived.Task).ConfigureAwait(false);
                    messageArrived.TrySetResult(true);
                    messageArrived = new TaskCompletionSource<bool>();
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