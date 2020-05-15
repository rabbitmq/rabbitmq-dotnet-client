using System.Diagnostics;

namespace RabbitMQ.Client
{
    public class RabbitMQDiagnosticListener
    {
        internal static DiagnosticListener Source = new DiagnosticListener("RabbitMQ.Client");
    }
}
