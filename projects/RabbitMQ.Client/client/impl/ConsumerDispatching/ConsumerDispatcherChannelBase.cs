using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.ConsumerDispatching
{
#nullable enable
    internal abstract class ConsumerDispatcherChannelBase : ConsumerDispatcherBase, IConsumerDispatcher
    {
        protected readonly ChannelBase _channel;
        protected readonly ChannelReader<WorkStruct> _reader;
        private readonly ChannelWriter<WorkStruct> _writer;
        private readonly Task _worker;
        private bool _quiesce = false;
        private bool _disposed;

        internal ConsumerDispatcherChannelBase(ChannelBase channel, int concurrency)
        {
            _channel = channel;
            var workChannel = Channel.CreateUnbounded<WorkStruct>(new UnboundedChannelOptions
            {
                SingleReader = concurrency == 1,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
            _reader = workChannel.Reader;
            _writer = workChannel.Writer;

            Func<Task> loopStart = ProcessChannelAsync;
            if (concurrency == 1)
            {
                _worker = Task.Run(loopStart);
            }
            else
            {
                var tasks = new Task[concurrency];
                for (int i = 0; i < concurrency; i++)
                {
                    tasks[i] = Task.Run(loopStart);
                }
                _worker = Task.WhenAll(tasks);
            }
        }

        public bool IsShutdown
        {
            get
            {
                return _quiesce;
            }
        }

        public ValueTask HandleBasicConsumeOkAsync(IBasicConsumer consumer, string consumerTag, CancellationToken cancellationToken)
        {
            if (false == _disposed && false == _quiesce)
            {
                AddConsumer(consumer, consumerTag);
                WorkStruct work = WorkStruct.CreateConsumeOk(consumer, consumerTag);
                return _writer.WriteAsync(work, cancellationToken);
            }
            else
            {
                return default;
            }
        }

        public ValueTask HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered,
            string exchange, string routingKey, ReadOnlyBasicProperties basicProperties, RentedMemory body,
            CancellationToken cancellationToken)
        {
            if (false == _disposed && false == _quiesce)
            {
                IBasicConsumer consumer = GetConsumerOrDefault(consumerTag);
                var work = WorkStruct.CreateDeliver(consumer, consumerTag, deliveryTag, redelivered, exchange, routingKey, basicProperties, body);
                return _writer.WriteAsync(work, cancellationToken);
            }
            else
            {
                return default;
            }
        }

        public ValueTask HandleBasicCancelOkAsync(string consumerTag, CancellationToken cancellationToken)
        {
            if (false == _disposed && false == _quiesce)
            {
                IBasicConsumer consumer = GetAndRemoveConsumer(consumerTag);
                WorkStruct work = WorkStruct.CreateCancelOk(consumer, consumerTag);
                return _writer.WriteAsync(work, cancellationToken);
            }
            else
            {
                return default;
            }
        }

        public ValueTask HandleBasicCancelAsync(string consumerTag, CancellationToken cancellationToken)
        {
            if (false == _disposed && false == _quiesce)
            {
                IBasicConsumer consumer = GetAndRemoveConsumer(consumerTag);
                WorkStruct work = WorkStruct.CreateCancel(consumer, consumerTag);
                return _writer.WriteAsync(work, cancellationToken);
            }
            else
            {
                return default;
            }
        }

        public void Quiesce()
        {
            _quiesce = true;
        }

        public void WaitForShutdown()
        {
            if (_disposed)
            {
                return;
            }

            if (_quiesce)
            {
                try
                {
                    if (false == _reader.Completion.Wait(TimeSpan.FromSeconds(2)))
                    {
                        ESLog.Warn("consumer dispatcher did not shut down in a timely fashion (sync)");
                    }
                    if (false == _worker.Wait(TimeSpan.FromSeconds(2)))
                    {
                        ESLog.Warn("consumer dispatcher did not shut down in a timely fashion (sync)");
                    }
                }
                catch (AggregateException aex)
                {
                    AggregateException aexf = aex.Flatten();
                    bool foundUnexpectedException = false;
                    foreach (Exception innerAexf in aexf.InnerExceptions)
                    {
                        if (false == (innerAexf is OperationCanceledException))
                        {
                            foundUnexpectedException = true;
                            break;
                        }
                    }
                    if (foundUnexpectedException)
                    {
                        ESLog.Warn("consumer dispatcher task had unexpected exceptions");
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }
            else
            {
                throw new InvalidOperationException("WaitForShutdown called but _quiesce is false");
            }
        }

        public async Task WaitForShutdownAsync()
        {
            if (_disposed)
            {
                return;
            }

            if (_quiesce)
            {
                try
                {
                    await _reader.Completion
                        .ConfigureAwait(false);
                    await _worker
                        .ConfigureAwait(false);
                }
                catch (AggregateException aex)
                {
                    AggregateException aexf = aex.Flatten();
                    bool foundUnexpectedException = false;
                    foreach (Exception innerAexf in aexf.InnerExceptions)
                    {
                        if (false == (innerAexf is OperationCanceledException))
                        {
                            foundUnexpectedException = true;
                            break;
                        }
                    }
                    if (foundUnexpectedException)
                    {
                        ESLog.Warn("consumer dispatcher task had unexpected exceptions (async)");
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }
            else
            {
                throw new InvalidOperationException("WaitForShutdownAsync called but _quiesce is false");
            }
        }

        protected sealed override void ShutdownConsumer(IBasicConsumer consumer, ShutdownEventArgs reason)
        {
            _writer.TryWrite(WorkStruct.CreateShutdown(consumer, reason));
        }

        protected override void InternalShutdown()
        {
            _writer.Complete();
        }

        protected override Task InternalShutdownAsync()
        {
            _writer.Complete();
            return _worker;
        }

        protected abstract Task ProcessChannelAsync();

        protected readonly struct WorkStruct : IDisposable
        {
            public readonly IBasicConsumer Consumer;
            public IAsyncBasicConsumer AsyncConsumer => (IAsyncBasicConsumer)Consumer;
            public readonly string? ConsumerTag;
            public readonly ulong DeliveryTag;
            public readonly bool Redelivered;
            public readonly string? Exchange;
            public readonly string? RoutingKey;
            public readonly ReadOnlyBasicProperties BasicProperties;
            public readonly RentedMemory Body;
            public readonly ShutdownEventArgs? Reason;
            public readonly WorkType WorkType;

            private WorkStruct(WorkType type, IBasicConsumer consumer, string consumerTag)
                : this()
            {
                WorkType = type;
                Consumer = consumer;
                ConsumerTag = consumerTag;
            }

            private WorkStruct(IBasicConsumer consumer, ShutdownEventArgs reason)
                : this()
            {
                WorkType = WorkType.Shutdown;
                Consumer = consumer;
                Reason = reason;
            }

            private WorkStruct(IBasicConsumer consumer, string consumerTag, ulong deliveryTag, bool redelivered,
                string exchange, string routingKey, ReadOnlyBasicProperties basicProperties, RentedMemory body)
            {
                WorkType = WorkType.Deliver;
                Consumer = consumer;
                ConsumerTag = consumerTag;
                DeliveryTag = deliveryTag;
                Redelivered = redelivered;
                Exchange = exchange;
                RoutingKey = routingKey;
                BasicProperties = basicProperties;
                Body = body;
                Reason = default;
            }

            public static WorkStruct CreateCancel(IBasicConsumer consumer, string consumerTag)
            {
                return new WorkStruct(WorkType.Cancel, consumer, consumerTag);
            }

            public static WorkStruct CreateCancelOk(IBasicConsumer consumer, string consumerTag)
            {
                return new WorkStruct(WorkType.CancelOk, consumer, consumerTag);
            }

            public static WorkStruct CreateConsumeOk(IBasicConsumer consumer, string consumerTag)
            {
                return new WorkStruct(WorkType.ConsumeOk, consumer, consumerTag);
            }

            public static WorkStruct CreateShutdown(IBasicConsumer consumer, ShutdownEventArgs reason)
            {
                return new WorkStruct(consumer, reason);
            }

            public static WorkStruct CreateDeliver(IBasicConsumer consumer, string consumerTag, ulong deliveryTag, bool redelivered,
                string exchange, string routingKey, ReadOnlyBasicProperties basicProperties, RentedMemory body)
            {
                return new WorkStruct(consumer, consumerTag, deliveryTag, redelivered,
                    exchange, routingKey, basicProperties, body);
            }

            public void Dispose() => Body.Dispose();
        }

        protected enum WorkType : byte
        {
            Shutdown,
            Cancel,
            CancelOk,
            Deliver,
            ConsumeOk
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                try
                {
                    if (disposing)
                    {
                        Quiesce();
                    }
                }
                catch
                {
                    // CHOMP
                }
                finally
                {
                    _disposed = true;
                }
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
