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
using System.Text;
using System.Text.RegularExpressions;
using RabbitMQ.Client;
using Xunit.Abstractions;

namespace Test
{
    public class RabbitMQCtl
    {
        private static readonly char[] newLine = new char[] { '\n' };
        private static readonly Func<string, Process> s_invokeRabbitMqCtl = GetRabbitMqCtlInvokeAction();
        private static readonly Regex s_getConnectionProperties =
            new Regex(@"^(?<pid><[^>]*>)\s\[.*""connection_name"",""(?<connection_name>[^""]*)"".*\]$", RegexOptions.Multiline | RegexOptions.Compiled);

        private readonly ITestOutputHelper _output;

        public RabbitMQCtl(ITestOutputHelper output)
        {
            _output = output;
        }

        public void CloseConnection(IConnection conn)
        {
            CloseConnection(GetConnectionPid(conn.ClientProvidedName));
        }

        public void AddUser(string username, string password)
        {
            ExecRabbitMQCtl($"add_user {username} {password}");
        }

        public void ChangePassword(string username, string password)
        {
            ExecRabbitMQCtl($"change_password {username} {password}");
        }

        public void SetPermissions(string username, string conf, string write, string read)
        {
            ExecRabbitMQCtl($"set_permissions {username} \"{conf}\" \"{write}\" \"${read}\" ");
        }

        public void DeleteUser(string username)
        {
            ExecRabbitMQCtl($"delete_user {username}");
        }

        public string ExecRabbitMQCtl(string args)
        {
            try
            {
                using var process = s_invokeRabbitMqCtl(args);
                process.Start();
                process.WaitForExit();
                string stderr = process.StandardError.ReadToEnd();
                string stdout = process.StandardOutput.ReadToEnd();

                if (stderr.Length > 0 || process.ExitCode > 0)
                {
                    ReportExecFailure("rabbitmqctl", args, $"{stderr}\n{stdout}");
                }

                return stdout;
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

        private static Func<string, Process> GetRabbitMqCtlInvokeAction()
        {
            string precomputedArguments;
            string envVariable = Environment.GetEnvironmentVariable("RABBITMQ_RABBITMQCTL_PATH");

            if (!string.IsNullOrWhiteSpace(envVariable))
            {
                const string DockerPrefix = "DOCKER:";
                if (envVariable.StartsWith(DockerPrefix))
                {
                    // Call docker
                    precomputedArguments = $"exec {envVariable.Substring(DockerPrefix.Length)} rabbitmqctl ";
                    return args => CreateProcess("docker", precomputedArguments + args);
                }
                else
                {
                    // call the path from the env var
                    return args => CreateProcess(envVariable, args);
                }
            }

            // Try default
            string umbrellaRabbitmqctlPath;
            string providedRabbitmqctlPath;

            if (IsRunningOnMonoOrDotNetCore())
            {
                umbrellaRabbitmqctlPath = "../../../../../../rabbit/scripts/rabbitmqctl";
                providedRabbitmqctlPath = "rabbitmqctl";
            }
            else
            {
                umbrellaRabbitmqctlPath = @"..\..\..\..\..\..\rabbit\scripts\rabbitmqctl.bat";
                providedRabbitmqctlPath = "rabbitmqctl.bat";
            }

            string path = File.Exists(umbrellaRabbitmqctlPath) ? umbrellaRabbitmqctlPath : providedRabbitmqctlPath;

            if (IsRunningOnMonoOrDotNetCore())
            {
                return args => CreateProcess(path, args);
            }
            else
            {
                precomputedArguments = $"/c \"\"{path}\" ";
                return args => CreateProcess("cmd.exe", precomputedArguments + args);
            }
        }

        private string GetConnectionPid(string connectionName)
        {
            string stdout = ExecRabbitMQCtl("list_connections --silent pid client_properties");

            var match = s_getConnectionProperties.Match(stdout);
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

        private void CloseConnection(string pid)
        {
            ExecRabbitMQCtl($"close_connection \"{pid}\" \"Closed via rabbitmqctl\"");
        }

        private static bool IsRunningOnMonoOrDotNetCore()
        {
#if NETCOREAPP
            return true;
#else
            return Type.GetType("Mono.Runtime") != null;
#endif
        }

        private static void Publish(IConnection conn, Encoding encoding)
        {
            IChannel ch = conn.CreateChannel();
            ch.BasicPublish("amq.fanout", "", encoding.GetBytes("message"));
        }

        private static Process CreateProcess(string cmd, string arguments, string workDirectory = null)
        {
            return new Process
            {
                StartInfo =
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    FileName = cmd,
                    Arguments = arguments,
                    WorkingDirectory = workDirectory
                }
            };
        }
    }
}
