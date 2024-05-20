using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.ConsumerDispatching
{
#nullable enable
    internal abstract class ConsumerDispatcherChannelBase : ConsumerDispatcherBase, IConsumerDispatcher
    {
        protected readonly CancellationTokenSource _consumerDispatcherCts = new CancellationTokenSource();
        protected readonly CancellationToken _consumerDispatcherToken;

        protected readonly ChannelBase _channel;
        protected readonly ChannelReader<WorkStruct> _reader;
        private readonly ChannelWriter<WorkStruct> _writer;
        private readonly Task _worker;
        private bool _quiesce = false;
        private bool _disposed;

        internal ConsumerDispatcherChannelBase(ChannelBase channel, int concurrency)
        {
            _consumerDispatcherToken = _consumerDispatcherCts.Token;
            _channel = channel;
            var workChannel = System.Threading.Channels.Channel.CreateUnbounded<WorkStruct>(new UnboundedChannelOptions
            {
                SingleReader = concurrency == 1,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
            _reader = workChannel.Reader;
            _writer = workChannel.Writer;

            Func<Task> loopStart =
                () => ProcessChannelAsync(_consumerDispatcherToken);
            if (concurrency == 1)
            {
                _worker = Task.Run(loopStart, _consumerDispatcherToken);
            }
            else
            {
                var tasks = new Task[concurrency];
                for (int i = 0; i < concurrency; i++)
                {
                    tasks[i] = Task.Run(loopStart, _consumerDispatcherToken);
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

        public ValueTask HandleBasicCancelAsync(RentedMemory method, CancellationToken cancellationToken)
        {
            if (false == _disposed && false == _quiesce)
            {
                WorkStruct cancelWork = WorkStruct.CreateCancel(method);
                return _writer.WriteAsync(cancelWork, cancellationToken);
            }
            else
            {
                return default;
            }
        }

        public ValueTask HandleBasicCancelOkAsync(RentedMemory method, CancellationToken cancellationToken)
        {
            if (false == _disposed && false == _quiesce)
            {
                WorkStruct cancelOkWork = WorkStruct.CreateCancelOk(method);
                return _writer.WriteAsync(cancelOkWork, cancellationToken);
            }
            else
            {
                return default;
            }
        }

        public ValueTask HandleBasicConsumeOkAsync(IBasicConsumer consumer, ConsumerTag consumerTag, CancellationToken cancellationToken)
        {
            if (false == _disposed && false == _quiesce)
            {
                AddConsumer(consumer, consumerTag);
                WorkStruct consumeOkWork = WorkStruct.CreateConsumeOk(consumer, consumerTag);
                return _writer.WriteAsync(consumeOkWork, cancellationToken);
            }
            else
            {
                return default;
            }
        }

        public ValueTask HandleBasicDeliverAsync(ulong deliveryTag, bool redelivered,
            in ReadOnlyBasicProperties basicProperties, RentedMemory method, RentedMemory body,
            CancellationToken cancellationToken)
        {
            if (false == _disposed && false == _quiesce)
            {
                (IBasicConsumer consumer, _) = GetConsumerOrDefault(BasicDeliver.GetConsumerTag(method.Memory));
                var work = WorkStruct.CreateDeliver(consumer, deliveryTag, redelivered, basicProperties, method, body);
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

        private bool IsCancellationRequested
        {
            get
            {
                try
                {
                    return _consumerDispatcherCts.IsCancellationRequested;
                }
                catch (ObjectDisposedException)
                {
                    return true;
                }
            }
        }

        public void WaitForShutdown()
        {
            if (_disposed)
            {
                return;
            }

            if (_quiesce)
            {
                if (IsCancellationRequested)
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
            CancelConsumerDispatcherCts();
        }

        protected override Task InternalShutdownAsync()
        {
            _writer.Complete();
            CancelConsumerDispatcherCts();
            return _worker;
        }

        protected abstract Task ProcessChannelAsync(CancellationToken token);

        protected readonly struct WorkStruct : IDisposable
        {
            public readonly IBasicConsumer? Consumer;
            public readonly ConsumerTag? ConsumerTag;
            public readonly ulong DeliveryTag;
            public readonly bool Redelivered;
            public readonly ReadOnlyBasicProperties BasicProperties;
            public readonly RentedMemory Method;
            public readonly RentedMemory Body;
            public readonly ShutdownEventArgs? Reason;
            public readonly WorkType WorkType;

            private WorkStruct(IBasicConsumer consumer, ulong deliveryTag, bool redelivered,
                in ReadOnlyBasicProperties basicProperties, RentedMemory method, RentedMemory body)
            {
                WorkType = WorkType.Deliver;
                ConsumerTag = null;
                Consumer = consumer;
                DeliveryTag = deliveryTag;
                Redelivered = redelivered;
                BasicProperties = basicProperties;
                Method = method;
                Body = body;
                Reason = default;
            }

            private WorkStruct(IBasicConsumer consumer, ShutdownEventArgs reason)
                : this()
            {
                WorkType = WorkType.Shutdown;
                Consumer = consumer;
                Reason = reason;
            }

            private WorkStruct(WorkType workType, RentedMemory method)
                : this()
            {
                WorkType = workType;
                Method = method;
            }

            private WorkStruct(WorkType type, IBasicConsumer consumer, ConsumerTag consumerTag)
                : this()
            {
                WorkType = type;
                Consumer = consumer;
                ConsumerTag = consumerTag;
            }

            public static WorkStruct CreateCancel(RentedMemory method)
            {
                return new WorkStruct(WorkType.Cancel, method);
            }

            public static WorkStruct CreateCancelOk(RentedMemory method)
            {
                return new WorkStruct(WorkType.CancelOk, method);
            }

            public static WorkStruct CreateConsumeOk(IBasicConsumer consumer, ConsumerTag consumerTag)
            {
                return new WorkStruct(WorkType.ConsumeOk, consumer, consumerTag);
            }

            public static WorkStruct CreateDeliver(IBasicConsumer consumer, ulong deliveryTag, bool redelivered,
                in ReadOnlyBasicProperties basicProperties, RentedMemory method, RentedMemory body)
            {
                return new WorkStruct(consumer, deliveryTag, redelivered,
                    basicProperties, method, body);
            }

            public static WorkStruct CreateShutdown(IBasicConsumer consumer, ShutdownEventArgs reason)
            {
                return new WorkStruct(consumer, reason);
            }

            public IAsyncBasicConsumer? AsyncConsumer
            {
                get
                {
                    return Consumer as IAsyncBasicConsumer;
                }
            }

            public void Dispose()
            {
                Method.Dispose();
                Body.Dispose();
            }
        }

        protected enum WorkType : byte
        {
            Shutdown,
            Cancel,
            CancelOk,
            Deliver,
            ConsumeOk
        }

        protected void CancelConsumerDispatcherCts()
        {
            try
            {
                _consumerDispatcherCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
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
                        CancelConsumerDispatcherCts();
                        _consumerDispatcherCts.Dispose();
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
