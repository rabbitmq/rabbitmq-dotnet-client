using System;
using System.Threading;
using System.Threading.Tasks;
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

        public ConsumerDispatcherBase()
        {
            var r = new Random();
            r.NextBytes(_body);
        }
    }

    public class BasicDeliverConsumerDispatching : ConsumerDispatcherBase
    {
        [Params(1, 30)]
        public int Count { get; set; }

        [Params(1, 2)]
        public int Concurrency { get; set; }

        [GlobalSetup(Target = nameof(AsyncConsumerDispatcher))]
        public async Task SetUpAsyncConsumer()
        {
            _consumer.Count = Count;
            _dispatcher = new AsyncConsumerDispatcher(null, Concurrency);
            await _dispatcher.HandleBasicConsumeOkAsync(_consumer, _consumerTag, CancellationToken.None);
        }

        [Benchmark]
        public async Task AsyncConsumerDispatcher()
        {
            using (RentedMemory body = new RentedMemory(_body))
            {
                for (int i = 0; i < Count; i++)
                {
                    await _dispatcher.HandleBasicDeliverAsync(_consumerTag, _deliveryTag, false, _exchange, _routingKey, _properties, body,
                        CancellationToken.None);
                }
                _autoResetEvent.Wait();
                _autoResetEvent.Reset();
            }
        }

        [GlobalSetup(Target = nameof(ConsumerDispatcher))]
        public async Task SetUpConsumer()
        {
            _consumer.Count = Count;
            _dispatcher = new AsyncConsumerDispatcher(null, Concurrency);
            await _dispatcher.HandleBasicConsumeOkAsync(_consumer, _consumerTag, CancellationToken.None);
        }

        [Benchmark]
        public async Task ConsumerDispatcher()
        {
            using (RentedMemory body = new RentedMemory(_body))
            {
                for (int i = 0; i < Count; i++)
                {
                    await _dispatcher.HandleBasicDeliverAsync(_consumerTag, _deliveryTag, false, _exchange, _routingKey, _properties, body,
                        CancellationToken.None);
                }
                _autoResetEvent.Wait();
                _autoResetEvent.Reset();
            }
        }
    }
}
