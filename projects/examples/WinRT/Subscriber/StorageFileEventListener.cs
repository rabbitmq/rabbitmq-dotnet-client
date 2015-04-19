using System.Diagnostics.Tracing;

namespace RabbitMQ.Client.Examples.WinRT.Subscriber
{
    public class StorageFileEventListener : EventListener
    {
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            throw new System.NotImplementedException();
        }
    }
}