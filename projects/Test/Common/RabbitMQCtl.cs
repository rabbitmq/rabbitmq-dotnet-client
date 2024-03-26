// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2020 VMware, Inc.
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
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Xunit.Abstractions;

namespace Test
{
    public class RabbitMQCtl
    {
        private static readonly char[] newLine = new char[] { '\n' };
        // NOTE: \r?
        // https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-options#multiline-mode
        private static readonly Regex s_getConnectionProperties =
            new Regex(@"^(?<pid><[^>]*>)\s\[.*""connection_name"",""(?<connection_name>[^""]*)"".*\]\r?$", RegexOptions.Multiline | RegexOptions.Compiled);

        private readonly ITestOutputHelper _output;

        public RabbitMQCtl(ITestOutputHelper output)
        {
            _output = output;
        }

        public async Task CloseConnectionAsync(IConnection conn)
        {
            string pid = await GetConnectionPidAsync(conn.ClientProvidedName);
            await CloseConnectionAsync(pid);
        }

        public Task AddUserAsync(string username, string password)
        {
            return ExecRabbitMQCtlAsync($"add_user {username} {password}");
        }

        public Task ChangePasswordAsync(string username, string password)
        {
            return ExecRabbitMQCtlAsync($"change_password {username} {password}");
        }

        public Task SetPermissionsAsync(string username, string conf, string write, string read)
        {
            return ExecRabbitMQCtlAsync($"set_permissions {username} \"{conf}\" \"{write}\" \"${read}\" ");
        }

        public Task DeleteUserAsync(string username)
        {
            return ExecRabbitMQCtlAsync($"delete_user {username}");
        }

        public async Task<string> ExecRabbitMQCtlAsync(string args)
        {
            try
            {
                ProcessStartInfo rabbitmqCtlStartInfo = GetRabbitMqCtlStartInfo(args);
                ProcessUtil.Result result = await ProcessUtil.RunAsync(rabbitmqCtlStartInfo);
                return result.StdOut;
            }
            catch (Exception e)
            {
                ReportExecFailure("rabbitmqctl", args, e.Message);
                throw;
            }
        }

        private void ReportExecFailure(string cmd, string args, string msg)
        {
            _output.WriteLine($"Failure while running {cmd} {args}:\n{msg}");
        }

        private static ProcessStartInfo GetRabbitMqCtlStartInfo(string args)
        {
            string envVariable = Environment.GetEnvironmentVariable("RABBITMQ_RABBITMQCTL_PATH");

            if (false == string.IsNullOrWhiteSpace(envVariable))
            {
                const string DockerPrefix = "DOCKER:";
                if (envVariable.StartsWith(DockerPrefix))
                {
                    // Call docker
                    return CreateProcessStartInfo("docker",
                        $"exec {envVariable.Substring(DockerPrefix.Length)} rabbitmqctl {args}");
                }
                else
                {
                    // call the path from the env var
                    return CreateProcessStartInfo(envVariable, args);
                }
            }

            string umbrellaRabbitmqctlPath;
            string providedRabbitmqctlPath;

            if (Util.IsWindows)
            {
                umbrellaRabbitmqctlPath = @"..\..\..\..\..\..\rabbit\scripts\rabbitmqctl.bat";
                providedRabbitmqctlPath = "rabbitmqctl.bat";
            }
            else
            {
                umbrellaRabbitmqctlPath = "../../../../../../rabbit/scripts/rabbitmqctl";
                providedRabbitmqctlPath = "rabbitmqctl";
            }

            string path = File.Exists(umbrellaRabbitmqctlPath) ? umbrellaRabbitmqctlPath : providedRabbitmqctlPath;

            return CreateProcessStartInfo(path, args);
        }

        private async Task<string> GetConnectionPidAsync(string connectionName)
        {
            string stdout = await ExecRabbitMQCtlAsync("list_connections --silent pid client_properties");
            Match match = s_getConnectionProperties.Match(stdout);
            while (match.Success)
            {
                if (match.Groups["connection_name"].Value == connectionName)
                {
                    return match.Groups["pid"].Value;
                }

                match = match.NextMatch();
            }

            throw new Exception($"No connection found with name: {connectionName}");
        }

        private Task CloseConnectionAsync(string pid)
        {
            return ExecRabbitMQCtlAsync($"close_connection \"{pid}\" \"Closed via rabbitmqctl\"");
        }

        private static ProcessStartInfo CreateProcessStartInfo(string cmd, string arguments, string workDirectory = null)
        {
            return new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                FileName = cmd,
                Arguments = arguments,
                WorkingDirectory = workDirectory
            };
        }
    }
}
