using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using RabbitMQ.Client.Events;
// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238
using RabbitMQ.Client.MessagePatterns;

namespace RabbitMQ.Client.Examples.WinRT.Subscriber
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MainPageViewModel viewModel;

        private int messageNumber = 0;
        private int sentMessageNumber = 0;

        private ConnectionFactory connectionFactory;
        private IConnection connection;
        private IModel model;
        private Subscription sub;

        public MainPage()
        {
            this.InitializeComponent();

            this.viewModel = new MainPageViewModel();
            this.DataContext = this.viewModel;
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }

        private async void ConnectAction_Click(object sender, RoutedEventArgs e)
        {
            Exception exceptionOccurred = null;
            try
            {
                this.Disconnect();

                var exchange = this.viewModel.Exchange ?? string.Empty;
                var exchangeType = this.viewModel.ExchangeType;
                var routingKey = this.viewModel.RoutingKey;

                this.connectionFactory = new ConnectionFactory
                {
                    Uri = this.viewModel.Uri,
                };

                await this.ConnectAsync(routingKey, exchange, exchangeType);

                this.viewModel.ConnectionStatus = "Connected";
            }
            catch (Exception ex)
            {
                exceptionOccurred = ex;
            }

            if (exceptionOccurred != null)
            {
                await this.ErrorOccured(exceptionOccurred);
                this.viewModel.ConnectionStatus = "Error Connecting";
            }
        }

        private Task ConnectAsync(string routingKey, string exchange, string exchangeType) {
            var currentSynchronizationContext = TaskScheduler.FromCurrentSynchronizationContext();
            return Task.Factory.StartNew(
                () =>
                {
                    this.connection = this.connectionFactory.CreateConnection();
                    this.connection.ConnectionShutdown += ConnectionOnConnectionShutdown;

                    this.model = this.connection.CreateModel();
                    this.model.QueueDeclare(routingKey, false, false, false, null);

                    this.sub = new Subscription(this.model, routingKey);
                    var ignoreTask = this.sub.NextAsync()
                        .ContinueWith(this.SubscriptionContinuation, currentSynchronizationContext);

                    if (exchange != "") {
                        this.model.ExchangeDeclare(exchange, exchangeType);
                        this.model.QueueBind(routingKey, exchange, routingKey, null);
                    }
                });
        }

        private void ConnectionOnConnectionShutdown(object sender, ShutdownEventArgs reason) {
            connection.ConnectionShutdown -= this.ConnectionOnConnectionShutdown;

            Debug.WriteLine("Connection was shutdown: " + reason.ToString());

            var ignoreTask = this.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () => this.viewModel.ConnectionStatus = "Shutdown - " + reason.ReplyText);

            // NOTE: Normally, one would tie into the App.Resuming event and re-connect to the AMQP broker
        }

        private async void SubscriptionContinuation(Task<BasicDeliverEventArgs> continuation)
        {
            Exception exceptionOccurred = null;
            try {
                // receive and add to the viewmodel
                BasicDeliverEventArgs eventArgs = continuation.Result;

                if (eventArgs == null) {
                    Debug.WriteLine("Received empty BasicDeliverEventArgs.");
                    return;
                }

                this.AddReceivedMessage(eventArgs);
                await Task.Factory.StartNew(() => this.sub.Ack(eventArgs));

                // Receive the next message
                var ignoreTask = this.sub.NextAsync()
                    .ContinueWith(this.SubscriptionContinuation, TaskScheduler.FromCurrentSynchronizationContext());
            }
            catch (AggregateException ex) {
                if (ex.InnerException is EndOfStreamException) {
                    Debug.WriteLine("Shutdown");
                }
                else {
                    exceptionOccurred = ex;
                }
            }
            catch (Exception ex) {
                exceptionOccurred = ex;
            }

            if (exceptionOccurred != null)
            {
                await this.ErrorOccured(exceptionOccurred);
            }
        }

        private void AddReceivedMessage(BasicDeliverEventArgs eventArgs) {
            this.viewModel.MessagesReceived.Insert(
                0,
                new MainPageViewModel.ReceivedMessage(this.messageNumber++, eventArgs.Body));
        }

        private void AddMessage(string message)
        {
            this.viewModel.MessagesReceived.Insert(
                0,
                new MainPageViewModel.ReceivedMessage(this.messageNumber++, message));
        }

        private async void DisconnectAction_Click(object sender, RoutedEventArgs e)
        {
            Exception exceptionOccurred = null;
            try
            {
                this.Disconnect();
            }
            catch (Exception ex)
            {
                exceptionOccurred = ex;
            }

            if (exceptionOccurred != null)
            {
                this.viewModel.ConnectionStatus = "Error Disconnecting";
                await this.ErrorOccured(exceptionOccurred);
            }
        }

        private void Disconnect() {
            if (this.sub != null) {
                this.sub.Close();
            }
            this.sub = null;

            if (this.model != null && this.model.IsOpen) {
                this.model.Abort();
            }
            this.model = null;

            if (this.connection != null && this.connection.IsOpen) {
                this.connection.Abort();
            }
            this.connection = null;
            this.viewModel.ConnectionStatus = "Disconnected";
        }

        private async void SendMessagesAction_Click(object sender, RoutedEventArgs e)
        {
            Exception exceptionOccurred = null;
            var exchange = this.viewModel.Exchange ?? string.Empty;
            if (exchange != string.Empty)
            {
                this.model.ExchangeDeclare(exchange, this.viewModel.ExchangeType);
            }

            try
            {
                for (int i = 0; i < this.viewModel.SendCount; i++) {
                    var format = string.Format("Welcome to Caerbannog! (sent: {0})", this.sentMessageNumber++);
                    await this.SendMessageAsync(Encoding.UTF8.GetBytes(format), exchange, "test");
                }
            }
            catch (Exception ex)
            {
                exceptionOccurred = ex;
            }

            if (exceptionOccurred != null) {
                await this.ErrorOccured(exceptionOccurred);
            }
        }

        private Task SendMessageAsync(byte[] message, string exchange, string routingKey) {
            var model = this.model;
            if (model == null) throw new NullReferenceException("IModel is not set, probably not connected.");

            return Task.Factory.StartNew(
                () => model.BasicPublish(exchange, routingKey, null, message));
        }

        private async Task ErrorOccured(Exception exceptionOccurred) {
            Debug.WriteLine(exceptionOccurred);
            var dialog = new MessageDialog(exceptionOccurred.ToString());
            await dialog.ShowAsync();
        }
    }
}
