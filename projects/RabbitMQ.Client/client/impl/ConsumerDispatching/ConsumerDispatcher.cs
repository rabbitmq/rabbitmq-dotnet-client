using System;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.ConsumerDispatching
{
    internal sealed class ConsumerDispatcher : ConsumerDispatcherChannelBase
    {
        internal ConsumerDispatcher(ChannelBase channel, int concurrency)
            : base(channel, concurrency)
        {
        }

        protected override async Task ProcessChannelAsync()
        {
            try
            {
                while (await _reader.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (_reader.TryRead(out WorkStruct work))
                    {
                        using (work)
                        {
                            try
                            {
                                IBasicConsumer consumer = work.Consumer;
                                switch (work.WorkType)
                                {
                                    case WorkType.Deliver:
                                        await consumer.HandleBasicDeliverAsync(
                                            work.ConsumerTag!, work.DeliveryTag, work.Redelivered,
                                            work.Exchange!, work.RoutingKey!, work.BasicProperties!, work.Body.Memory)
                                            .ConfigureAwait(false);
                                        break;
                                    case WorkType.Cancel:
                                        consumer.HandleBasicCancel(work.ConsumerTag!);
                                        break;
                                    case WorkType.CancelOk:
                                        consumer.HandleBasicCancelOk(work.ConsumerTag!);
                                        break;
                                    case WorkType.ConsumeOk:
                                        consumer.HandleBasicConsumeOk(work.ConsumerTag!);
                                        break;
                                    case WorkType.Shutdown:
                                        consumer.HandleChannelShutdown(_channel, work.Reason!);
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
            catch (OperationCanceledException)
            {
                if (false == _reader.Completion.IsCompleted)
                {
                    throw;
                }
            }
        }
    }
}
