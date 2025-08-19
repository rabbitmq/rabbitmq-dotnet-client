using System;
using System.Threading;
using NUnit.Framework;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestBasicCancelAnyConsumer : IntegrationFixture
    {
        [Test]
        public void TestBasicCancelWithQueueDelete()
        {
            // Arrange
            Exception modelException = null;
            Model.CallbackException += HandleCallbackException;
            Model.QueueDeclare(TestContext.CurrentContext.Test.Name, false, false, false);
            string consumerTag = Model.BasicConsume(TestContext.CurrentContext.Test.Name, false, new VoidConsumer());

            // Act
            WithTemporaryModel(m => m.QueueDelete(TestContext.CurrentContext.Test.Name));
            TestDelegate act = () => Model.BasicCancel(consumerTag);

            // Assert
            Assert.DoesNotThrow(act);
            SpinWait.SpinUntil(() => !(modelException is null), TimingFixture.TestTimeout);
            Assert.IsNull(modelException, "The model has thrown an exception when cancelling a consumer after the queue has been deleted. This exception comes from the scheduling of CancelOk Action.");

            // Cleanup
            Model.CallbackException -= HandleCallbackException;

            void HandleCallbackException(object sender, CallbackExceptionEventArgs e) => modelException = e.Exception;
        }

        private sealed class VoidConsumer : DefaultBasicConsumer{}
    }
}
