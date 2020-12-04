using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using RabbitMQ.Client.client.impl.Channel;
using Channel = System.Threading.Channels.Channel;

namespace RabbitMQ.Client.Impl
{
    internal class ConsumerWorkService
    {
        private readonly ConcurrentDictionary<ChannelBase, WorkPool> _workPools = new ConcurrentDictionary<ChannelBase, WorkPool>();
        private readonly Func<ChannelBase, WorkPool> _startNewWorkPoolFunc;
        protected readonly int _concurrency;

        public ConsumerWorkService(int concurrency)
        {
            _concurrency = concurrency;

            _startNewWorkPoolFunc = channelBase => StartNewWorkPool(channelBase);
        }

        public void AddWork(ChannelBase channelBase, Action fn)
        {
            /*
             * rabbitmq/rabbitmq-dotnet-client#841
             * https://docs.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.getoradd
             * Note that the value delegate is not atomic but the AddWork method will not be called concurrently.
             */
            WorkPool workPool = _workPools.GetOrAdd(channelBase, _startNewWorkPoolFunc);
            workPool.Enqueue(fn);
        }

        private WorkPool StartNewWorkPool(ChannelBase channelBase)
        {
            var newWorkPool = new WorkPool(channelBase, _concurrency);
            newWorkPool.Start();
            return newWorkPool;
        }

        public void StopWork()
        {
            foreach (ChannelBase channelBase in _workPools.Keys)
            {
                StopWork(channelBase);
            }
        }

        public void StopWork(ChannelBase channelBase)
        {
            StopWorkAsync(channelBase).GetAwaiter().GetResult();
        }

        internal Task StopWorkAsync(ChannelBase channelBase)
        {
            if (_workPools.TryRemove(channelBase, out WorkPool workPool))
            {
                return workPool.Stop();
            }

            return Task.CompletedTask;
        }

        private class WorkPool
        {
            private readonly Channel<Action> _channel;
            private readonly ChannelBase _channelBase;
            private readonly int _concurrency;
            private Task _worker;
            private CancellationTokenSource _tokenSource;
            private SemaphoreSlim _limiter;

            public WorkPool(ChannelBase channelBase, int concurrency)
            {
                _channelBase = channelBase;
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
                        catch(Exception e)
                        {
                            _channelBase.OnUnhandledExceptionOccurred(e, "ConsumerWorkService");
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

                            _ = OffloadToWorkerThreadPool(action, _channelBase, _limiter);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // ignored
                }
            }

            private static async Task OffloadToWorkerThreadPool(Action action, ChannelBase channel, SemaphoreSlim limiter)
            {
                try
                {
                    // like Task.Run but doesn't closure allocate
                    await Task.Factory.StartNew(state =>
                    {
                        ((Action)state)();
                    }, action, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default)
                    .ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    channel.OnUnhandledExceptionOccurred(e, "ConsumerWorkService");
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
