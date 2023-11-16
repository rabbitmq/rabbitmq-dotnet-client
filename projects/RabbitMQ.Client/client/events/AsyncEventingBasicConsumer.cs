using System;
using System.Threading.Tasks;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Events
{
    public class AsyncEventingBasicConsumer : AsyncDefaultBasicConsumer
    {
        ///<summary>Constructor which sets the Channel property to the given value.</summary>
        public AsyncEventingBasicConsumer(IChannel channel) : base(channel)
        {
        }

        ///<summary>
        /// Event fired when a delivery arrives for the consumer.
        /// </summary>
        /// <remarks>
        /// Handlers must copy or fully use delivery body before returning.
        /// Accessing the body at a later point is unsafe as its memory can
        /// be already released.
        /// </remarks>
        public event AsyncEventHandler<BasicDeliverEventArgs> Received
        {
            add => _receivedWrapper.AddHandler(value);
            remove => _receivedWrapper.RemoveHandler(value);
        }
        private AsyncEventingWrapper<BasicDeliverEventArgs> _receivedWrapper;

        ///<summary>Fires when the server confirms successful consumer registration.</summary>
        public event AsyncEventHandler<ConsumerEventArgs> Registered
        {
            add => _registeredWrapper.AddHandler(value);
            remove => _registeredWrapper.RemoveHandler(value);
        }
        private AsyncEventingWrapper<ConsumerEventArgs> _registeredWrapper;

        ///<summary>Fires on channel shutdown, both client and server initiated.</summary>
        public event AsyncEventHandler<ShutdownEventArgs> Shutdown
        {
            add => _shutdownWrapper.AddHandler(value);
            remove => _shutdownWrapper.RemoveHandler(value);
        }
        private AsyncEventingWrapper<ShutdownEventArgs> _shutdownWrapper;

        ///<summary>Fires when the server confirms successful consumer cancellation.</summary>
        public event AsyncEventHandler<ConsumerEventArgs> Unregistered
        {
            add => _unregisteredWrapper.AddHandler(value);
            remove => _unregisteredWrapper.RemoveHandler(value);
        }
        private AsyncEventingWrapper<ConsumerEventArgs> _unregisteredWrapper;

        ///<summary>Fires when the server confirms successful consumer cancellation.</summary>
        public override async Task HandleBasicCancelOk(string consumerTag)
        {
            await base.HandleBasicCancelOk(consumerTag).ConfigureAwait(false);
            if (!_unregisteredWrapper.IsEmpty)
            {
                await _unregisteredWrapper.InvokeAsync(this, new ConsumerEventArgs(new[] { consumerTag })).ConfigureAwait(false);
            }
        }

        ///<summary>Fires when the server confirms successful consumer registration.</summary>
        public override async Task HandleBasicConsumeOk(string consumerTag)
        {
            await base.HandleBasicConsumeOk(consumerTag).ConfigureAwait(false);
            if (!_registeredWrapper.IsEmpty)
            {
                await _registeredWrapper.InvokeAsync(this, new ConsumerEventArgs(new[] { consumerTag })).ConfigureAwait(false);
            }
        }

        ///<summary>Fires the Received event.</summary>
        public override Task HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, ReadOnlyMemory<byte> exchange, ReadOnlyMemory<byte> routingKey, in ReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            // No need to call base, it's empty.
            return _receivedWrapper.InvokeAsync(this, new BasicDeliverEventArgs(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body));
        }

        ///<summary>Fires the Shutdown event.</summary>
        public override async Task HandleChannelShutdown(object channel, ShutdownEventArgs reason)
        {
            await base.HandleChannelShutdown(channel, reason).ConfigureAwait(false);
            if (!_shutdownWrapper.IsEmpty)
            {
                await _shutdownWrapper.InvokeAsync(this, reason).ConfigureAwait(false);
            }
        }
    }
}
