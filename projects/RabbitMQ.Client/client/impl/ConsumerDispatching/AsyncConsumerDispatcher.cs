using System;
using System.Buffers;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.ConsumerDispatching
{
#nullable enable
    internal sealed class AsyncConsumerDispatcher : ConsumerDispatcherChannelBase
    {
        public AsyncConsumerDispatcher(ChannelBase channel, int concurrency)
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
                        var task = work.WorkType switch
                        {
                            WorkType.Deliver => work.AsyncConsumer.HandleBasicDeliver(work.ConsumerTag, work.DeliveryTag, work.Redelivered, work.Exchange, work.RoutingKey, work.BasicProperties, work.Body),
                            WorkType.Cancel => work.AsyncConsumer.HandleBasicCancel(work.ConsumerTag),
                            WorkType.CancelOk => work.AsyncConsumer.HandleBasicCancelOk(work.ConsumerTag),
                            WorkType.ConsumeOk => work.AsyncConsumer.HandleBasicConsumeOk(work.ConsumerTag),
                            WorkType.Shutdown => work.AsyncConsumer.HandleChannelShutdown(_channel, work.Reason),
                            _ => Task.CompletedTask
                        };
                        await task.ConfigureAwait(false);
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
