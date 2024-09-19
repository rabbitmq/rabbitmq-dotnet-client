using System;
using System.Threading;
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
        public event AsyncEventHandler<BasicDeliverEventArgs> ReceivedAsync
        {
            add => _receivedAsyncWrapper.AddHandler(value);
            remove => _receivedAsyncWrapper.RemoveHandler(value);
        }
        private AsyncEventingWrapper<BasicDeliverEventArgs> _receivedAsyncWrapper;

        ///<summary>Fires when the server confirms successful consumer registration.</summary>
        public event AsyncEventHandler<ConsumerEventArgs> RegisteredAsync
        {
            add => _registeredAsyncWrapper.AddHandler(value);
            remove => _registeredAsyncWrapper.RemoveHandler(value);
        }
        private AsyncEventingWrapper<ConsumerEventArgs> _registeredAsyncWrapper;

        ///<summary>Fires on channel shutdown, both client and server initiated.</summary>
        public event AsyncEventHandler<ShutdownEventArgs> ShutdownAsync
        {
            add => _shutdownAsyncWrapper.AddHandler(value);
            remove => _shutdownAsyncWrapper.RemoveHandler(value);
        }
        private AsyncEventingWrapper<ShutdownEventArgs> _shutdownAsyncWrapper;

        ///<summary>Fires when the server confirms successful consumer cancellation.</summary>
        public event AsyncEventHandler<ConsumerEventArgs> UnregisteredAsync
        {
            add => _unregisteredAsyncWrapper.AddHandler(value);
            remove => _unregisteredAsyncWrapper.RemoveHandler(value);
        }
        private AsyncEventingWrapper<ConsumerEventArgs> _unregisteredAsyncWrapper;

        ///<summary>Fires when the server confirms successful consumer cancellation.</summary>
        protected override async Task OnCancelAsync(string[] consumerTags, CancellationToken cancellationToken = default)
        {
            await base.OnCancelAsync(consumerTags, cancellationToken)
                .ConfigureAwait(false);
            if (!_unregisteredAsyncWrapper.IsEmpty)
            {
                await _unregisteredAsyncWrapper.InvokeAsync(this, new ConsumerEventArgs(consumerTags, cancellationToken))
                    .ConfigureAwait(false);
            }
        }

        ///<summary>Fires when the server confirms successful consumer registration.</summary>
        public override async Task HandleBasicConsumeOkAsync(string consumerTag, CancellationToken cancellationToken = default)
        {
            await base.HandleBasicConsumeOkAsync(consumerTag, cancellationToken)
                .ConfigureAwait(false);
            if (!_registeredAsyncWrapper.IsEmpty)
            {
                await _registeredAsyncWrapper.InvokeAsync(this, new ConsumerEventArgs(new[] { consumerTag }, cancellationToken))
                    .ConfigureAwait(false);
            }
        }

        ///<summary>Fires the Received event.</summary>
        public override Task HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey,
            IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
        {
            var deliverEventArgs = new BasicDeliverEventArgs(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body, cancellationToken);

            // No need to call base, it's empty.
            return _receivedAsyncWrapper.InvokeAsync(this, deliverEventArgs);
        }

        ///<summary>Fires the Shutdown event.</summary>
        public override async Task HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason)
        {
            await base.HandleChannelShutdownAsync(channel, reason)
                .ConfigureAwait(false);
            if (!_shutdownAsyncWrapper.IsEmpty)
            {
                await _shutdownAsyncWrapper.InvokeAsync(this, reason)
                    .ConfigureAwait(false);
            }
        }
    }
}
