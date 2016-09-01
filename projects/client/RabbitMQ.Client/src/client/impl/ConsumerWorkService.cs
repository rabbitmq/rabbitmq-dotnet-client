using RabbitMQ.Util;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabbitMQ.Client
{
    public class ConsumerWorkService
    {
        public const int MAX_THUNK_EXECUTION_BATCH_SIZE = 16;
        private TaskScheduler scheduler;
        private BatchingWorkPool<IModel, Action> workPool;

        public ConsumerWorkService() :
            this(TaskScheduler.Default) { }

        public ConsumerWorkService(TaskScheduler scheduler)
        {
            this.scheduler = scheduler;
            this.workPool = new BatchingWorkPool<IModel, Action>();
        }

        public void ExecuteThunk()
        {
            var actions = new List<Action>(MAX_THUNK_EXECUTION_BATCH_SIZE);

            try
            {
                IModel key = this.workPool.NextWorkBlock(ref actions, MAX_THUNK_EXECUTION_BATCH_SIZE);
                if (key == null) { return; }

                try
                {
                    foreach (var fn in actions)
                    {
                        fn();
                    }
                }
                finally
                {
                    if (this.workPool.FinishWorkBlock(key))
                    {
                        var t = new Task(new Action(ExecuteThunk));
                        t.Start(this.scheduler);
                    }
                }
            }
            catch (Exception)
            {
                return;
            }
        }

        public void AddWork(IModel model, Action fn)
        {
            if (this.workPool.AddWorkItem(model, fn))
            {
                var t = new Task(new Action(ExecuteThunk));
                t.Start(this.scheduler);
            }
        }

        public void RegisterKey(IModel model)
        {
            this.workPool.RegisterKey(model);
        }

        public void StopWork(IModel model)
        {
            this.workPool.UnregisterKey(model);
        }

        public void StopWork()
        {
            this.workPool.UnregisterAllKeys();
        }
    }
}
