using System;
using System.Buffers;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.ConsumerDispatching
{
#nullable enable
    internal sealed class ConsumerDispatcher : ConsumerDispatcherChannelBase
    {
        public ConsumerDispatcher(ChannelBase channel, int concurrency)
            : base(channel, concurrency)
        {
        }

        protected override async Task ProcessChannelAsync()
        {
            while (await _reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (_reader.TryRead(out var work))
                {
                    try
                    {
                        var consumer = work.Consumer;
                        var consumerTag = work.ConsumerTag;
                        switch (work.WorkType)
                        {
                            case WorkType.Deliver:
                                consumer.HandleBasicDeliver(consumerTag, work.DeliveryTag, work.Redelivered, work.Exchange, work.RoutingKey, work.BasicProperties, work.Body);
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
                    finally
                    {
                        if (work.RentedMethodArray != null)
                        {
                            ArrayPool<byte>.Shared.Return(work.RentedMethodArray);
                        }

                        if (work.RentedArray != null)
                        {
                            ArrayPool<byte>.Shared.Return(work.RentedArray);
                        }
                    }
                }
            }
        }
    }
}
