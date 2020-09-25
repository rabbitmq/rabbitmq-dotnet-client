using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Impl
{
    internal sealed class AsyncConsumerWorkService : ConsumerWorkService
    {
        private readonly ConcurrentDictionary<IModel, WorkPool> _workPools = new ConcurrentDictionary<IModel, WorkPool>();
        private readonly Func<IModel, WorkPool> _startNewWorkPoolFunc;

        public AsyncConsumerWorkService(int concurrency) : base(concurrency)
        {
            _startNewWorkPoolFunc = model => StartNewWorkPool(model);
        }

        public void Schedule(IModel model, Work work)
        {
            /*
             * rabbitmq/rabbitmq-dotnet-client#841
             * https://docs.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.getoradd
             * Note that the value delegate is not atomic but the Schedule method will not be called concurrently.
             */
            WorkPool workPool = _workPools.GetOrAdd(model, _startNewWorkPoolFunc);
            workPool.Enqueue(work);
        }

        private WorkPool StartNewWorkPool(IModel model)
        {
            var newWorkPool = new WorkPool(model, _concurrency);
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

        private class WorkPool
        {
            private readonly Channel<Work> _channel;
            private readonly IModel _model;
            private Task _worker;
            private readonly int _concurrency;
            private SemaphoreSlim _limiter;
            private CancellationTokenSource _tokenSource;

            public WorkPool(IModel model, int concurrency)
            {
                _concurrency = concurrency;
                _model = model;
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
                            if (!(_model is ModelBase modelBase))
                            {
                                return;
                            }

                            var details = new Dictionary<string, object>
                            {
                                { "consumer", work.Consumer },
                                { "context", work.Consumer }
                            };
                            modelBase.OnCallbackException(CallbackExceptionEventArgs.Build(e, details));
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

                            _ = HandleConcurrent(work, _model, _limiter);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // ignored
                }
            }

            private static async Task HandleConcurrent(Work work, IModel model, SemaphoreSlim limiter)
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
                    if (!(model is ModelBase modelBase))
                    {
                        return;
                    }

                    var details = new Dictionary<string, object>
                    {
                        { "consumer", work.Consumer },
                        { "context", work.Consumer }
                    };
                    modelBase.OnCallbackException(CallbackExceptionEventArgs.Build(e, details));
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
