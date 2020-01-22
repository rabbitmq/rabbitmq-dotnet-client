using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Exceptions;

namespace RabbitMQ.Client.Impl
{
    public class AsyncRpcContinuation : IRpcContinuation
    {
        private readonly TaskCompletionSource<Command> _taskCompletionSource = new TaskCompletionSource<Command>(TaskCreationOptions.RunContinuationsAsynchronously);

        public virtual async ValueTask<Command> GetReplyAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (cancellationToken != default)
                {
                    using (cancellationToken.Register(() => _taskCompletionSource.TrySetCanceled(cancellationToken)))
                    {
                        return await _taskCompletionSource.Task.ConfigureAwait(false);
                    }
                }

                return await _taskCompletionSource.Task.ConfigureAwait(false);
            }
            catch (OperationInterruptedException)
            {
                throw;
            }
        }

        public virtual async ValueTask<Command> GetReplyAsync(TimeSpan timeout)
        {
            using (CancellationTokenSource cts = new CancellationTokenSource(timeout))
            {
                return await GetReplyAsync(cts.Token).ConfigureAwait(false);
            }
        }

        public void HandleCommand(Command cmd)
        {
            _taskCompletionSource.TrySetResult(cmd);
        }

        public void HandleModelShutdown(ShutdownEventArgs reason)
        {
            _taskCompletionSource.TrySetException(new OperationInterruptedException(reason));
        }
    }
}
