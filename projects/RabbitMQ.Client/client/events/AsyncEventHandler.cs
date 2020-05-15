using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Events
{
    public delegate ValueTask AsyncEventHandler<in TEvent>(object sender, TEvent @event) where TEvent : EventArgs;

    internal static class AsyncEventHandlerExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async ValueTask InvokeAsync<TEvent>(this AsyncEventHandler<TEvent> eventHandler, object sender, TEvent @event) where TEvent : EventArgs
        {
            if(eventHandler != null)
            {
                foreach(AsyncEventHandler<TEvent> handlerInstance in eventHandler.GetInvocationList())
                {
                    ValueTask handlerTask = handlerInstance(sender, @event);
                    if (!handlerTask.IsCompletedSuccessfully)
                    {
                        await handlerTask.ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
