using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.ConsumerDispatching
{
#nullable enable
    internal sealed class AsyncConsumerDispatcher : ConsumerDispatcherChannelBase
    {
        internal AsyncConsumerDispatcher(ChannelBase channel, int concurrency)
            : base(channel, concurrency)
        {
        }

        protected override async Task ProcessChannelAsync(CancellationToken token)
        {
            while (await _reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                while (_reader.TryRead(out WorkStruct work))
                {
                    using (work)
                    {
                        try
                        {
                            Task task = work.WorkType switch
                            {
                                WorkType.Deliver => work.AsyncConsumer.HandleBasicDeliver(
                                    work.ConsumerTag, work.DeliveryTag, work.Redelivered,
                                    work.Exchange, work.RoutingKey, work.BasicProperties, work.Body.Memory),

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
                    }
                }
            }
        }
    }
}
