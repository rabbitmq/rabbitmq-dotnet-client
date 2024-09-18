using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Impl
{
    internal struct AsyncEventingWrapper<TEvent> where TEvent : AsyncEventArgs
    {
        private event AsyncEventHandler<TEvent>? _event;
        private Delegate[]? _handlers;
        private string? _context;
        private Func<Exception, string, CancellationToken, Task>? _onException;

        public readonly bool IsEmpty => _event is null;

        public AsyncEventingWrapper(string context, Func<Exception, string, CancellationToken, Task> onException)
        {
            _event = null;
            _handlers = null;
            _context = context;
            _onException = onException;
        }

        public void AddHandler(AsyncEventHandler<TEvent>? handler)
        {
            _event += handler;
            _handlers = null;
        }

        public void RemoveHandler(AsyncEventHandler<TEvent>? handler)
        {
            _event -= handler;
            _handlers = null;
        }

        // Do not make this function async! (This type is a struct that gets copied at the start of an async method => empty _handlers is copied)
        public Task InvokeAsync(object sender, TEvent parameter)
        {
            Delegate[]? handlers = _handlers;
            if (handlers is null)
            {
                handlers = _event?.GetInvocationList();
                if (handlers is null)
                {
                    return Task.CompletedTask;
                }

                _handlers = handlers;
            }

            return InternalInvoke(handlers, sender, parameter);
        }

        private readonly async Task InternalInvoke(Delegate[] handlers, object sender, TEvent @event)
        {
            foreach (AsyncEventHandler<TEvent> action in handlers)
            {
                try
                {
                    await action(sender, @event)
                        .ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    if (_onException != null)
                    {
                        await _onException(exception, _context!, @event.CancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        public void Takeover(in AsyncEventingWrapper<TEvent> other)
        {
            _event = other._event;
            _handlers = other._handlers;
            _context = other._context;
            _onException = other._onException;
        }
    }
}
