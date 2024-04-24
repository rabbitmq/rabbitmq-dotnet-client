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

        protected override async Task ProcessChannelAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (await _reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (_reader.TryRead(out WorkStruct work))
                    {
                        using (work)
                        {
                            try
                            {
                                Task task = work.WorkType switch
                                {
                                    WorkType.Deliver => work.AsyncConsumer.HandleBasicDeliverAsync(
                                        work.ConsumerTag, work.DeliveryTag, work.Redelivered,
                                        work.Exchange, work.RoutingKey, work.BasicProperties, work.Body.Memory,
                                        cancellationToken: cancellationToken),

                                    WorkType.Cancel => work.AsyncConsumer.HandleBasicCancelAsync(work.ConsumerTag),

                                    WorkType.CancelOk => work.AsyncConsumer.HandleBasicCancelOkAsync(work.ConsumerTag),

                                    WorkType.ConsumeOk => work.AsyncConsumer.HandleBasicConsumeOkAsync(work.ConsumerTag),

                                    WorkType.Shutdown => work.AsyncConsumer.HandleChannelShutdownAsync(_channel, work.Reason),

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
            catch (OperationCanceledException ex)
            {
                if (ex.CancellationToken != _consumerDispatcherToken)
                {
                    throw;
                }
            }
        }
    }
}
