// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       https://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v2.0:
//
//---------------------------------------------------------------------------
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task

using System.Globalization;
using EasyNetQ.Management.Client;

namespace GH_1749
{
    public class Util : IDisposable
    {
        private static readonly Random s_random = Random.Shared;
        private readonly ManagementClient _managementClient;

        public Util() : this("localhost", "guest", "guest")
        {
        }

        public Util(string hostname, string managementUsername, string managementPassword)
        {
            if (string.IsNullOrEmpty(managementUsername))
            {
                managementUsername = "guest";
            }

            if (string.IsNullOrEmpty(managementPassword))
            {
                throw new ArgumentNullException(nameof(managementPassword));
            }

            var managementUri = new Uri($"http://{hostname}:15672");
            _managementClient = new ManagementClient(managementUri, managementUsername, managementPassword);
        }

        public static string Now => DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture);

        public static Random S_Random
        {
            get
            {
                return s_random;
            }
        }

        public async Task CloseConnectionAsync(string connectionClientProvidedName)
        {
            ushort tries = 1;
            EasyNetQ.Management.Client.Model.Connection? connectionToClose = null;
            do
            {
                IReadOnlyList<EasyNetQ.Management.Client.Model.Connection> connections;
                try
                {
                    do
                    {
                        ushort delayMilliseconds = (ushort)(tries * 2 * 100);
                        if (delayMilliseconds > 1000)
                        {
                            delayMilliseconds = 1000;
                        }

                        await Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds));

                        connections = await _managementClient.GetConnectionsAsync();
                    } while (connections.Count == 0);

                    connectionToClose = connections.Where(c0 =>
                    {
                        if (c0.ClientProperties.ContainsKey("connection_name"))
                        {
                            object? maybeConnectionName = c0.ClientProperties["connection_name"];
                            if (maybeConnectionName is string connectionNameStr)
                            {
                                return string.Equals(connectionNameStr, connectionClientProvidedName, StringComparison.InvariantCultureIgnoreCase);
                            }
                        }

                        return false;
                    }).FirstOrDefault();
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
                throw new InvalidOperationException(
                    $"{Now} [ERROR] could not find/delete connection: '{connectionClientProvidedName}'");
            }
        }

        public void Dispose() => _managementClient.Dispose();
    }
}
