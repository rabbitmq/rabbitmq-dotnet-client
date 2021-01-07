using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.client.impl.ConsumerDispatching
{
    internal sealed class AsyncConsumerDispatcher : ConsumerDispatcherChannelBase
    {
        public AsyncConsumerDispatcher(ModelBase model, int concurrency)
            : base(model, concurrency)
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
                            WorkType.Shutdown => work.AsyncConsumer.HandleModelShutdown(_model, work.Reason),
                            _ => Task.CompletedTask
                        };
                        await task.ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        _model.OnCallbackException(CallbackExceptionEventArgs.Build(e, work.WorkType.ToString(), work.Consumer));
                    }
                    finally
                    {
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
