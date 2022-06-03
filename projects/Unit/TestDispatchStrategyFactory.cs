using System;
using RabbitMQ.Client.DispatchStrategies;
using RabbitMQ.Client.Events;
using Xunit;

namespace RabbitMQ.Client.Unit;

public class TestDispatchStrategyFactory
{
    [Fact]
    public void Create_AsyncConsumer_AsyncDispatchStrategy()
    {
        var cf = new ConnectionFactory();
        using IConnection c = cf.CreateConnection();
        using IModel m = c.CreateModel();
        var consumer = new AsyncEventingBasicConsumer(m);
        var strategy = DispatchStrategyFactory.Create(consumer);

        Assert.IsType<AsyncDispatchStrategy>(strategy);
        Assert.Equal(consumer, strategy.Consumer);
    }
    
    [Fact]
    public void Create_BasicConsumer_BasicDispatchStrategy()
    {
        var cf = new ConnectionFactory();
        using IConnection c = cf.CreateConnection();
        using IModel m = c.CreateModel();
        var consumer = new EventingBasicConsumer(m);
        var strategy = DispatchStrategyFactory.Create(consumer);

        Assert.IsType<BasicDispatchStrategy>(strategy);
        Assert.Equal(consumer, strategy.Consumer);
    }
    
    [Fact]
    public void Create_NullArgument_Exception() => Assert.Throws<ArgumentNullException>(() => _ = DispatchStrategyFactory.Create(null));
}
