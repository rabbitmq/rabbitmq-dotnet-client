using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using EasyNetQ.Management.Client;
using Xunit.Abstractions;

namespace Test
{
    public class Util : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly ManagementClient _managementClient;
        private static readonly bool s_isWindows = false;

        static Util()
        {
            s_isWindows = InitIsWindows();
        }

        public Util(ITestOutputHelper output) : this(output, "guest", "guest")
        {
        }

        public Util(ITestOutputHelper output, string managementUsername, string managementPassword)
        {
            _output = output;

            if (string.IsNullOrEmpty(managementUsername))
            {
                managementUsername = "guest";
            }

            if (string.IsNullOrEmpty(managementPassword))
            {
                throw new ArgumentNullException(nameof(managementPassword));
            }

            var managementUri = new Uri("http://localhost:15672");
            _managementClient = new ManagementClient(managementUri, managementUsername, managementPassword);
        }

        public static string Now => DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture);

        public static bool IsWindows => s_isWindows;

        public async Task CloseConnectionAsync(string connectionClientProvidedName)
        {
            ushort tries = 1;
            EasyNetQ.Management.Client.Model.Connection connectionToClose = null;
            do
            {
                IReadOnlyList<EasyNetQ.Management.Client.Model.Connection> connections;
                try
                {
                    do
                    {
                        ushort delaySeconds = (ushort)(tries * 2);
                        if (delaySeconds > 10)
                        {
                            delaySeconds = 10;
                        }

                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

                        connections = await _managementClient.GetConnectionsAsync();
                    } while (connections.Count == 0);

                    connectionToClose = connections.Where(c0 =>
                        string.Equals((string)c0.ClientProperties["connection_name"], connectionClientProvidedName,
                        StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                }
                catch (ArgumentNullException)
                {
                    // Sometimes we see this in GitHub CI
                    tries++;
                    continue;
                }

                if (connectionToClose != null)
                {
                    try
                    {
                        await _managementClient.CloseConnectionAsync(connectionToClose);
                        return;
                    }
                    catch (UnexpectedHttpStatusCodeException)
                    {
                        tries++;
                    }
                }
            } while (tries <= 10);

            if (connectionToClose == null)
            {
                _output.WriteLine("{0} [WARNING] could not find/delete connection: '{1}'",
                    Now, connectionClientProvidedName);
            }
        }

        public void Dispose() => _managementClient.Dispose();

        private static bool InitIsWindows()
        {
            PlatformID platform = Environment.OSVersion.Platform;
            if (platform == PlatformID.Win32NT)
            {
                return true;
            }

            string os = Environment.GetEnvironmentVariable("OS");
            if (os != null)
            {
                os = os.Trim();
                return os == "Windows_NT";
            }

            return false;
        }
    }
}
