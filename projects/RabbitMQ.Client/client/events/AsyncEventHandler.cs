using System;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Events
{
    public delegate Task AsyncEventHandler<in TEvent>(object sender, TEvent @event) where TEvent : EventArgs;

    internal static class AsyncEventHandlerExtensions
    {
        public static async Task InvokeAsync<TEvent>(this AsyncEventHandler<TEvent> eventHandler, object sender, TEvent @event) where TEvent : EventArgs
        {
            if(eventHandler != null)
            {
                foreach(AsyncEventHandler<TEvent> handlerInstance in eventHandler.GetInvocationList())
                {
                    await handlerInstance(sender, @event).ConfigureAwait(false);
                }
            }
        }
    }
}
