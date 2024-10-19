namespace RabbitMQ.Client
{
    /// <summary>
    /// Channel creation options.
    /// </summary>
    public sealed class CreateChannelOptions
    {
        /// <summary>
        /// Enable or disable publisher confirmations on this channel. Defaults to <c>false</c>
        /// </summary>
        public bool PublisherConfirmationsEnabled { get; set; } = false;

        /// <summary>
        /// Should this library track publisher confirmations for you? Defaults to <c>false</c>
        /// </summary>
        public bool PublisherConfirmationTrackingEnabled { get; set; } = false;

        /// <summary>
        /// If publisher confirmation tracking is enabled, this represents the number of allowed
        /// outstanding publisher confirmations before publishing is blocked.
        ///
        /// Defaults to <c>128</c>
        ///
        /// Set to <c>null</c>, to allow an unlimited number of outstanding confirmations.
        ///
        /// </summary>
        public ushort? MaxOutstandingPublisherConfirmations { get; set; } = 128;

        /// <summary>
        /// Set to a value greater than one to enable concurrent processing. For a concurrency greater than one <see cref="IAsyncBasicConsumer"/>
        /// will be offloaded to the worker thread pool so it is important to choose the value for the concurrency wisely to avoid thread pool overloading.
        /// <see cref="IAsyncBasicConsumer"/> can handle concurrency much more efficiently due to the non-blocking nature of the consumer.
        ///
        /// Defaults to <c>null</c>, which will use the value from <see cref="IConnectionFactory.ConsumerDispatchConcurrency"/>
        ///
        /// For concurrency greater than one this removes the guarantee that consumers handle messages in the order they receive them.
        /// In addition to that consumers need to be thread/concurrency safe.
        /// </summary>
        public ushort? ConsumerDispatchConcurrency { get; set; } = null;

        /// <summary>
        /// The default channel options.
        /// </summary>
        public static CreateChannelOptions Default { get; } = new CreateChannelOptions();
    }
}
