using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RabbitMQ.Client.ConsumerDispatching;

#nullable enable
internal abstract class ConsumerDispatcherBase
{
    private static readonly FallbackConsumer fallbackConsumer = new FallbackConsumer();
    private readonly Dictionary<string, IBasicConsumer> _consumers;

    public IBasicConsumer? DefaultConsumer { get; set; }

    protected ConsumerDispatcherBase()
    {
        _consumers = new Dictionary<string, IBasicConsumer>();
    }

    protected void AddConsumer(IBasicConsumer consumer, string tag)
    {
        lock (_consumers)
        {
            _consumers[tag] = consumer;
        }
    }

    protected IBasicConsumer GetConsumerOrDefault(string tag)
    {
        lock (_consumers)
        {
            return _consumers.TryGetValue(tag, out var consumer) ? consumer : GetDefaultOrFallbackConsumer();
        }
    }

    public IBasicConsumer GetAndRemoveConsumer(string tag)
    {
        lock (_consumers)
        {
            return _consumers.Remove(tag, out var consumer) ? consumer : GetDefaultOrFallbackConsumer();
        }
    }

    public Task ShutdownAsync(ShutdownEventArgs reason)
    {
        lock (_consumers)
        {
            foreach (KeyValuePair<string, IBasicConsumer> pair in _consumers)
            {
                ShutdownConsumer(pair.Value, reason);
            }
            _consumers.Clear();
        }

        return InternalShutdownAsync();
    }

    protected abstract void ShutdownConsumer(IBasicConsumer consumer, ShutdownEventArgs reason);

    protected abstract Task InternalShutdownAsync();

    // Do not inline as it's not the default case on a hot path
    [MethodImpl(MethodImplOptions.NoInlining)]
    private IBasicConsumer GetDefaultOrFallbackConsumer()
    {
        return DefaultConsumer ?? fallbackConsumer;
    }
}
