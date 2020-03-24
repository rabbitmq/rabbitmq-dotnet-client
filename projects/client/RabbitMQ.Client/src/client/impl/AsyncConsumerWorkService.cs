using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client
{
    internal sealed class AsyncConsumerWorkService : ConsumerWorkService
    {
        private readonly ConcurrentDictionary<IModel, WorkPool> _workPools = new ConcurrentDictionary<IModel, WorkPool>();

        public void Schedule<TWork>(ModelBase model, TWork work) where TWork : Work
        {
            _workPools.GetOrAdd(model, StartNewWorkPool).Enqueue(work);
        }

        private WorkPool StartNewWorkPool(IModel model)
        {
            var newWorkPool = new WorkPool(model as ModelBase);
            newWorkPool.Start();
            return newWorkPool;
        }

        public void Stop(IModel model)
        {
            if (_workPools.TryRemove(model, out WorkPool workPool))
            {
                workPool.Stop();
            }
        }

        public void Stop()
        {
            foreach (IModel model in _workPools.Keys)
            {
                Stop(model);
            }
        }

        class WorkPool
        {
            readonly ConcurrentQueue<Work> _workQueue;
            readonly CancellationTokenSource _tokenSource;
            readonly ModelBase _model;
            readonly CancellationTokenRegistration _tokenRegistration;
            volatile TaskCompletionSource<bool> _syncSource = TaskCompletionSourceFactory.Create<bool>();
            private Task _worker;

            public WorkPool(ModelBase model)
            {
                _model = model;
                _workQueue = new ConcurrentQueue<Work>();
                _tokenSource = new CancellationTokenSource();
                _tokenRegistration = _tokenSource.Token.Register(() => _syncSource.TrySetCanceled());
            }

            public void Start()
            {
                _worker = Task.Run(Loop, CancellationToken.None);
            }

            public void Enqueue(Work work)
            {
                _workQueue.Enqueue(work);
                _syncSource.TrySetResult(true);
            }

            async Task Loop()
            {
                while (_tokenSource.IsCancellationRequested == false)
                {
                    try
                    {
                        await _syncSource.Task.ConfigureAwait(false);
                        _syncSource = TaskCompletionSourceFactory.Create<bool>();
                    }
                    catch (TaskCanceledException)
                    {
                        // Swallowing the task cancellation in case we are stopping work.
                    }

                    while (_tokenSource.IsCancellationRequested == false && _workQueue.TryDequeue(out Work work))
                    {
                        await work.Execute(_model).ConfigureAwait(false);
                    }
                }
            }

            public void Stop()
            {
                _tokenSource.Cancel();
                _tokenRegistration.Dispose();
            }

            static class TaskCompletionSourceFactory
            {
#if NETFRAMEWORK
                static readonly FieldInfo StateField = typeof(Task).GetField("m_stateFlags", BindingFlags.NonPublic | BindingFlags.Instance);
#endif
        
                public static TaskCompletionSource<T> Create<T>(TaskCreationOptions options = TaskCreationOptions.None)
                {
#if NETFRAMEWORK
                    //This lovely hack forces the task scheduler to run continuations asynchronously,
                    //see https://stackoverflow.com/questions/22579206/how-can-i-prevent-synchronous-continuations-on-a-task/22588431#22588431
                    var tcs = new TaskCompletionSource<T>(options);
                    const int TASK_STATE_THREAD_WAS_ABORTED = 134217728;
                    StateField.SetValue(tcs.Task, (int) StateField.GetValue(tcs.Task) | TASK_STATE_THREAD_WAS_ABORTED);
                    return tcs;
#else
                    return new TaskCompletionSource<T>(options | TaskCreationOptions.RunContinuationsAsynchronously);
#endif
                }
            }
        }
    }
}
