using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Impl
{
#nullable enable
    internal struct EventingWrapper<T>
    {
        private event EventHandler<T>? _event;
        private Delegate[]? _handlers;
        private string? _context;
        private Action<Exception, string>? _onExceptionAction;

        public readonly bool IsEmpty => _event is null;

        public EventingWrapper(string context, Action<Exception, string> onExceptionAction)
        {
            _event = null;
            _handlers = null;
            _context = context;
            _onExceptionAction = onExceptionAction;
        }

        public void AddHandler(EventHandler<T>? handler)
        {
            _event += handler;
            _handlers = null;
        }

        public void RemoveHandler(EventHandler<T>? handler)
        {
            _event -= handler;
            _handlers = null;
        }

        public void ClearHandlers()
        {
            _event = null;
            _handlers = null;
        }

        public void Invoke(object sender, T parameter)
        {
            Delegate[]? handlers = _handlers;
            if (handlers is null)
            {
                handlers = _event?.GetInvocationList();
                if (handlers is null)
                {
                    return;
                }

                _handlers = handlers;
            }

            for (int i = 0; i < handlers.Length; i++)
            {
                EventHandler<T> action = (EventHandler<T>)handlers[i];
                try
                {
                    action(sender, parameter);
                }
                catch (Exception exception)
                {
                    Action<Exception, string>? onException = _onExceptionAction;
                    if (onException != null)
                    {
                        onException(exception, _context!);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        public void Takeover(in EventingWrapper<T> other)
        {
            _event = other._event;
            _handlers = other._handlers;
            _context = other._context;
            _onExceptionAction = other._onExceptionAction;
        }
    }

    internal struct AsyncEventingWrapper<T>
    {
        private event AsyncEventHandler<T>? _event;
        private Delegate[]? _handlers;

        public readonly bool IsEmpty => _event is null;

        public void AddHandler(AsyncEventHandler<T>? handler)
        {
            _event += handler;
            _handlers = null;
        }

        public void RemoveHandler(AsyncEventHandler<T>? handler)
        {
            _event -= handler;
            _handlers = null;
        }

        // NOTE:
        // Do not make this function async!
        // (This type is a struct that gets copied at the start of an async method => empty _handlers is copied)
        public Task InvokeAsync(object sender, T parameter,
            CancellationToken cancellationToken)
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

            if (handlers.Length == 1)
            {
                return ((AsyncEventHandler<T>)handlers[0])(sender, parameter, cancellationToken);
            }

            return InternalInvoke(handlers, sender, parameter);
        }

        private static async Task InternalInvoke(Delegate[] handlers, object sender, T parameter)
        {
            for (int i = 0; i < handlers.Length; i++)
            {
                AsyncEventHandler<T> action = (AsyncEventHandler<T>)handlers[i];
                await action(sender, parameter)
                    .ConfigureAwait(false);
            }
        }

        public void Takeover(in AsyncEventingWrapper<T> other)
        {
            _event = other._event;
            _handlers = other._handlers;
        }
    }
}
