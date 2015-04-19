using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

using RabbitMQ.Client.Examples.WinRT.Subscriber.Annotations;

namespace RabbitMQ.Client.Examples.WinRT.Subscriber
{
    public class MainPageViewModel : INotifyPropertyChanged
    {
        private bool useServerNamedQueue;
        private string exchange;
        private string uri;
        private int sendCount;
        private bool blockingReceiveSelected;
        private bool enumeratingReceiveSelected;
        private readonly ObservableCollection<ReceivedMessage> messagesReceived = new ObservableCollection<ReceivedMessage>();
        private readonly ObservableCollection<string> exchangeTypes = new ObservableCollection<string>(Client.ExchangeType.All());
        private string exchangeType;
        private string routingKey;
        private string connectionStatus;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool UseServerNamedQueue
        {
            get { return this.useServerNamedQueue; }
            set
            {
                if (value.Equals(this.useServerNamedQueue))
                {
                    return;
                }
                this.useServerNamedQueue = value;
                this.OnPropertyChanged();
            }
        }

        public string Exchange
        {
            get { return this.exchange; }
            set
            {
                if (value == this.exchange)
                {
                    return;
                }
                this.exchange = value;
                this.OnPropertyChanged();
            }
        }

        public string ExchangeType
        {
            get { return this.exchangeType; }
            set
            {
                if (value == this.exchangeType)
                {
                    return;
                }
                this.exchangeType = value;
                this.OnPropertyChanged();
            }
        }

        public string RoutingKey
        {
            get { return this.routingKey; }
            set
            {
                if (value == this.routingKey)
                {
                    return;
                }
                this.routingKey = value;
                this.OnPropertyChanged();
            }
        }

        public string ConnectionStatus
        {
            get { return this.connectionStatus; }
            set
            {
                if (value == this.connectionStatus)
                {
                    return;
                }
                this.connectionStatus = value;
                this.OnPropertyChanged();
            }
        }

        public string Uri
        {
            get { return this.uri; }
            set
            {
                if (value == this.uri)
                {
                    return;
                }
                this.uri = value;
                this.OnPropertyChanged();
            }
        }

        public int SendCount
        {
            get { return this.sendCount; }
            set
            {
                if (value == this.sendCount)
                {
                    return;
                }
                this.sendCount = value;
                this.OnPropertyChanged();
            }
        }

        public bool BlockingReceiveSelected
        {
            get { return this.blockingReceiveSelected; }
            set
            {
                if (value.Equals(this.blockingReceiveSelected))
                {
                    return;
                }
                this.blockingReceiveSelected = value;
                this.OnPropertyChanged();
            }
        }

        public bool EnumeratingReceiveSelected
        {
            get { return this.enumeratingReceiveSelected; }
            set
            {
                if (value.Equals(this.enumeratingReceiveSelected))
                {
                    return;
                }
                this.enumeratingReceiveSelected = value;
                this.OnPropertyChanged();
            }
        }

        public ObservableCollection<ReceivedMessage> MessagesReceived
        {
            get { return this.messagesReceived; }
        }

        public ObservableCollection<string> ExchangeTypes
        {
            get { return this.exchangeTypes; }
        }

        public MainPageViewModel()
        {
            //this.uri = "amqp://user:pass@host:port/vhost";
            this.uri = "amqp://localhost/";
            this.exchange = string.Empty;
            this.routingKey = "test";
            this.sendCount = 10;
            this.blockingReceiveSelected = true;
            this.useServerNamedQueue = false;
            this.connectionStatus = "Disconnected";
            this.exchangeType = Client.ExchangeType.Direct;
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public class ReceivedMessage
        {
            public ReceivedMessage(int i, byte[] body)
                :this(i, Encoding.UTF8.GetString(body, 0, body.Length))
            {
            }

            public ReceivedMessage(int i, string message)
            {
                this.DecodedString = string.Format("Message {0}: {1}", i, message);
            }

            public string DecodedString { get; set; }
        }
    }

    public class MainPageViewModelDesign : MainPageViewModel
    {
        public MainPageViewModelDesign()
        {
            this.MessagesReceived.Add(new ReceivedMessage(0, "Welcome to Caerbannog!"));
            this.MessagesReceived.Add(new ReceivedMessage(1, "Welcome to Caerbannog!"));
        }
    }
}