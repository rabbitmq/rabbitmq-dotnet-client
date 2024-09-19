using System;
using System.Diagnostics;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.ConsumerDispatching
{
    internal sealed class AsyncConsumerDispatcher : ConsumerDispatcherChannelBase
    {
        internal AsyncConsumerDispatcher(ChannelBase channel, ushort concurrency)
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
                                switch (work.WorkType)
                                {
                                    case WorkType.Deliver:
                                        using (Activity? activity = RabbitMQActivitySource.Deliver(work.RoutingKey!, work.Exchange!,
                                            work.DeliveryTag, work.BasicProperties!, work.Body.Size))
                                        {
                                            await work.Consumer.HandleBasicDeliverAsync(
                                                work.ConsumerTag!, work.DeliveryTag, work.Redelivered,
                                                work.Exchange!, work.RoutingKey!, work.BasicProperties!, work.Body.Memory)
                                                .ConfigureAwait(false);
                                        }
                                        break;
                                    case WorkType.Cancel:
                                        await work.Consumer.HandleBasicCancelAsync(work.ConsumerTag!)
                                            .ConfigureAwait(false);
                                        break;
                                    case WorkType.CancelOk:
                                        await work.Consumer.HandleBasicCancelOkAsync(work.ConsumerTag!)
                                            .ConfigureAwait(false);
                                        break;
                                    case WorkType.ConsumeOk:
                                        await work.Consumer.HandleBasicConsumeOkAsync(work.ConsumerTag!)
                                            .ConfigureAwait(false);
                                        break;
                                    case WorkType.Shutdown:
                                        await work.Consumer.HandleChannelShutdownAsync(_channel, work.Reason!)
                                            .ConfigureAwait(false);
                                        break;
                                }
                            }
                            catch (Exception e)
                            {
                                await _channel.OnCallbackExceptionAsync(CallbackExceptionEventArgs.Build(e, work.WorkType.ToString(), work.Consumer))
                                    .ConfigureAwait(false);
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
