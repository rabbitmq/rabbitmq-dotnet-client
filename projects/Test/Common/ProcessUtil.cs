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
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    /// <summary>
    /// Process helper with asynchronous interface
    /// https://gist.github.com/Indigo744/b5f3bd50df4b179651c876416bf70d0a
    /// - Based on https://gist.github.com/georg-jung/3a8703946075d56423e418ea76212745
    /// - And on https://stackoverflow.com/questions/470256/process-waitforexit-asynchronously
    /// </summary>
    public static class ProcessUtil
    {
        /// <summary>
        /// Run a process asynchronously
        /// <para>To capture STDOUT, set StartInfo.RedirectStandardOutput to TRUE</para>
        /// <para>To capture STDERR, set StartInfo.RedirectStandardError to TRUE</para>
        /// </summary>
        /// <param name="startInfo">ProcessStartInfo object</param>
        /// <param name="timeoutMs">The timeout in milliseconds (null for no timeout)</param>
        /// <returns>Result object</returns>
        public static async Task<Result> RunAsync(ProcessStartInfo startInfo)
        {
            var result = new Result();

            using (var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true })
            {
                // List of tasks to wait for a whole process exit
                var processTasks = new List<Task>();

                // === EXITED Event handling ===
                var processExitEvent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                process.Exited += (sender, args) =>
                {
                    processExitEvent.TrySetResult(true);
                };

                processTasks.Add(processExitEvent.Task);

                // === STDOUT handling ===
                var stdOutBuilder = new StringBuilder();

                if (process.StartInfo.RedirectStandardOutput)
                {
                    var stdOutCloseEvent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                    process.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data == null)
                        {
                            stdOutCloseEvent.TrySetResult(true);
                        }
                        else
                        {
                            stdOutBuilder.AppendLine(e.Data);
                        }
                    };

                    processTasks.Add(stdOutCloseEvent.Task);
                }
                else
                {
                    // STDOUT is not redirected, so we won't look for it
                }

                // === STDERR handling ===
                var stdErrBuilder = new StringBuilder();

                if (process.StartInfo.RedirectStandardError)
                {
                    var stdErrCloseEvent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data == null)
                        {
                            stdErrCloseEvent.TrySetResult(true);
                        }
                        else
                        {
                            stdErrBuilder.AppendLine(e.Data);
                        }
                    };

                    processTasks.Add(stdErrCloseEvent.Task);
                }
                else
                {
                    // STDERR is not redirected, so we won't look for it
                }

                // === START OF PROCESS ===
                if (false == process.Start())
                {
                    result.ExitCode = process.ExitCode;
                    return result;
                }

                // Reads the output stream first as needed and then waits because deadlocks are possible
                if (process.StartInfo.RedirectStandardOutput)
                {
                    process.BeginOutputReadLine();
                }
                else
                {
                    // No STDOUT
                }

                if (process.StartInfo.RedirectStandardError)
                {
                    process.BeginErrorReadLine();
                }
                else
                {
                    // No STDERR
                }

                // === ASYNC WAIT OF PROCESS ===

                // Process completion = exit AND stdout (if defined) AND stderr (if defined)
                Task processCompletionTask = Task.WhenAll(processTasks);

                int attempts = 0;
                while (attempts < 3)
                {
                    try
                    {
                        // Task to wait for exit OR timeout (if defined)
                        await processCompletionTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                        // -> Process exited cleanly
                        result.ExitCode = process.ExitCode;
                        break;
                    }
                    catch (OperationCanceledException ex)
                    {
                        KillProcess(process);
                        if (attempts == 2)
                        {
                            throw;
                        }
                        else
                        {
                            Console.WriteLine("[WARNING] caught exception and re-trying ({0}): {1}", attempts, ex);
                        }
                    }
                    catch (TimeoutException ex)
                    {
                        KillProcess(process);
                        if (attempts == 2)
                        {
                            throw;
                        }
                        else
                        {
                            Console.WriteLine("[WARNING] caught exception and re-trying ({0}): {1}", attempts, ex);
                        }
                    }

                    attempts++;
                }

                // Read stdout/stderr
                result.StdOut = stdOutBuilder.ToString();
                result.StdErr = stdErrBuilder.ToString();
            }

            return result;
        }

        private static void KillProcess(Process process)
        {
            try
            {
                process.Kill();
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// Run process result
        /// </summary>
        public class Result
        {
            /// <summary>
            /// Exit code
            /// <para>If NULL, process exited due to timeout</para>
            /// </summary>
            public int? ExitCode { get; set; } = null;

            /// <summary>
            /// Standard error stream
            /// </summary>
            public string StdErr { get; set; } = "";

            /// <summary>
            /// Standard output stream
            /// </summary>
            public string StdOut { get; set; } = "";
        }
    }
}
