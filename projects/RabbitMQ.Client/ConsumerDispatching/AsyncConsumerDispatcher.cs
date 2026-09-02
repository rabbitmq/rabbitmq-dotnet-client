using System;
using System.Diagnostics;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.ConsumerDispatching
{
    internal sealed class AsyncConsumerDispatcher : ConsumerDispatcherChannelBase
    {
        internal AsyncConsumerDispatcher(Channel channel, ushort concurrency)
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
                                            /*
                                             * Record a throwing consumer callback on the deliver span
                                             * before rethrowing to the reporting catch below. Without
                                             * this the span is disposed on the way out and ends
                                             * status=Unset with no exception event, so a consumer that
                                             * throws on every message still traces as fully
                                             * successful. See issue #1967.
                                             */
                                            try
                                            {
                                                await work.Consumer.HandleBasicDeliverAsync(
                                                    work.ConsumerTag!, work.DeliveryTag, work.Redelivered,
                                                    work.Exchange!, work.RoutingKey!, work.BasicProperties!, work.Body.Memory, work.CancellationToken)
                                                    .ConfigureAwait(false);
                                            }
                                            catch (Exception e)
                                            {
                                                /*
                                                 * Shutdown cancels the dispatcher token
                                                 * (Quiesce -> _shutdownCts.Cancel), so a
                                                 * cancellation-aware consumer throwing
                                                 * OperationCanceledException on shutdown is not a
                                                 * delivery failure and is not recorded on the span,
                                                 * matching the publish and connection paths. The
                                                 * rethrow is unchanged: the outer catch still reports
                                                 * it via OnCallbackExceptionAsync. See issue #1967.
                                                 */
                                                if (!(e is OperationCanceledException && work.CancellationToken.IsCancellationRequested))
                                                {
                                                    activity.SetActivityError(e);
                                                }
                                                throw;
                                            }
                                        }
                                        break;
                                    case WorkType.Cancel:
                                        await work.Consumer.HandleBasicCancelAsync(work.ConsumerTag!, work.CancellationToken)
                                            .ConfigureAwait(false);
                                        break;
                                    case WorkType.CancelOk:
                                        await work.Consumer.HandleBasicCancelOkAsync(work.ConsumerTag!, work.CancellationToken)
                                            .ConfigureAwait(false);
                                        break;
                                    case WorkType.ConsumeOk:
                                        await work.Consumer.HandleBasicConsumeOkAsync(work.ConsumerTag!, work.CancellationToken)
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
            finally
            {
                while (_reader.TryRead(out WorkStruct work))
                {
                    using (work)
                    {
                        ESLog.Warn($"discarding consumer work: {work.WorkType}");
                    }
                }
            }
        }
    }
}
