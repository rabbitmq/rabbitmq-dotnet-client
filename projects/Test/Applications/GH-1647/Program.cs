#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
using System.Text;
using RabbitMQ.Client;

ConnectionFactory connectionFactory = new()
{
    AutomaticRecoveryEnabled = true,
    UserName = "guest",
    Password = "guest"
};

var props = new BasicProperties();
byte[] msg = Encoding.UTF8.GetBytes("test");
using var connection = await connectionFactory.CreateConnectionAsync();
for (int i = 0; i < 300; i++)
{
    try
    {
        using var channel = await connection.CreateChannelAsync(); // New channel for each message
        await Task.Delay(1000);
        await channel.BasicPublishAsync(string.Empty, string.Empty, props, msg);
        Console.WriteLine($"Sent message {i}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to send message {i}: {ex.Message}");
        await Task.Delay(1000);
    }
}
