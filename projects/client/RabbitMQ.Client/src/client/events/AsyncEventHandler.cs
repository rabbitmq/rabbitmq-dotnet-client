using System;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Events
{
    public delegate Task AsyncEventHandler<in TEvent>(object sender, TEvent @event) where TEvent : EventArgs;
}