using System;

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

            foreach (EventHandler<T> action in handlers)
            {
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
}
