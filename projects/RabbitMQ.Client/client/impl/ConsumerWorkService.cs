using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    internal class ConsumerWorkService
    {
        private readonly ConcurrentDictionary<IModel, WorkPool> _workPools = new ConcurrentDictionary<IModel, WorkPool>();
        private readonly Func<IModel, WorkPool> _startNewWorkPoolFunc;
        protected readonly int _concurrency;

        public ConsumerWorkService(int concurrency)
        {
            _concurrency = concurrency;

            _startNewWorkPoolFunc = model => StartNewWorkPool(model);
        }

        public void AddWork(IModel model, Action fn)
        {
            /*
             * rabbitmq/rabbitmq-dotnet-client#841
             * https://docs.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.getoradd
             * Note that the value delegate is not atomic but the AddWork method will not be called concurrently.
             */
            WorkPool workPool = _workPools.GetOrAdd(model, _startNewWorkPoolFunc);
            workPool.Enqueue(fn);
        }

        private WorkPool StartNewWorkPool(IModel model)
        {
            var newWorkPool = new WorkPool(_concurrency);
            newWorkPool.Start();
            return newWorkPool;
        }

        public void StopWork()
        {
            foreach (IModel model in _workPools.Keys)
            {
                StopWork(model);
            }
        }

        public void StopWork(IModel model)
        {
            StopWorkAsync(model).GetAwaiter().GetResult();
        }

        internal Task StopWorkAsync(IModel model)
        {
            if (_workPools.TryRemove(model, out WorkPool workPool))
            {
                return workPool.Stop();
            }

            return Task.CompletedTask;
        }

        private class WorkPool
        {
            private readonly Channel<Action> _channel;
            private readonly int _concurrency;
            private Task _worker;
            private CancellationTokenSource _tokenSource;
            private SemaphoreSlim _limiter;

            public WorkPool(int concurrency)
            {
                _concurrency = concurrency;
                _channel = Channel.CreateUnbounded<Action>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false });
            }

            public void Start()
            {
                if (_concurrency == 1)
                {
                    _worker = Task.Run(Loop, CancellationToken.None);
                }
                else
                {
                    _limiter = new SemaphoreSlim(_concurrency);
                    _tokenSource = new CancellationTokenSource();
                    _worker = Task.Run(() => LoopWithConcurrency(_tokenSource.Token), CancellationToken.None);
                }
            }

            public void Enqueue(Action action)
            {
                _channel.Writer.TryWrite(action);
            }

            private async Task Loop()
            {
                while (await _channel.Reader.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (_channel.Reader.TryRead(out Action work))
                    {
                        try
                        {
                            work();
                        }
                        catch(Exception)
                        {
                            // ignored
                        }
                    }
                }
            }

            private async Task LoopWithConcurrency(CancellationToken cancellationToken)
            {
                try
                {
                    while (await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        while (_channel.Reader.TryRead(out Action action))
                        {
                            // Do a quick synchronous check before we resort to async/await with the state-machine overhead.
                            if(!_limiter.Wait(0))
                            {
                                await _limiter.WaitAsync(cancellationToken).ConfigureAwait(false);
                            }

                            _ = OffloadToWorkerThreadPool(action, _limiter);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // ignored
                }
            }

            private static async Task OffloadToWorkerThreadPool(Action action, SemaphoreSlim limiter)
            {
                try
                {
                    // like Task.Run but doesn't closure allocate
                    await Task.Factory.StartNew(state =>
                    {
                        ((Action)state)();
                    }, action, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
                }
                catch (Exception)
                {
                    // ignored
                }
                finally
                {
                    limiter.Release();
                }
            }

            public Task Stop()
            {
                _channel.Writer.Complete();
                _tokenSource?.Cancel();
                _limiter?.Dispose();
                return _worker;
            }
        }
    }
}
