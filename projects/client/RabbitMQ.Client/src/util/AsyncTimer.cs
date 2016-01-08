using System;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Util
{
    public sealed class AsyncTimer
    {
        private readonly Func<Task> _callback;

        CancellationTokenSource tokenSource;

        private Task timerTask;

        public AsyncTimer(Func<Task> callback)
        {
            _callback = callback;
        }

        public void Start(TimeSpan dueTime, TimeSpan period)
        {
            tokenSource = new CancellationTokenSource();
            timerTask = Task.Run(async () =>
            {
                try
                {
                    var token = tokenSource.Token;

                    await Task.Delay(dueTime, token).ConfigureAwait(false);

                    while (token.IsCancellationRequested)
                    {
                        await _callback().ConfigureAwait(false);

                        await Task.Delay(period, token).ConfigureAwait(false);

                    }
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        public Task Stop()
        {
            tokenSource.Cancel();
            return timerTask;
        }
    }


    public static class TaskExtensions
    {
        public static void Ignore(this Task task)
        {
        }
    }
}