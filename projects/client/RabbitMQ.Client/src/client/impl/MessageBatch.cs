namespace RabbitMQ.Client.Impl
{
    using System.Collections.Generic;
    using RabbitMQ.Client;
    using RabbitMQ.Client.Framing.Impl;
    using RabbitMQ.Client.Impl;

    public class MessageBatch : IMessageBatch
    {
        private List<Command> commands = new List<Command>();
        private ModelBase model;
        internal MessageBatch(ModelBase model)
        {
            this.model = model;
        }

        public void Add(string exchange, string routingKey, bool mandatory, IBasicProperties basicProperties, byte[] body)
        {
            var bp = basicProperties == null ? model.CreateBasicProperties() : basicProperties;
            var method = new BasicPublish
            {
                m_exchange = exchange,
                m_routingKey = routingKey,
                m_mandatory = mandatory
            };

            commands.Add(new Command(method, (ContentHeaderBase)bp, body));
        }

        public void Publish()
        {
            model.SendCommands(commands);
        }
    }
}