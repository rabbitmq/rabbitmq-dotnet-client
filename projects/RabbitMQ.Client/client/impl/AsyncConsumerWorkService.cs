using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    internal sealed class AsyncConsumerWorkService : ConsumerWorkService
    {
        private readonly ConcurrentDictionary<IModel, WorkPool> _workPools;
        private readonly Func<IModel, WorkPool> _startNewWorkPoolAction;

        public AsyncConsumerWorkService()
        {
            _workPools = new ConcurrentDictionary<IModel, WorkPool>();
            _startNewWorkPoolAction = model => StartNewWorkPool(model);
        }

        public void Schedule<TWork>(ModelBase model, TWork work) where TWork : Work
        {
            _workPools.GetOrAdd(model, _startNewWorkPoolAction).Enqueue(work);
        }

        private WorkPool StartNewWorkPool(IModel model)
        {
            var newWorkPool = new WorkPool(model as ModelBase);
            newWorkPool.Start();
            return newWorkPool;
        }

        public Task Stop(IModel model)
        {
            if (_workPools.TryRemove(model, out WorkPool workPool))
            {
                return workPool.Stop();
            }

            return Task.CompletedTask;
        }

        class WorkPool
        {
            readonly Channel<Work> _channel;
            readonly ModelBase _model;
            private Task _worker;

            public WorkPool(ModelBase model)
            {
                _model = model;
                _channel = Channel.CreateUnbounded<Work>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false });
            }

            public void Start()
            {
                _worker = Task.Run(Loop, CancellationToken.None);
            }

            public void Enqueue(Work work)
            {
                _channel.Writer.TryWrite(work);
            }

            async Task Loop()
            {
                while (await _channel.Reader.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (_channel.Reader.TryRead(out Work work))
                    {
                        try
                        {
                            Task task = work.Execute(_model);
                            if (!task.IsCompleted)
                            {
                                await task.ConfigureAwait(false);
                            }
                        }
                        catch(Exception)
                        {

                        }
                    }
                }
            }

            public Task Stop()
            {
                _channel.Writer.Complete();
                return _worker;
            }
        }
    }
}
