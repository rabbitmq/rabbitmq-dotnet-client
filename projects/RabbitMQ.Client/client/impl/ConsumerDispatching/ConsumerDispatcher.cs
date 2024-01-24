using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.ConsumerDispatching
{
#nullable enable
    internal sealed class ConsumerDispatcher : ConsumerDispatcherChannelBase
    {
        internal ConsumerDispatcher(ChannelBase channel, int concurrency)
            : base(channel, concurrency)
        {
        }

        protected override async Task ProcessChannelAsync(CancellationToken token)
        {
            while (await _reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                while (_reader.TryRead(out var work))
                {
                    using (work)
                    {
                        try
                        {
                            IBasicConsumer consumer = work.Consumer;
                            string? consumerTag = work.ConsumerTag;
                            switch (work.WorkType)
                            {
                                case WorkType.Deliver:
                                    await consumer.HandleBasicDeliverAsync(
                                        consumerTag, work.DeliveryTag, work.Redelivered,
                                        work.Exchange, work.RoutingKey, work.BasicProperties, work.Body.Memory);
                                    break;
                                case WorkType.Cancel:
                                    consumer.HandleBasicCancel(consumerTag);
                                    break;
                                case WorkType.CancelOk:
                                    consumer.HandleBasicCancelOk(consumerTag);
                                    break;
                                case WorkType.ConsumeOk:
                                    consumer.HandleBasicConsumeOk(consumerTag);
                                    break;
                                case WorkType.Shutdown:
                                    consumer.HandleChannelShutdown(_channel, work.Reason);
                                    break;
                            }
                        }
                        catch (Exception e)
                        {
                            _channel.OnCallbackException(CallbackExceptionEventArgs.Build(e, work.WorkType.ToString(), work.Consumer));
                        }
                    }
                }
            }
        }
    }
}
