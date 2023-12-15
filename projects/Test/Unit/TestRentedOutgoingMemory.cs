using RabbitMQ.Client;
using Xunit;

namespace Test.Unit;

public class TestRentedOutgoingMemory
{
    [Fact]
    public void TestNonBlocking()
    {
        // Arrange
        byte[] buffer = new byte[] { 1, 2, 3, 4, 5 };
        RentedOutgoingMemory rentedMemory = RentedOutgoingMemory.GetAndInitialize(buffer, waitSend: false);

        // Act
        var waitTask = rentedMemory.WaitForDataSendAsync();

        // Assert
        Assert.True(waitTask.IsCompleted);
    }

    [Fact]
    public void TestBlocking()
    {
        // Arrange
        byte[] buffer = new byte[] { 1, 2, 3, 4, 5 };
        RentedOutgoingMemory rentedMemory = RentedOutgoingMemory.GetAndInitialize(buffer, waitSend: true);

        // Act
        var waitTask = rentedMemory.WaitForDataSendAsync();

        // Assert
        Assert.False(waitTask.IsCompleted);
    }

    [Fact]
    public void TestBlockingCompleted()
    {
        // Arrange
        byte[] buffer = new byte[] { 1, 2, 3, 4, 5 };
        RentedOutgoingMemory rentedMemory = RentedOutgoingMemory.GetAndInitialize(buffer, waitSend: true);

        // Act
        var waitTask = rentedMemory.WaitForDataSendAsync();

        rentedMemory.DidSend();

        // Assert
        Assert.False(waitTask.IsCompleted);
    }
}
