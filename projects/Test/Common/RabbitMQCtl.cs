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
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Test
{
    public class RabbitMQCtl
    {
        private readonly ITestOutputHelper _output;

        public RabbitMQCtl(ITestOutputHelper output)
        {
            _output = output;
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
