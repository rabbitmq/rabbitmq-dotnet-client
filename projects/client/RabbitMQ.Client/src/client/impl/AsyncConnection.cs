using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing.Impl
{
    internal class AsyncConnection : Connection, IAsyncConnection
    {
        public AsyncConnection(IConnectionFactory factory, bool insist, IFrameHandler frameHandler, string clientProvidedName = null) : 
            base(factory, insist, frameHandler, clientProvidedName)
        {
            AsyncConsumerWorkService = new AsyncConsumerWorkService();
        }

        public AsyncConsumerWorkService AsyncConsumerWorkService { get; }
    }
}