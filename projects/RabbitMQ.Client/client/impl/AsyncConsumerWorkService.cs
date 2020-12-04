using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using RabbitMQ.Client.client.impl.Channel;
using Channel = System.Threading.Channels.Channel;

namespace RabbitMQ.Client.Impl
{
    internal sealed class AsyncConsumerWorkService : ConsumerWorkService
    {
        private readonly ConcurrentDictionary<ChannelBase, WorkPool> _workPools = new ConcurrentDictionary<ChannelBase, WorkPool>();
        private readonly Func<ChannelBase, WorkPool> _startNewWorkPoolFunc;

        public AsyncConsumerWorkService(int concurrency) : base(concurrency)
        {
            _startNewWorkPoolFunc = channelBase => StartNewWorkPool(channelBase);
        }

        public void Schedule(ChannelBase channelBase, Work work)
        {
            /*
             * rabbitmq/rabbitmq-dotnet-client#841
             * https://docs.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.getoradd
             * Note that the value delegate is not atomic but the Schedule method will not be called concurrently.
             */
            WorkPool workPool = _workPools.GetOrAdd(channelBase, _startNewWorkPoolFunc);
            workPool.Enqueue(work);
        }

        private WorkPool StartNewWorkPool(ChannelBase channelBase)
        {
            var newWorkPool = new WorkPool(channelBase, _concurrency);
            newWorkPool.Start();
            return newWorkPool;
        }

        public Task Stop(ChannelBase channelBase)
        {
            if (_workPools.TryRemove(channelBase, out WorkPool workPool))
            {
                return workPool.Stop();
            }

            return Task.CompletedTask;
        }

        private class WorkPool
        {
            private readonly Channel<Work> _channel;
            private readonly ChannelBase _channelBase;
            private Task _worker;
            private readonly int _concurrency;
            private SemaphoreSlim _limiter;
            private CancellationTokenSource _tokenSource;

            public WorkPool(ChannelBase channelBase, int concurrency)
            {
                _concurrency = concurrency;
                _channelBase = channelBase;
                _channel = Channel.CreateUnbounded<Work>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false });
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

            public void Enqueue(Work work)
            {
                _channel.Writer.TryWrite(work);
            }

            private async Task Loop()
            {
                while (await _channel.Reader.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (_channel.Reader.TryRead(out Work work))
                    {
                        try
                        {
                            Task task = work.Execute();
                            if (!task.IsCompleted)
                            {
                                await task.ConfigureAwait(false);
                            }
                            else
                            {
                                // to materialize exceptions if any
                                task.GetAwaiter().GetResult();
                            }
                        }
                        catch (Exception e)
                        {
                            _channelBase.OnUnhandledExceptionOccurred(e, work.Context, work.Consumer);
                        }
                        finally
                        {
                            work.PostExecute();
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
                        while (_channel.Reader.TryRead(out Work work))
                        {
                            // Do a quick synchronous check before we resort to async/await with the state-machine overhead.
                            if (!_limiter.Wait(0))
                            {
                                await _limiter.WaitAsync(cancellationToken).ConfigureAwait(false);
                            }

                            _ = HandleConcurrent(work, _channelBase, _limiter);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // ignored
                }
            }

            private static async Task HandleConcurrent(Work work, ChannelBase channelBase, SemaphoreSlim limiter)
            {
                try
                {
                    Task task = work.Execute();
                    if (!task.IsCompleted)
                    {
                        await task.ConfigureAwait(false);
                    }
                    else
                    {
                        // to materialize exceptions if any
                        task.GetAwaiter().GetResult();
                    }
                }
                catch (Exception e)
                {
                    channelBase.OnUnhandledExceptionOccurred(e, work.Context, work.Consumer);
                }
                finally
                {
                    work.PostExecute();
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
