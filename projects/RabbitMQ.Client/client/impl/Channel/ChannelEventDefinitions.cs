namespace RabbitMQ.Client.client.impl.Channel
{
    #nullable enable
    /// <summary>
    /// The delegate for a failed delivery.
    /// </summary>
    /// <param name="message">The message failed to deliver.</param>
    /// <param name="replyText">The close code (See under "Reply Codes" in the AMQP specification).</param>
    /// <param name="replyCode">A message indicating the reason for closing the model.</param>
    public delegate void MessageDeliveryFailedDelegate(in Message message, string replyText, ushort replyCode);
}
