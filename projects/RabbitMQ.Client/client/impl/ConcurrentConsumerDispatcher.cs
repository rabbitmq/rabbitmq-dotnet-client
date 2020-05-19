using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Impl
{
    internal sealed class ConcurrentConsumerDispatcher : IConsumerDispatcher
    {
        private readonly ModelBase _model;
        private readonly ConsumerWorkService _workService;

        public ConcurrentConsumerDispatcher(ModelBase model, ConsumerWorkService ws)
        {
            _model = model;
            _workService = ws;
            IsShutdown = false;
        }

        public void Quiesce()
        {
            IsShutdown = true;
        }

        public Task Shutdown(IModel model)
        {
            return _workService.StopWorkAsync(model);
        }

        public bool IsShutdown
        {
            get;
            private set;
        }

        public void HandleBasicConsumeOk(IBasicConsumer consumer,
                                         string consumerTag)
        {
            UnlessShuttingDown(() =>
            {
                try
                {
                    consumer.HandleBasicConsumeOk(consumerTag);
                }
                catch (Exception e)
                {
                    var details = new Dictionary<string, object>()
                    {
                        {"consumer", consumer},
                        {"context",  "HandleBasicConsumeOk"}
                    };
                    _model.OnCallbackException(CallbackExceptionEventArgs.Build(e, details));
                }
            });
        }

        public void HandleBasicDeliver(IBasicConsumer consumer,
                                       string consumerTag,
                                       ulong deliveryTag,
                                       bool redelivered,
                                       string exchange,
                                       string routingKey,
                                       IBasicProperties basicProperties,
                                       ReadOnlySpan<byte> body)
        {
            byte[] memoryCopyArray = ArrayPool<byte>.Shared.Rent(body.Length);
            Memory<byte> memoryCopy = new Memory<byte>(memoryCopyArray, 0, body.Length);
            body.CopyTo(memoryCopy.Span);
            UnlessShuttingDown(() =>
            {
                try
                {
                    consumer.HandleBasicDeliver(consumerTag,
                                                deliveryTag,
                                                redelivered,
                                                exchange,
                                                routingKey,
                                                basicProperties,
                                                memoryCopy);
                }
                catch (Exception e)
                {
                    var details = new Dictionary<string, object>()
                    {
                        {"consumer", consumer},
                        {"context",  "HandleBasicDeliver"}
                    };
                    _model.OnCallbackException(CallbackExceptionEventArgs.Build(e, details));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(memoryCopyArray);
                }
            });
        }

        public void HandleBasicCancelOk(IBasicConsumer consumer, string consumerTag)
        {
            UnlessShuttingDown(() =>
            {
                try
                {
                    consumer.HandleBasicCancelOk(consumerTag);
                }
                catch (Exception e)
                {
                    var details = new Dictionary<string, object>()
                    {
                        {"consumer", consumer},
                        {"context",  "HandleBasicCancelOk"}
                    };
                    _model.OnCallbackException(CallbackExceptionEventArgs.Build(e, details));
                }
            });
        }

        public void HandleBasicCancel(IBasicConsumer consumer, string consumerTag)
        {
            UnlessShuttingDown(() =>
            {
                try
                {
                    consumer.HandleBasicCancel(consumerTag);
                }
                catch (Exception e)
                {
                    var details = new Dictionary<string, object>()
                    {
                        {"consumer", consumer},
                        {"context",  "HandleBasicCancel"}
                    };
                    _model.OnCallbackException(CallbackExceptionEventArgs.Build(e, details));
                }
            });
        }

        public void HandleModelShutdown(IBasicConsumer consumer, ShutdownEventArgs reason)
        {
            // the only case where we ignore the shutdown flag.
            try
            {
                consumer.HandleModelShutdown(_model, reason);
            }
            catch (Exception e)
            {
                var details = new Dictionary<string, object>()
                    {
                        {"consumer", consumer},
                        {"context",  "HandleModelShutdown"}
                    };
                _model.OnCallbackException(CallbackExceptionEventArgs.Build(e, details));
            };
        }

        private void UnlessShuttingDown(Action fn)
        {
            if (!IsShutdown)
            {
                Execute(fn);
            }
        }

        private void Execute(Action fn)
        {
            _workService.AddWork(_model, fn);
        }
    }
}
