﻿// This source code is dual-licensed under the Apache License, version
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

#pragma warning disable 2002

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace RabbitMQ.Client.Unit
{
#nullable enable
    public static class RabbitMQCtl
    {
        private static readonly char[] newLine = new char[] { '\n' };

        private static Process GetRabbitMqCtlInvokeAction(string args)
        {
            string precomputedArguments;
            string? envVariable = Environment.GetEnvironmentVariable("RABBITMQ_RABBITMQCTL_PATH");

            if (!string.IsNullOrWhiteSpace(envVariable))
            {
                const string DockerPrefix = "DOCKER:";
                if (envVariable.StartsWith(DockerPrefix))
                {
                    // Call docker
                    precomputedArguments = $"exec {envVariable.Substring(DockerPrefix.Length)} rabbitmqctl ";
                    return CreateProcess("docker", precomputedArguments + args);
                }

                // call the path from the env var
                return CreateProcess(envVariable, args);
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
                return CreateProcess(path, args);
            }

            precomputedArguments = $"/c \"\"{path}\" ";
            return CreateProcess("cmd.exe", precomputedArguments + args);
        }

        //
        // Shelling Out
        //
        private static string ExecRabbitMQCtl(string args)
        {
            try
            {
                using var process = GetRabbitMqCtlInvokeAction(args);
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

        private static Process CreateProcess(string cmd, string arguments, string? workDirectory = null)
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

        private static void ReportExecFailure(string cmd, string args, string msg)
        {
            Xunit.Assert.True(false, $"Failure while running {cmd} {args}:\n{msg}");
        }

        private static bool IsRunningOnMonoOrDotNetCore()
        {
#if NETCOREAPP
            return true;
#else
            return Type.GetType("Mono.Runtime") != null;
#endif
        }

        //
        // Flow Control
        //
        public static void Block(IConnection conn, Encoding encoding)
        {
            ExecRabbitMQCtl("set_vm_memory_high_watermark 0.000000001");
            // give rabbitmqctl some time to do its job
            Thread.Sleep(1200);
            Publish(conn, encoding);
        }

        public static void Publish(IConnection conn, Encoding encoding)
        {
            IModel ch = conn.CreateModel();
            ch.BasicPublish("amq.fanout", "", encoding.GetBytes("message"));
        }

        public static void Unblock()
        {
            ExecRabbitMQCtl("set_vm_memory_high_watermark 0.4");
        }

        public static void CloseConnection(IConnection conn)
        {
            CloseConnection(GetConnectionPid(conn.ClientProvidedName));
        }

        private static readonly Regex s_getConnectionProperties = new Regex(@"^(?<pid><[^>]*>)\s\[.*""connection_name"",""(?<connection_name>[^""]*)"".*\]$", RegexOptions.Multiline | RegexOptions.Compiled);
        private static string GetConnectionPid(string connectionName)
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

        private static void CloseConnection(string pid)
        {
            ExecRabbitMQCtl($"close_connection \"{pid}\" \"Closed via rabbitmqctl\"");
        }

        public static void CloseAllConnections()
        {
            foreach (var pid in EnumerateConnectionsPid())
            {
                CloseConnection(pid);
            }
        }

        private static string[] EnumerateConnectionsPid()
        {
            string rabbitmqCtlResult = ExecRabbitMQCtl("list_connections --silent pid");
            return rabbitmqCtlResult.Split(newLine, StringSplitOptions.RemoveEmptyEntries);
        }

        public static void RestartRabbitMQ()
        {
            StopRabbitMQ();
            Thread.Sleep(500);
            StartRabbitMQ();
            AwaitRabbitMQ();
        }

        public static void StopRabbitMQ()
        {
            ExecRabbitMQCtl("stop_app");
        }

        public static void StartRabbitMQ()
        {
            ExecRabbitMQCtl("start_app");
        }

        public static void AwaitRabbitMQ()
        {
            ExecRabbitMQCtl("await_startup");
        }
    }
}
