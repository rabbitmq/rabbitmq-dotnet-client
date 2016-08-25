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
            WorkPool workPool;
            if (workPools.TryGetValue(model, out workPool))
            {
                workPool.Enqueue(fn);
            }
        }

        public void RegisterKey(IModel model)
        {
            // the main model can be skipped, as it will not use CWS anyway
            if (model.ChannelNumber == 0)
            {
                return;
            }

            var workPool = new WorkPool(model);
            if (workPools.TryAdd(model, workPool))
            {
                workPool.Start();
            }
        }

        public void StopWork(IModel model)
        {
            if (model.ChannelNumber == 0)
            {
                return;
            }

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
                var thread = new Thread(Loop)
                {
                    Name = "WorkPool-" + name,
                    IsBackground = true
                };
                thread.Start();
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
