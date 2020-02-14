using System;
using System.Collections.Concurrent;
using System.Threading;

namespace RabbitMQ.Client
{
    public class ConsumerWorkService
    {
        readonly ConcurrentDictionary<IModel, WorkPool> workPools = new ConcurrentDictionary<IModel, WorkPool>();

        public void AddWork(IModel model, Action fn)
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
            readonly ConcurrentQueue<Action> actions;
            readonly AutoResetEvent messageArrived;
            readonly TimeSpan waitTime;
            readonly CancellationTokenSource tokenSource;
            readonly string name;

            public WorkPool(IModel model)
            {
                name = model.ToString();
                actions = new ConcurrentQueue<Action>();
                messageArrived = new AutoResetEvent(false);
                waitTime = TimeSpan.FromMilliseconds(100);
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
                actions.Enqueue(action);
                messageArrived.Set();
            }

            void Loop()
            {
                while (tokenSource.IsCancellationRequested == false)
                {
                    Action action;
                    while (actions.TryDequeue(out action))
                    {
                        try
                        {
                            action();
                        }
                        catch (Exception)
                        {
                        }
                    }

                    messageArrived.WaitOne(waitTime);
                }
            }

            public void Stop()
            {
                tokenSource.Cancel();
            }
        }
    }
}
