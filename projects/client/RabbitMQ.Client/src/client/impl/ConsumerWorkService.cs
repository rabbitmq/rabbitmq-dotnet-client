using System;
using System.Collections.Concurrent;
using System.Threading;

namespace RabbitMQ.Client
{
    public class ConsumerWorkService
    {
        private readonly int workPoolCapacity;
        private readonly ConcurrentDictionary<IModel, WorkPool> workPools;

        public ConsumerWorkService(int workPoolCapacity)
        {
            this.workPoolCapacity = workPoolCapacity;
            this.workPools = new ConcurrentDictionary<IModel, WorkPool>();
        }

        public void AddWork(IModel model, Action fn)
        {
            // two step approach is taken, as TryGetValue does not aquire locks
            // if this fails, GetOrAdd is called, which takes a lock

            WorkPool workPool;
            if (workPools.TryGetValue(model, out workPool) == false)
            {
                var newWorkPool = new WorkPool(model, workPoolCapacity);
                workPool = workPools.GetOrAdd(model, newWorkPool);

                // start if it's only the workpool that has been just created
                if (newWorkPool == workPool)
                {
                    newWorkPool.Start();
                }
            }

            workPool.Enqueue(fn);
        }

        public void StopWork(IModel model)
        {
            WorkPool workPool;
            if (workPools.TryRemove(model, out workPool))
            {
                workPool.Stop();
            }
        }

        public void StopWork()
        {
            foreach (var model in workPools.Keys)
            {
                StopWork(model);
            }
        }

        class WorkPool
        {
            readonly BlockingCollection<Action> actions;
            readonly CancellationTokenSource tokenSource;
            readonly string name;

            public WorkPool(IModel model, int capacity)
            {
                name = model.ToString();
                if (capacity > 0)
                {
                    actions = new BlockingCollection<Action>(new ConcurrentQueue<Action>(), capacity);
                }
                else
                {
                    actions = new BlockingCollection<Action>(new ConcurrentQueue<Action>());
                }
                tokenSource = new CancellationTokenSource();
            }

            public void Start()
            {
#if NETFX_CORE
                System.Threading.Tasks.Task.Factory.StartNew(Loop, System.Threading.Tasks.TaskCreationOptions.LongRunning);
#else
                var thread = new Thread(Loop)
                {
                    Name = "WorkPool-" + name,
                    IsBackground = true
                };
                thread.Start();
#endif
            }

            public void Enqueue(Action action)
            {
                try
                {
                    actions.Add(action, tokenSource.Token);
                }
                catch (OperationCanceledException)
                {

                }
            }

            void Loop()
            {
                try
                {
                    foreach (var action in actions.GetConsumingEnumerable(tokenSource.Token))
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
                catch (OperationCanceledException)
                {
                }
            }

            public void Stop()
            {
                tokenSource.Cancel();
            }
        }
    }
}
