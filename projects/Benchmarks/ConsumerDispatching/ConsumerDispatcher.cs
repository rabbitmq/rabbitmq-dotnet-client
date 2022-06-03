using System.Threading;
using BenchmarkDotNet.Attributes;
using RabbitMQ.Client;
using RabbitMQ.Client.ConsumerDispatching;

namespace RabbitMQ.Benchmarks
{
    [Config(typeof(Config))]
    [BenchmarkCategory("ConsumerDispatcher")]
    public class ConsumerDispatcherBase
    {
        protected static readonly ManualResetEventSlim _autoResetEvent = new ManualResetEventSlim(false);

        private protected IConsumerDispatcher _dispatcher;
        private protected readonly AsyncBasicConsumerFake _consumer = new AsyncBasicConsumerFake(_autoResetEvent);
        protected readonly string _consumerTag = "ConsumerTag";
        protected readonly ulong _deliveryTag = 500UL;
        protected readonly string _exchange = "Exchange";
        protected readonly string _routingKey = "RoutingKey";
        protected readonly ReadOnlyBasicProperties _properties = new ReadOnlyBasicProperties();
        protected readonly byte[] _body = new byte[512];
    }

    public class BasicDeliverConsumerDispatching : ConsumerDispatcherBase
    {
        [Params(1, 30)]
        public int Count { get; set; }

        [Params(1, 2)]
        public int Concurrency { get; set; }

        [GlobalSetup(Target = nameof(AsyncConsumerDispatcher))]
        public void SetUpAsyncConsumer()
        {
            _consumer.Count = Count;
            _dispatcher = new ConsumerDispatcher(null, Concurrency);
            _dispatcher.HandleBasicConsumeOk(_consumer, _consumerTag);
        }
        [Benchmark]
        public void AsyncConsumerDispatcher()
        {
            for (int i = 0; i < Count; i++)
            {
                _dispatcher.HandleBasicDeliver(_consumerTag, _deliveryTag, false, _exchange, _routingKey, _properties, _body, _body);
            }
            _autoResetEvent.Wait();
            _autoResetEvent.Reset();
        }

        [GlobalSetup(Target = nameof(ConsumerDispatcher))]
        public void SetUpConsumer()
        {
            _consumer.Count = Count;
            _dispatcher = new ConsumerDispatcher(null, Concurrency);
            _dispatcher.HandleBasicConsumeOk(_consumer, _consumerTag);
        }
        [Benchmark]
        public void ConsumerDispatcher()
        {
            for (int i = 0; i < Count; i++)
            {
                _dispatcher.HandleBasicDeliver(_consumerTag, _deliveryTag, false, _exchange, _routingKey, _properties, _body, _body);
            }
            _autoResetEvent.Wait();
            _autoResetEvent.Reset();
        }
    }
}
