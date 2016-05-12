using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;

namespace RabbitMQ.Client
{
    /// <summary>
    /// TCP-Client with support for http tunnel through http proxy server
    /// </summary>
    public class TunneledTcpClient : TcpClientAdapter, IDisposable
    {
        #region constants

        private const int WAIT_FOR_DATA_INTERVAL = 50; // 50 ms
        private const int WAIT_FOR_DATA_TIMEOUT = 15000; // 15 seconds

        #endregion constants

        #region fields

        //private static ILog log = LogManager.GetLogger(typeof(TunneledTcpClient));

        private readonly ProxyInfos _proxyInformation;
        private readonly AddressFamily _addressFamily;

        #endregion fields

        #region ctor

        /// <summary>
        /// Constructor.  
        /// </summary>
        /// <param name="proxyHost">Host name or IP address of the proxy server.</param>
        /// <param name="proxyPort">Port number to connect to the proxy server.</param>
        /// <param name="proxyUsername">Username for the proxy server.</param>
        /// <param name="proxyPassword">Password for the proxy server.</param>
        public TunneledTcpClient(AddressFamily addressFamily, string proxyHost, int proxyPort, string proxyUsername = null, string proxyPassword = null) :
            base(null)
        {
            if (!String.IsNullOrEmpty(proxyHost) && (proxyPort <= 0 || proxyPort > 65535))
                throw new ArgumentOutOfRangeException("proxyPort", "port must be greater than zero and less than 65535");

            _proxyInformation = new ProxyInfos(proxyHost, proxyPort, proxyUsername, proxyPassword);
            _addressFamily = addressFamily;
        }


        #endregion ctor

        #region dispose

        public void Dispose()
        {
            if (_tcpClient != null)
            {
                var i = _tcpClient as IDisposable;
                if (i != null)
                {
                    i.Dispose();
                    _tcpClient = null;
                }
            }
        }

        #endregion dispose

        #region methods - CreateConnection

        private delegate TcpClient CreateConnectionCaller(string destinationHost, int destinationPort, AddressFamily addressFamily, ProxyInfos proxyInfos);

        private CreateConnectionCaller caller = new CreateConnectionCaller(createConnection);

        /// <summary>
        /// Open TCP connection through HTTP-Tunnel.
        /// </summary>
        /// <remarks>Method invokes method 'createConnection' asynchronously.</remarks>
        /// <param name="host">Destination host name or IP address.</param>
        /// <param name="port">Port number to connect to on the destination host.</param>
        /// <param name="requestCallback">Callback</param>
        /// <param name="state"></param>
        /// <returns></returns>
        public override IAsyncResult BeginConnect(string host, int port, AsyncCallback requestCallback, object state)
        {
            return caller.BeginInvoke(host, port, _addressFamily, _proxyInformation, requestCallback, state);
        }

        public override void EndConnect(IAsyncResult asyncResult)
        {
            _tcpClient = caller.EndInvoke(asyncResult);
        }

        /// <summary>
        /// Creates a remote TCP connection through a proxy server to the destination host on the destination port.
        /// </summary>
        /// <param name="destinationHost">Destination host name or IP address.</param>
        /// <param name="destinationPort">Port number to connect to on the destination host.</param>
        /// <param name="addressFamily">AdressFamliy for tcp client. Currently ignored.</param>
        /// <param name="proxyInfos">Dto for all container infos</param>
        /// <returns>
        /// Returns an open TcpClient object that can be used normally to communicate
        /// with the destination server
        /// </returns>
        /// <remarks>
        /// This method creates a connection to the proxy server and instructs the proxy server
        /// to make a pass through connection to the specified destination host on the specified
        /// port.  
        /// </remarks>
        private static TcpClient createConnection(string destinationHost, int destinationPort, AddressFamily addressFamily, ProxyInfos proxyInfos)
        {
            var tcpClient = new TcpClient()
            {
                NoDelay = true
            };

            try
            {
                //if (String.IsNullOrEmpty(proxyInfos.Host))
                //    throw new ProxyException("ProxyHost property must contain a value.");

                if (proxyInfos != null && proxyInfos.Uri!=null && !String.IsNullOrEmpty(proxyInfos.Uri.DnsSafeHost))
                {
                    if (proxyInfos.Uri.Port <= 0 || proxyInfos.Uri.Port > 65535)
                        throw new ProxyException("ProxyPort value must be greater than zero and less than 65535");

                    // attempt to open the connection
                    tcpClient.Connect(proxyInfos.Uri.DnsSafeHost, proxyInfos.Uri.Port);

                    //log.DebugFormat("Connection to proxy established: {0}", tcpClient.Client.RemoteEndPoint);

                    //  send connection command to proxy host for the specified destination host and port
                    sendConnectionCommand(tcpClient, proxyInfos, destinationHost, destinationPort);


                }
                else // no proxy in use
                {
                    //log.DebugFormat("No proxy available. Connect directly to destination host.");

                    // attempt to open the connection
                    tcpClient.Connect(destinationHost, destinationPort);

                    //log.DebugFormat("Connection to destination established: {0}", tcpClient.Client.RemoteEndPoint);

                }

                return tcpClient;
            }
            catch (SocketException ex)
            {
                throw new ProxyException(String.Format(CultureInfo.InvariantCulture, "Connection to proxy host {0} on port {1} failed.", Utils.GetHost(tcpClient) ?? destinationHost, Utils.GetPort(tcpClient) ?? destinationPort.ToString(), ex));
            }
        }


        private static void sendConnectionCommand(TcpClient tcpClient, ProxyInfos infos, string host, int port)
        {
            //log.DebugFormat("Start sending connection command");

            NetworkStream stream = tcpClient.GetStream();

            string connectCmd = createCommandString(host, port, infos);

            //log.DebugFormat("Connect command: {0}", connectCmd);

            byte[] request = ASCIIEncoding.ASCII.GetBytes(connectCmd);

            // send the connect request
            stream.Write(request, 0, request.Length);

            // wait for the proxy server to respond
            waitForData(tcpClient, stream);


            //log.DebugFormat("Reponse for connection command available!");
            // PROXY SERVER RESPONSE
            // =======================================================================
            //HTTP/1.0 200 Connection Established<CR><LF>
            //[.... other HTTP header lines ending with <CR><LF>..
            //ignore all of them]
            //<CR><LF>    // Last Empty Line

            // create an byte response array  
            byte[] response = new byte[tcpClient.ReceiveBufferSize];
            StringBuilder sbuilder = new StringBuilder();
            int bytes = 0;
            long total = 0;

            do
            {
                bytes = stream.Read(response, 0, tcpClient.ReceiveBufferSize);
                total += bytes;
                sbuilder.Append(System.Text.ASCIIEncoding.UTF8.GetString(response, 0, bytes));
            } while (stream.DataAvailable);

            var resp = parseResponse(sbuilder.ToString());

            //log.DebugFormat("Response: {0} - {1}", resp.Code, resp.Message);

            //  evaluate the reply code for an error condition
            if (resp.Code != HttpResponseCodes.OK)
                handleProxyCommandError(host, port, tcpClient, resp);

            //log.DebugFormat("Connection to destination established: {0}", tcpClient.Client.RemoteEndPoint);

        }

        private static string createCommandString(string host, int port, ProxyInfos proxyInfos)
        {
            string connectCmd;
            if (!string.IsNullOrEmpty(proxyInfos.UserName))
            {
                //  gets the user/pass into base64 encoded string in the form of [username]:[password]
                string auth = Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", proxyInfos.UserName, proxyInfos.Password)));

                // PROXY SERVER REQUEST
                // =======================================================================
                //CONNECT starksoft.com:443 HTTP/1.0<CR><LF>
                //HOST starksoft.com:443<CR><LF>
                //Proxy-Authorization: username:password<CR><LF>
                //              NOTE: username:password string will be base64 encoded as one 
                //                        concatenated string
                //[... other HTTP header lines ending with <CR><LF> if required]>
                //<CR><LF>    // Last Empty Line
                connectCmd = String.Format(CultureInfo.InvariantCulture, "CONNECT {0}:{1} HTTP/1.1\r\nUser-Agent: rabbitmq-net-client/3.5.3\r\nHost: {0}:{1}\r\nProxy-Authorization: Basic {2}\r\n\r\n", host, port.ToString(CultureInfo.InvariantCulture), auth);
            }
            else
            {
                // PROXY SERVER REQUEST
                // =======================================================================
                //CONNECT starksoft.com:443 HTTP/1.0 <CR><LF>
                //HOST starksoft.com:443<CR><LF>
                //[... other HTTP header lines ending with <CR><LF> if required]>
                //<CR><LF>    // Last Empty Line
                connectCmd = String.Format(CultureInfo.InvariantCulture, "CONNECT {0}:{1} HTTP/1.1\r\nUser-Agent: rabbitmq-net-client/3.5.3\r\nHost: {0}:{1}\r\n\r\n", host, port.ToString(CultureInfo.InvariantCulture));
            }
            return connectCmd;
        }

        private static void handleProxyCommandError(string host, int port, TcpClient tcpClient, HttpResponse resp)
        {
            string msg;

            switch (resp.Code)
            {
                case HttpResponseCodes.None:
                    msg = String.Format(CultureInfo.InvariantCulture, "Proxy destination {0} on port {1} failed to return a recognized HTTP response code.  Server response: {2}", Utils.GetHost(tcpClient), Utils.GetPort(tcpClient), resp.Message);
                    break;

                case HttpResponseCodes.BadGateway:
                    //HTTP/1.1 502 Proxy Error (The specified Secure Sockets Layer (SSL) port is not allowed. ISA Server is not configured to allow SSL requests from this port. Most Web browsers use port 443 for SSL requests.)
                    msg = String.Format(CultureInfo.InvariantCulture, "Proxy destination {0} on port {1} responded with a 502 code - Bad Gateway.  If you are connecting to a Microsoft ISA destination please refer to knowledge based article Q283284 for more information.  Server response: {2}", Utils.GetHost(tcpClient), Utils.GetPort(tcpClient), resp.Message);
                    break;

                default:
                    msg = String.Format(CultureInfo.InvariantCulture, "Proxy destination {0} on port {1} responded with a {2} code - {3}", Utils.GetHost(tcpClient), Utils.GetPort(tcpClient), ((int)resp.Code).ToString(CultureInfo.InvariantCulture), resp.Message);
                    break;
            }

            //  throw a new application exception 
            throw new ProxyException(msg);
        }

        private static void waitForData(TcpClient tcpClient, NetworkStream stream)
        {
            int sleepTime = 0;
            while (!stream.DataAvailable)
            {
                Thread.Sleep(WAIT_FOR_DATA_INTERVAL);
                sleepTime += WAIT_FOR_DATA_INTERVAL;
                if (sleepTime > WAIT_FOR_DATA_TIMEOUT)
                    throw new ProxyException(String.Format("A timeout while waiting for the proxy server at {0} on port {1} to respond.", Utils.GetHost(tcpClient), Utils.GetPort(tcpClient)));
            }
        }

        private static HttpResponse parseResponse(string response)
        {
            //log.DebugFormat("Proxy response: {0}", response);

            string[] data = null;

            //  get rid of the LF character if it exists and then split the string on all CR
            data = response.Replace('\n', ' ').Split('\r');

            return parseCodeAndText(data[0]);
        }

        private static HttpResponse parseCodeAndText(string line)
        {
            int begin = 0;
            int end = 0;
            string val = null;

            if (line.IndexOf("HTTP") == -1)
                throw new ProxyException(String.Format("No HTTP response received from proxy destination.  Server response: {0}.", line));

            begin = line.IndexOf(" ") + 1;
            end = line.IndexOf(" ", begin);

            val = line.Substring(begin, end - begin);
            Int32 code = 0;

            if (!Int32.TryParse(val, out code))
                throw new ProxyException(String.Format("An invalid response code was received from proxy destination.  Server response: {0}.", line));

            return new HttpResponse
            {
                Code = (HttpResponseCodes)code,
                Message = line.Substring(end + 1).Trim()
            };

        }

        #endregion Methods - CreateConnection

        #region subclasses

        public enum HttpResponseCodes
        {
            None = 0,
            Continue = 100,
            SwitchingProtocols = 101,
            OK = 200,
            Created = 201,
            Accepted = 202,
            NonAuthoritiveInformation = 203,
            NoContent = 204,
            ResetContent = 205,
            PartialContent = 206,
            MultipleChoices = 300,
            MovedPermanetly = 301,
            Found = 302,
            SeeOther = 303,
            NotModified = 304,
            UserProxy = 305,
            TemporaryRedirect = 307,
            BadRequest = 400,
            Unauthorized = 401,
            PaymentRequired = 402,
            Forbidden = 403,
            NotFound = 404,
            MethodNotAllowed = 405,
            NotAcceptable = 406,
            ProxyAuthenticantionRequired = 407,
            RequestTimeout = 408,
            Conflict = 409,
            Gone = 410,
            PreconditionFailed = 411,
            RequestEntityTooLarge = 413,
            RequestURITooLong = 414,
            UnsupportedMediaType = 415,
            RequestedRangeNotSatisfied = 416,
            ExpectationFailed = 417,
            InternalServerError = 500,
            NotImplemented = 501,
            BadGateway = 502,
            ServiceUnavailable = 503,
            GatewayTimeout = 504,
            HTTPVersionNotSupported = 505
        }

        /// <summary>
        /// Container for proxy related information
        /// </summary>
        public class ProxyInfos
        {
            public Uri Uri { get; set; }
            public string UserName { get; set; }
            public string Password { get; set; }

            public ProxyInfos(string host, int port, string userName, string password)
            {
                if (!string.IsNullOrEmpty(host))
                {
                    this.Uri = new Uri(string.Format("http://{0}:{1}", host, port), UriKind.Absolute);
                    this.UserName = userName;
                    this.Password = password;
                }
            }
        }

        /// <summary>
        /// Simple container for response message
        /// </summary>
        public class HttpResponse
        {
            public HttpResponseCodes Code { get; set; }

            public string Message { get; set; }
        }

        /// <summary>
        /// Event arguments class for the EncryptAsyncCompleted event.
        /// </summary>
        public class CreateConnectionAsyncCompletedEventArgs : AsyncCompletedEventArgs
        {
            private TcpClient _tcpClient;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="error">Exception information generated by the event.</param>
            /// <param name="cancelled">Cancelled event flag.  This flag is set to true if the event was cancelled.</param>
            /// <param name="proxyConnection">Proxy Connection.  The initialized and open TcpClient proxy connection.</param>
            public CreateConnectionAsyncCompletedEventArgs(Exception error, bool cancelled, TcpClient proxyConnection)
                : base(error, cancelled, null)
            {
                _tcpClient = proxyConnection;
            }

            /// <summary>
            /// The proxy connection.
            /// </summary>
            public TcpClient TcpClient
            {
                get { return _tcpClient; }
            }
        }

        /// <summary>
        /// This exception is thrown when a general, unexpected proxy error.   
        /// </summary>
        [Serializable()]
        public class ProxyException : Exception
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            public ProxyException()
            {
            }

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="message">Exception message text.</param>
            public ProxyException(string message)
                : base(message)
            {
            }

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="message">Exception message text.</param>
            /// <param name="innerException">The inner exception object.</param>
            public ProxyException(string message, Exception innerException)
                :
               base(message, innerException)
            {
            }

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="info">Serialization information.</param>
            /// <param name="context">Stream context information.</param>
            protected ProxyException(SerializationInfo info,
               StreamingContext context)
                : base(info, context)
            {
            }
        }

        /// <summary>
        /// Collection of helper methods
        /// </summary>
        internal static class Utils
        {
            internal static string GetHost(TcpClient client)
            {
                if (client == null)
                    throw new ArgumentNullException("client");

                string host = null;
                try
                {
                    host = ((System.Net.IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                }
                catch
                { };

                return host;
            }

            internal static string GetPort(TcpClient client)
            {
                if (client == null)
                    throw new ArgumentNullException("client");

                string port = null;
                try
                {
                    port = ((System.Net.IPEndPoint)client.Client.RemoteEndPoint).Port.ToString(CultureInfo.InvariantCulture);
                }
                catch
                { };

                return port;
            }

        }

        #endregion subclasses
    }

}
