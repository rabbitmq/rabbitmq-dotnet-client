using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing.Impl;
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
            try
            {
                while (await _reader.WaitToReadAsync(token).ConfigureAwait(false))
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
                                        {
                                            IBasicConsumer? deliverConsumer = work.Consumer ??
                                                throw new InvalidOperationException("[CRITICAL] should never see this");
                                            ConsumerTag? deliverConsumerTag = work.ConsumerTag;
                                            var method = new BasicDeliver(work.Method.Memory);
                                            var exchangeName = new ExchangeName(method._exchange);
                                            var routingKey = new RoutingKey(method._routingKey);
                                            await deliverConsumer.HandleBasicDeliverAsync(
                                                deliverConsumerTag, work.DeliveryTag, work.Redelivered,
                                                exchangeName, routingKey, work.BasicProperties, work.Body.Memory)
                                                .ConfigureAwait(false);
                                        }
                                        break;
                                    case WorkType.Cancel:
                                        {
                                            RentedMemory cancelMethodMemory = work.Method;
                                            ConsumerTag cancelConsumerTag = BasicCancel.GetConsumerTag(cancelMethodMemory.Memory);
                                            IBasicConsumer cancelConsumer = GetAndRemoveConsumer(cancelConsumerTag);
                                            cancelConsumer.HandleBasicCancel(cancelConsumerTag);
                                        }
                                        break;
                                    case WorkType.CancelOk:
                                        {
                                            RentedMemory cancelOkMethodMemory = work.Method;
                                            ConsumerTag cancelOkConsumerTag = BasicCancelOk.GetConsumerTag(cancelOkMethodMemory.Memory);
                                            IBasicConsumer cancelOkConsumer = GetAndRemoveConsumer(cancelOkConsumerTag);
                                            cancelOkConsumer.HandleBasicCancelOk(cancelOkConsumerTag);
                                        }
                                        break;
                                    case WorkType.ConsumeOk:
                                        {
                                            IBasicConsumer? consumeOkConsumer = work.Consumer ??
                                                throw new InvalidOperationException("[CRITICAL] should never see this");
                                            consumeOkConsumer.HandleBasicConsumeOk(work.ConsumerTag);
                                        }
                                        break;
                                    case WorkType.Shutdown:
                                        {
                                            IBasicConsumer? shutdownConsumer = work.Consumer ??
                                                throw new InvalidOperationException("[CRITICAL] should never see this");
                                            shutdownConsumer.HandleChannelShutdown(_channel, work.Reason);
                                        }
                                        break;
                                    default:
                                        throw new InvalidOperationException();
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
                if (false == token.IsCancellationRequested)
                {
                    throw;
                }
            }
        }
    }
}
