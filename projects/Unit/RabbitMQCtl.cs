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

#pragma warning disable 2002

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace RabbitMQ.Client.Unit
{
    public static class RabbitMQCtl
    {
        //
        // Shelling Out
        //
        private static Process ExecRabbitMQCtl(string args)
        {
            // Allow the path to the rabbitmqctl.bat to be set per machine
            string envVariable = Environment.GetEnvironmentVariable("RABBITMQ_RABBITMQCTL_PATH");
            string rabbitmqctlPath;

            if (envVariable != null)
            {
                var regex = new Regex(@"^DOCKER:(?<dockerMachine>.+)$");
                Match match = regex.Match(envVariable);

                if (match.Success)
                {
                    return ExecRabbitMQCtlUsingDocker(args, match.Groups["dockerMachine"].Value);
                }
                else
                {
                    rabbitmqctlPath = envVariable;
                }
            }
            else
            {
                // provided by the umbrella
                string umbrellaRabbitmqctlPath;
                // provided in PATH by a RabbitMQ installation
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

                if (File.Exists(umbrellaRabbitmqctlPath))
                {
                    rabbitmqctlPath = umbrellaRabbitmqctlPath;
                }
                else
                {
                    rabbitmqctlPath = providedRabbitmqctlPath;
                }
            }

            return ExecCommand(rabbitmqctlPath, args);
        }

        private static Process ExecRabbitMQCtlUsingDocker(string args, string dockerMachineName)
        {
            var proc = new Process
            {
                StartInfo =
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };

            try
            {
                proc.StartInfo.FileName = "docker";
                proc.StartInfo.Arguments = $"exec {dockerMachineName} rabbitmqctl {args}";
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.RedirectStandardOutput = true;

                proc.Start();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                if (stderr.Length > 0 || proc.ExitCode > 0)
                {
                    string stdout = proc.StandardOutput.ReadToEnd();
                    ReportExecFailure("rabbitmqctl", args, $"{stderr}\n{stdout}");
                }

                return proc;
            }
            catch (Exception e)
            {
                ReportExecFailure("rabbitmqctl", args, e.Message);
                throw;
            }
        }

        private static Process ExecCommand(string command, string args)
        {
            return ExecCommand(command, args, null);
        }

        private static Process ExecCommand(string ctl, string args, string changeDirTo)
        {
            var proc = new Process
            {
                StartInfo =
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };
            if (changeDirTo != null)
            {
                proc.StartInfo.WorkingDirectory = changeDirTo;
            }

            string cmd;
            if (IsRunningOnMonoOrDotNetCore())
            {
                cmd = ctl;
            }
            else
            {
                cmd = "cmd.exe";
                args = $"/c \"\"{ctl}\" {args}\"";
            }

            try
            {
                proc.StartInfo.FileName = cmd;
                proc.StartInfo.Arguments = args;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.RedirectStandardOutput = true;

                proc.Start();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                if (stderr.Length > 0 || proc.ExitCode > 0)
                {
                    string stdout = proc.StandardOutput.ReadToEnd();
                    ReportExecFailure(cmd, args, $"{stderr}\n{stdout}");
                }

                return proc;
            }
            catch (Exception e)
            {
                ReportExecFailure(cmd, args, e.Message);
                throw;
            }
        }

        private static void ReportExecFailure(string cmd, string args, string msg)
        {
            Console.WriteLine($"Failure while running {cmd} {args}:\n{msg}");
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
            ch.BasicPublish("amq.fanout", "", null, encoding.GetBytes("message"));
        }


        public static void Unblock()
        {
            ExecRabbitMQCtl("set_vm_memory_high_watermark 0.4");
        }

        private static readonly Regex s_getConnectionProperties = new Regex(@"(?<pid>.*)\s\[.*\""connection_name\"",\""(?<connection_name>.*?)\"".*\]", RegexOptions.Compiled);
        public class ConnectionInfo
        {
            public string Pid
            {
                get; set;
            }

            public string Name
            {
                get; set;
            }

            public ConnectionInfo(string pid, string name)
            {
                Pid = pid;
                Name = name;
            }

            public override string ToString()
            {
                return $"pid = {Pid}, name: {Name}";
            }
        }

        public static List<ConnectionInfo> ListConnections()
        {
            Process proc = ExecRabbitMQCtl("list_connections --silent pid client_properties");
            string stdout = proc.StandardOutput.ReadToEnd();

            try
            {
                // {Environment.NewLine} is not sufficient
                var matches = s_getConnectionProperties.Matches(stdout);
                if (matches.Count > 0)
                {
                    var list = new List<ConnectionInfo>(matches.Count);
                    for (int i = 0; i < matches.Count; i++)
                    {
                        var s = matches[i];
                        Debug.Assert(s.Success, "Unable to parse connection list.");
                        Debug.Assert(s.Groups.ContainsKey("pid"), "Unable to parse pid from {stdout}");
                        Debug.Assert(s.Groups.ContainsKey("connection_name"), "Unable to parse connection_name from {stdout}");
                        list.Add(new ConnectionInfo(s.Groups["pid"].Value, s.Groups["connection_name"].Value));
                    }

                    return list;
                }

                return null;
            }
            catch (Exception)
            {
                Console.WriteLine($"Bad response from rabbitmqctl list_connections --silent pid client_properties{Environment.NewLine}{stdout}");
                throw;
            }
        }

        public static void CloseConnection(IConnection conn)
        {
            ConnectionInfo ci = ListConnections().First(x => conn.ClientProvidedName == x.Name);
            CloseConnection(ci.Pid);
        }

        public static void CloseAllConnections()
        {
            List<ConnectionInfo> cs = ListConnections();
            foreach (ConnectionInfo c in cs)
            {
                CloseConnection(c.Pid);
            }
        }

        public static void CloseConnection(string pid)
        {
            ExecRabbitMQCtl($"close_connection \"{pid}\" \"Closed via rabbitmqctl\"");
        }

        public static void RestartRabbitMQ()
        {
            StopRabbitMQ();
            Thread.Sleep(500);
            StartRabbitMQ();
        }

        public static void StopRabbitMQ()
        {
            ExecRabbitMQCtl("stop_app");
        }

        public static void StartRabbitMQ()
        {
            ExecRabbitMQCtl("start_app");
        }
    }
}
