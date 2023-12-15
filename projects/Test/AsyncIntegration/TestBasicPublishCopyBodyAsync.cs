using System.Threading.Tasks;
using RabbitMQ.Client;
using Xunit;
using Xunit.Abstractions;

namespace Test.AsyncIntegration;

public class TestBasicPublishCopyBodyAsync : AsyncIntegrationFixture
{
    public TestBasicPublishCopyBodyAsync(ITestOutputHelper output) : base(output)
    {
    }

    protected override ConnectionFactory CreateConnectionFactory()
    {
        var factory = base.CreateConnectionFactory();
        factory.CopyBodyToMemoryThreshold = 1024;
        return factory;
    }

    [Theory(Skip = "Parallelization is disabled for this collection")]
    [InlineData(512)]
    [InlineData(1024)]
    public async Task TestNonCopyingBody(ushort size)
    {
        QueueDeclareOk q = await _channel.QueueDeclareAsync(string.Empty, false, false, true, false, null);
        byte[] body = GetRandomBody(size);

        uint rentedBytes;

        using (var result = await TrackRentedBytes())
        {
            await _channel.BasicPublishAsync(string.Empty, q, body);
            rentedBytes = result.RentedBytes;
        }

        Assert.Equal((uint)1, await _channel.QueuePurgeAsync(q));

        // It is expected that the rented bytes is smaller than the size of the body
        // since we're not copying the body. Only the frame headers are rented.
        Assert.True(rentedBytes < size);
    }

    [Theory]
    [InlineData(1025)]
    [InlineData(2048)]
    public async Task TestCopyingBody(ushort size)
    {
        QueueDeclareOk q = await _channel.QueueDeclareAsync(string.Empty, false, false, true, false, null);
        byte[] body = GetRandomBody(size);

        uint rentedBytes;

        using (var result = await TrackRentedBytes())
        {
            await _channel.BasicPublishAsync(string.Empty, q, body);
            rentedBytes = result.RentedBytes;
        }

        Assert.Equal((uint)1, await _channel.QueuePurgeAsync(q));

        // It is expected that the rented bytes is larger than the size of the body
        // since the body is copied with the frame headers.
        Assert.True(rentedBytes >= size);
    }
}
