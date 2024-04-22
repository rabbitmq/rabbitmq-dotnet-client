﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using EasyNetQ.Management.Client;
using RabbitMQ.Client;

namespace Test
{
    public static class Util
    {
        private static readonly TimeSpan s_closeConnectionDelay = TimeSpan.FromSeconds(2);
        private static readonly ManagementClient s_managementClient;
        private static readonly bool s_isWindows = false;

        static Util()
        {
            var managementUri = new Uri("http://localhost:15672");
            s_managementClient = new ManagementClient(managementUri, "guest", "guest");
            s_isWindows = InitIsWindows();
        }

        public static string Now => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        public static bool IsWindows => s_isWindows;

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

        public static async Task CloseConnectionAsync(IConnection conn)
        {
            ushort tries = 10;
            EasyNetQ.Management.Client.Model.Connection connectionToClose = null;
            do
            {
                IReadOnlyList<EasyNetQ.Management.Client.Model.Connection> connections;
                try
                {
                    do
                    {
                        await Task.Delay(s_closeConnectionDelay);
                        connections = await s_managementClient.GetConnectionsAsync();
                    } while (connections.Count == 0);

                    connectionToClose = connections.Where(c0 =>
                        string.Equals((string)c0.ClientProperties["connection_name"], conn.ClientProvidedName,
                        StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();

                    if (connectionToClose == null)
                    {
                        tries--;
                    }
                    else
                    {
                        break;
                    }
                }
                catch (ArgumentNullException)
                {
                    // Sometimes we see this in GitHub CI
                    tries--;
                }
            } while (tries > 0);

            if (tries == 0)
            {
                throw new InvalidOperationException($"Could not delete connection: '{conn.ClientProvidedName}'");
            }

            if (connectionToClose == null)
            {
                throw new InvalidOperationException($"connectionToClose should not be null here");
            }

            await s_managementClient.CloseConnectionAsync(connectionToClose);
        }
    }
}
