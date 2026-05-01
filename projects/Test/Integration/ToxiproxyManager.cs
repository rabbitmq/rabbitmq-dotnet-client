using System;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using Test;
using Toxiproxy.Net;
using Toxiproxy.Net.Toxics;

namespace Integration
{
    public class ToxiproxyManager : IAsyncDisposable
    {
        public const ushort ProxyPort = 55672;
        private const string ProxyNamePrefix = "rmq";

        private readonly string _testDisplayName;
        private readonly Connection _proxyConnection;
        private readonly Client _proxyClient;
        private readonly Proxy _proxy;

        private bool _disposedValue = false;

        /// <summary>
        /// The hostname clients should use to connect to the Toxiproxy server.
        /// Defaults to <c>localhost</c>; overridden by <c>RABBITMQ_TOXIPROXY_HOST</c>.
        /// </summary>
        public string ProxyHost { get; private set; } = "localhost";

        public ToxiproxyManager(string testDisplayName, bool isRunningInCI, bool isWindows)
        {
            if (string.IsNullOrWhiteSpace(testDisplayName))
            {
                throw new ArgumentNullException(nameof(testDisplayName));
            }

            _testDisplayName = testDisplayName;

            // to start, assume everything is on localhost
            _proxy = new Proxy
            {
                Enabled = true,
                Listen = $"{IPAddress.Loopback}:{ProxyPort}",
                Upstream = $"{IPAddress.Loopback}:5672",
            };

            if (isRunningInCI)
            {
                _proxy.Listen = $"0.0.0.0:{ProxyPort}";

                // GitHub Actions
                if (false == isWindows)
                {
                    /*
                     * Note: See the following setup script:
                     * .ci/ubuntu/gha-setup.sh
                     */
                    _proxy.Upstream = "rabbitmq-dotnet-client-rabbitmq:5672";
                }
            }

            // Allow Docker-based local development to override the upstream host.
            // Set RABBITMQ_TOXIPROXY_UPSTREAM_HOST=<container-name-or-ip> when both
            // Toxiproxy and RabbitMQ run in Docker and 127.0.0.1 refers to the
            // Toxiproxy container's own loopback rather than the RabbitMQ container.
            string upstreamHostOverride = Environment.GetEnvironmentVariable("RABBITMQ_TOXIPROXY_UPSTREAM_HOST");
            if (!string.IsNullOrEmpty(upstreamHostOverride))
            {
                _proxy.Upstream = $"{upstreamHostOverride}:5672";
            }

            // Allow specifying the Toxiproxy management API host and the host that
            // clients should use to connect to the proxy.  Useful when running tests
            // from WSL2 using the Docker bridge IP (e.g. 172.26.0.3) to get a direct
            // Linux-to-Linux TCP path that propagates TCP RST without any Windows
            // port-forwarding proxy in between.
            string toxiproxyHost = "127.0.0.1";
            string proxyHostOverride = Environment.GetEnvironmentVariable("RABBITMQ_TOXIPROXY_HOST");
            if (!string.IsNullOrEmpty(proxyHostOverride))
            {
                toxiproxyHost = proxyHostOverride;
                ProxyHost = proxyHostOverride;
                // Ensure the proxy listens on all interfaces so non-loopback clients connect.
                _proxy.Listen = $"0.0.0.0:{ProxyPort}";
            }

            /*
             * Note:
             * Do NOT set resetAllToxicsAndProxiesOnClose to true, because it will
             * clear proxies being used by parallel TFM test runs
             */
            _proxyConnection = new Connection(toxiproxyHost, resetAllToxicsAndProxiesOnClose: false);
            _proxyClient = _proxyConnection.Client();
        }

        public async Task InitializeAsync()
        {
            /*
             * Note: since all Toxiproxy tests are in the same test class, they will run sequentially,
             * so we can use 55672 for the listen port. In addition, TestTfmsInParallel is set to false
             * so we know only one set of integration tests are running at a time
             */
            string proxyName = $"{ProxyNamePrefix}-{_testDisplayName}-{Util.Now}";
            _proxy.Name = proxyName;

            ushort retryCount = 5;
            do
            {
                try
                {
                    await _proxyClient.AddAsync(_proxy);
                    return;
                }
                catch (Exception ex)
                {
                    if (retryCount == 0)
                    {
                        throw;
                    }
                    else
                    {
                        string now = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
                        Console.Error.WriteLine("{0} [ERROR] error initializing proxy '{1}': {2}", now, proxyName, ex);
                    }
                }
                --retryCount;
                await Task.Delay(TimeSpan.FromSeconds(1));
            } while (retryCount >= 0);
        }

        public Task<T> AddToxicAsync<T>(T toxic) where T : ToxicBase
        {
            return _proxy.AddAsync(toxic);
        }

        public Task RemoveToxicAsync(string toxicName)
        {
            return _proxy.RemoveToxicAsync(toxicName);
        }

        public Task EnableAsync()
        {
            _proxy.Enabled = true;
            return _proxyClient.UpdateAsync(_proxy);
        }

        public Task DisableAsync()
        {
            _proxy.Enabled = false;
            return _proxyClient.UpdateAsync(_proxy);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposedValue)
            {
                try
                {
                    await _proxyClient.DeleteAsync(_proxy);
                    _proxyConnection.Dispose();
                }
                catch (Exception ex)
                {
                    string now = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
                    Console.Error.WriteLine("{0} [ERROR] error disposing proxy '{1}': {2}", now, _proxy.Name, ex);
                }
                finally
                {
                    _disposedValue = true;
                }
            }
        }
    }
}
