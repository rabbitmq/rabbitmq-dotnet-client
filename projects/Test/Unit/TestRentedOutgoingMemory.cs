using System.Threading.Tasks;
using RabbitMQ.Client;
using Xunit;

namespace Test.Unit;

public class TestRentedOutgoingMemory
{
    [Fact]
    public async Task TestNonBlocking()
    {
        // Arrange
        byte[] buffer = new byte[] { 1, 2, 3, 4, 5 };
        RentedOutgoingMemory rentedMemory = new RentedOutgoingMemory(buffer, waitSend: false);

        // Act
        var waitTask = rentedMemory.WaitForDataSendAsync().AsTask();
        var timeoutTask = Task.Delay(100);
        var completedTask = await Task.WhenAny(timeoutTask, waitTask);

        // Assert
        Assert.Equal(waitTask, completedTask);
    }

    [Fact]
    public async Task TestBlocking()
    {
        // Arrange
        byte[] buffer = new byte[] { 1, 2, 3, 4, 5 };
        RentedOutgoingMemory rentedMemory = new RentedOutgoingMemory(buffer, waitSend: true);

        // Act
        var waitTask = rentedMemory.WaitForDataSendAsync().AsTask();
        var timeoutTask = Task.Delay(100);
        var completedTask = await Task.WhenAny(timeoutTask, waitTask);

        // Assert
        Assert.Equal(timeoutTask, completedTask);
    }

    [Fact]
    public async Task TestBlockingCompleted()
    {
        // Arrange
        byte[] buffer = new byte[] { 1, 2, 3, 4, 5 };
        RentedOutgoingMemory rentedMemory = new RentedOutgoingMemory(buffer, waitSend: true);

        // Act
        var waitTask = rentedMemory.WaitForDataSendAsync().AsTask();
        var timeoutTask = Task.Delay(100);

        rentedMemory.DidSend();

        var completedTask = await Task.WhenAny(timeoutTask, waitTask);

        // Assert
        Assert.Equal(waitTask, completedTask);
    }
}
