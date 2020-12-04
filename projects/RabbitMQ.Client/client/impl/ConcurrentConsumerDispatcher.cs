using System;
using System.Buffers;
using System.Threading.Tasks;
using RabbitMQ.Client.client.impl.Channel;

namespace RabbitMQ.Client.Impl
{
    internal sealed class ConcurrentConsumerDispatcher : IConsumerDispatcher
    {
        private readonly ChannelBase _channelBase;
        private readonly ConsumerWorkService _workService;

        public ConcurrentConsumerDispatcher(ChannelBase channelBase, ConsumerWorkService ws)
        {
            _channelBase = channelBase;
            _workService = ws;
            IsShutdown = false;
        }

        public void Quiesce()
        {
            IsShutdown = true;
        }

        public Task Shutdown()
        {
            return _workService.StopWorkAsync(_channelBase);
        }

        public bool IsShutdown
        {
            get;
            private set;
        }

        public void HandleBasicConsumeOk(IBasicConsumer consumer,
                                         string consumerTag)
        {
            UnlessShuttingDown(() =>
            {
                try
                {
                    consumer.HandleBasicConsumeOk(consumerTag);
                }
                catch (Exception e)
                {
                    _channelBase.OnUnhandledExceptionOccurred(e, "HandleBasicConsumeOk", consumer);
                }
            });
        }

        public void HandleBasicDeliver(IBasicConsumer consumer,
                                       string consumerTag,
                                       ulong deliveryTag,
                                       bool redelivered,
                                       string exchange,
                                       string routingKey,
                                       IBasicProperties basicProperties,
                                       ReadOnlyMemory<byte> body,
                                       byte[] rentedArray)
        {
            UnlessShuttingDown(() =>
            {
                try
                {
                    consumer.HandleBasicDeliver(consumerTag,
                                                deliveryTag,
                                                redelivered,
                                                exchange,
                                                routingKey,
                                                basicProperties,
                                                body);
                }
                catch (Exception e)
                {
                    _channelBase.OnUnhandledExceptionOccurred(e, "HandleBasicDeliver", consumer);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rentedArray);
                }
            });
        }

        public void HandleBasicCancelOk(IBasicConsumer consumer, string consumerTag)
        {
            UnlessShuttingDown(() =>
            {
                try
                {
                    consumer.HandleBasicCancelOk(consumerTag);
                }
                catch (Exception e)
                {
                    _channelBase.OnUnhandledExceptionOccurred(e, "HandleBasicCancelOk", consumer);
                }
            });
        }

        public void HandleBasicCancel(IBasicConsumer consumer, string consumerTag)
        {
            UnlessShuttingDown(() =>
            {
                try
                {
                    consumer.HandleBasicCancel(consumerTag);
                }
                catch (Exception e)
                {
                    _channelBase.OnUnhandledExceptionOccurred(e, "HandleBasicCancel", consumer);
                }
            });
        }

        public void HandleModelShutdown(IBasicConsumer consumer, ShutdownEventArgs reason)
        {
            // the only case where we ignore the shutdown flag.
            try
            {
                consumer.HandleModelShutdown(_channelBase, reason);
            }
            catch (Exception e)
            {
                _channelBase.OnUnhandledExceptionOccurred(e, "HandleModelShutdown", consumer);
            }
        }

        private void UnlessShuttingDown(Action fn)
        {
            if (!IsShutdown)
            {
                Execute(fn);
            }
        }

        private void Execute(Action fn)
        {
            _workService.AddWork(_channelBase, fn);
        }
    }
}
