using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing.Impl;
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
                                Task task = Task.CompletedTask;
                                switch (work.WorkType)
                                {
                                    case WorkType.Deliver:
                                        {
                                            IAsyncBasicConsumer? deliverConsumer = work.AsyncConsumer ??
                                                throw new InvalidOperationException("[CRITICAL] should never see this");
                                            ConsumerTag? deliverConsumerTag = work.ConsumerTag;
                                            var method = new BasicDeliver(work.Method.Memory);
                                            var exchangeName = new ExchangeName(method._exchange);
                                            var routingKey = new RoutingKey(method._routingKey);
                                            task = deliverConsumer.HandleBasicDeliver(
                                                deliverConsumerTag, work.DeliveryTag, work.Redelivered,
                                                exchangeName, routingKey, work.BasicProperties, work.Body.Memory);
                                        }
                                        break;
                                    case WorkType.Cancel:
                                        {
                                            RentedMemory cancelMethodMemory = work.Method;
                                            ConsumerTag cancelConsumerTag = BasicCancel.GetConsumerTag(cancelMethodMemory.Memory);
                                            IAsyncBasicConsumer cancelConsumer = (IAsyncBasicConsumer)GetAndRemoveConsumer(cancelConsumerTag);
                                            task = cancelConsumer.HandleBasicCancel(cancelConsumerTag);
                                        }
                                        break;
                                    case WorkType.CancelOk:
                                        {
                                            RentedMemory cancelOkMethodMemory = work.Method;
                                            ConsumerTag cancelOkConsumerTag = BasicCancelOk.GetConsumerTag(cancelOkMethodMemory.Memory);
                                            IAsyncBasicConsumer cancelOkConsumer = (IAsyncBasicConsumer)GetAndRemoveConsumer(cancelOkConsumerTag);
                                            task = cancelOkConsumer.HandleBasicCancelOk(cancelOkConsumerTag);
                                        }
                                        break;
                                    case WorkType.ConsumeOk:
                                        {
                                            IAsyncBasicConsumer? consumeOkConsumer = work.AsyncConsumer ??
                                                throw new InvalidOperationException("[CRITICAL] should never see this");
                                            task = consumeOkConsumer.HandleBasicConsumeOk(work.ConsumerTag);
                                        }
                                        break;
                                    case WorkType.Shutdown:
                                        {
                                            IAsyncBasicConsumer? shutdownConsumer = work.AsyncConsumer ??
                                                throw new InvalidOperationException("[CRITICAL] should never see this");
                                            task = shutdownConsumer.HandleChannelShutdown(_channel, work.Reason);
                                        }
                                        break;
                                    default:
                                        throw new InvalidOperationException();
                                }

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
