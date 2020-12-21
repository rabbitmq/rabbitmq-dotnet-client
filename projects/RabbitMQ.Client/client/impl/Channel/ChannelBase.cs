using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.client.impl.Channel
{
    #nullable enable
    public abstract class ChannelBase : IDisposable, IAsyncDisposable
    {
        // TODO 8.0 - Call ModelRpc - this should be private
        private protected readonly object _rpcLock = new object();
        private protected readonly ManualResetEventSlim _flowControlBlock = new ManualResetEventSlim(true);
        private readonly RpcContinuationQueue _continuationQueue = new RpcContinuationQueue();

        private ShutdownEventArgs? _closeReason;

        private protected ISession Session { get; }

        /// <inheritdoc cref="IChannel.ChannelNumber"/> />
        public int ChannelNumber => Session.ChannelNumber;

        /// <inheritdoc cref="IChannel.ContinuationTimeout"/> />
        public TimeSpan ContinuationTimeout { get; set; }

        /// <inheritdoc cref="IChannel.IsOpen"/> />
        public bool IsOpen => CloseReason is null;

        /// <inheritdoc cref="IChannel.CloseReason"/> />
        public ShutdownEventArgs? CloseReason => _closeReason;

        private protected ChannelBase(ISession session)
        {
            Session = session;
            session.CommandReceived = HandleCommand;
            session.SessionShutdown += HandleSessionShutdown;
        }

        /// <inheritdoc cref="IChannel.UnhandledExceptionOccurred"/> />
        public event Action<Exception, Dictionary<string, object>?>? UnhandledExceptionOccurred;

        internal void OnUnhandledExceptionOccurred(Exception exception, string context, object consumer)
        {
            var handler = UnhandledExceptionOccurred;
            if (handler != null)
            {
                var details = new Dictionary<string, object>
                {
                    {"consumer", consumer},
                    {"context", context}
                };
                foreach (Action<Exception, Dictionary<string, object>?> action in handler.GetInvocationList())
                {
                    try
                    {
                        action(exception, details);
                    }
                    catch
                    {
                        // Exception in Callback-exception-handler. That was the app's last chance. Swallow the exception.
                        // FIXME: proper logging
                    }
                }
            }
        }

        internal void OnUnhandledExceptionOccurred(Exception exception, string context)
        {
            var handler = UnhandledExceptionOccurred;
            if (handler != null)
            {
                var details = new Dictionary<string, object>
                {
                    {"context", context}
                };
                foreach (Action<Exception, Dictionary<string, object>?> action in handler.GetInvocationList())
                {
                    try
                    {
                        action(exception, details);
                    }
                    catch
                    {
                        // Exception in Callback-exception-handler. That was the app's last chance. Swallow the exception.
                        // FIXME: proper logging
                    }
                }
            }
        }

        protected virtual void HandleSessionShutdown(object? sender, ShutdownEventArgs reason)
        {
            SetCloseReason(reason);
            GetRpcContinuation().HandleModelShutdown(reason);
            _flowControlBlock.Set();
        }

        protected bool SetCloseReason(ShutdownEventArgs reason)
        {
            return System.Threading.Interlocked.CompareExchange(ref _closeReason, reason, null) is null;
        }

        private protected T ModelRpc<T>(MethodBase method) where T : MethodBase
        {
            var k = new SimpleBlockingRpcContinuation();
            var outgoingCommand = new OutgoingCommand(method);
            MethodBase baseResult;
            lock (_rpcLock)
            {
                TransmitAndEnqueue(outgoingCommand, k);
                baseResult = k.GetReply(ContinuationTimeout).Method;
            }

            if (baseResult is T result)
            {
                return result;
            }

            throw new UnexpectedMethodException(baseResult.ProtocolClassId, baseResult.ProtocolMethodId, baseResult.ProtocolMethodName);
        }

        private protected void ModelSend(MethodBase method)
        {
            ModelSend(method, null, ReadOnlyMemory<byte>.Empty);
        }

        private protected void ModelSend(MethodBase method, ContentHeaderBase? header, ReadOnlyMemory<byte> body)
        {
            if (method.HasContent)
            {
                _flowControlBlock.Wait();
                Session.Transmit(new OutgoingCommand(method, header, body));
            }
            else
            {
                Session.Transmit(new OutgoingCommand(method, header, body));
            }
        }

        private protected void SendCommands(List<OutgoingCommand> commands)
        {
            _flowControlBlock.Wait();
            Session.Transmit(commands);
        }

        private void TransmitAndEnqueue(in OutgoingCommand cmd, IRpcContinuation k)
        {
            Enqueue(k);
            Session.Transmit(cmd);
        }

        // TODO 8.0 - Call ModelRpc - this should be private
        private protected void Enqueue(IRpcContinuation k)
        {
            if (CloseReason is null)
            {
                _continuationQueue.Enqueue(k);
            }
            else
            {
                k.HandleModelShutdown(CloseReason);
            }
        }

        private protected IRpcContinuation GetRpcContinuation()
        {
            return _continuationQueue.Next();
        }

        private void HandleCommand(in IncomingCommand cmd)
        {
            if (!DispatchAsynchronous(in cmd)) // Was asynchronous. Already processed. No need to process further.
            {
                _continuationQueue.Next().HandleCommand(in cmd);
            }
        }

        private protected abstract bool DispatchAsynchronous(in IncomingCommand cmd);

        protected void TakeOverChannel(ChannelBase channel)
        {
            UnhandledExceptionOccurred = channel.UnhandledExceptionOccurred;
        }

        /// <inheritdoc />
        public override string? ToString()
        {
            return Session.ToString();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);

            Dispose(false);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _flowControlBlock.Dispose();
            }
        }

        protected virtual ValueTask DisposeAsyncCore()
        {
            _flowControlBlock.Dispose();
            return default;
        }
    }
}
