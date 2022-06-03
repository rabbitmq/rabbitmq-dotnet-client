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
        public ConsumerDispatcher(ModelBase model, int concurrency)
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
                            WorkType.Deliver => work.Strategy.DispatchBasicDeliver(work.ConsumerTag, work.DeliveryTag, work.Redelivered, work.Exchange, work.RoutingKey, work.BasicProperties, work.Body),
                            WorkType.Cancel => work.Strategy.DispatchBasicCancel(work.ConsumerTag),
                            WorkType.CancelOk => work.Strategy.DispatchBasicCancelOk(work.ConsumerTag),
                            WorkType.ConsumeOk => work.Strategy.DispatchBasicConsumeOk(work.ConsumerTag),
                            WorkType.Shutdown => work.Strategy.DispatchModelShutdown(_model, work.Reason),
                            _ => Task.CompletedTask
                        };
                        await task.ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        _model.OnCallbackException(CallbackExceptionEventArgs.Build(e, work.WorkType.ToString(), work.Strategy.Consumer));
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
