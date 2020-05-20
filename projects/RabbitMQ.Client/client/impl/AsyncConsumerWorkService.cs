using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    internal sealed class AsyncConsumerWorkService : ConsumerWorkService
    {
        private readonly ConcurrentDictionary<IModel, WorkPool> _workPools = new ConcurrentDictionary<IModel, WorkPool>();
        private readonly Func<IModel, WorkPool> _startNewWorkPoolFunc = model => StartNewWorkPool(model);

        public void Schedule<TWork>(ModelBase model, TWork work) where TWork : Work
        {
            /*
             * rabbitmq/rabbitmq-dotnet-client#841
             * https://docs.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.getoradd
             * Note that the value delegate is not atomic but instances of this class are not meant to be used by
             * multiple threads.
             */
            WorkPool workPool = _workPools.GetOrAdd(model, _startNewWorkPoolFunc);
            workPool.Enqueue(work);
        }

        private static WorkPool StartNewWorkPool(IModel model)
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
