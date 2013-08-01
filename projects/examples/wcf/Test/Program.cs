// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2013 GoPivotal, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at http://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2013 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.ServiceModel.Channels;
using System.ServiceModel;

namespace RabbitMQ.ServiceModel.Test
{
    delegate void TestRun();

    class Program
    {
        public static Binding GetBinding() {
            //return new WSHttpBinding();

            return new RabbitMQBinding(System.Configuration.ConfigurationManager.AppSettings["manual-test-broker-hostname"],
                                       int.Parse(System.Configuration.ConfigurationManager.AppSettings["manual-test-broker-port"]),
                                       RabbitMQ.Client.Protocols.FromConfiguration("manual-test-broker-protocol"));
        }

        static void Main(string[] args)
        {
#if TRACE
            System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());
#endif

#if DEBUG
            Debug.Listeners.Add(new ConsoleTraceListener());
            DebugHelper.Enabled = true;
#endif

            int passed = 0, failed = 0;
            List<ITestCase> tests = new List<ITestCase>();
            tests.Add(new OneWayTest.OneWayTest());
            tests.Add(new TwoWayTest.TwoWayTest());
            tests.Add(new SessionTest.SessionTest());
            tests.Add(new DuplexTest.DuplexTest());
            tests.Add(new FaultTest.FaultTest());
            Banner();

            DateTime started = DateTime.Now;
            foreach (ITestCase test in tests)
            {
                Console.Write("\n[{0}] Running ", DateTime.Now.TimeOfDay);
                Util.WriteLine(ConsoleColor.Cyan, "{0}", test.GetType().Name);
                try
                {
                    test.Run();
                    Console.Write("[{0}] ", DateTime.Now.TimeOfDay);
                    Util.Write(ConsoleColor.Cyan, "{0} ", test.GetType().Name);
                    Util.WriteLine(ConsoleColor.Green, "Passed.");
                    passed++;
                }
                catch (Exception e)
                {
                    Console.WriteLine();
                    Util.Write(ConsoleColor.Cyan, "[{1}] {0} ", test.GetType().Name, DateTime.Now.TimeOfDay);
                    Util.WriteLine(ConsoleColor.Red, "Failed:");
                    Util.WriteLine(ConsoleColor.Magenta, "\t{0}\n\t{1}", e.GetType().Name, e.Message);
                    Util.WriteLine(ConsoleColor.Green, e.StackTrace);
                    failed++;
                    Environment.ExitCode = -1;
                }
            }
            TimeSpan duration = DateTime.Now.Subtract(started);

            Console.WriteLine();
            ConsoleColor tb = Console.BackgroundColor;
            Console.BackgroundColor = ConsoleColor.White;
            Console.ForegroundColor = ConsoleColor.Blue;
            WriteWholeLine();
            WriteWholeLine("  {0,-3} Test Passes   ({1,3}%)", passed, Percent(passed, failed));
            WriteWholeLine("  {0,-3} Test Failures ({1,3}%)", failed, Percent(failed, passed));
            WriteWholeLine("  Test Pass Took {0}h {1}m {2}s", duration.Hours, duration.Minutes, duration.Seconds);
            WriteWholeLine();
            Console.BackgroundColor = tb;
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        private static void Banner()
        {
            ConsoleColor tb = Console.BackgroundColor;
            Console.BackgroundColor = ConsoleColor.White;
            Console.ForegroundColor = ConsoleColor.Blue;
            WriteWholeLine();
            WriteWholeLine(" Test Host for RabbitMQ.ServiceModel");
            WriteWholeLine();
            Console.BackgroundColor = tb;
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        static int Percent(int a, params int[] values)
        {
            double denom = a;
            foreach (int val in values)
                denom += val;

            return (int)((((double)a) / denom) * 100);
        }

        static void WriteWholeLine()
        {
            int i = ConsoleWidth();
            while (i > 1)
            {
                Console.Write(" ");
                i--;
            }
            Console.Write("\n");
        }

        static void WriteWholeLine(string format, params object[] args)
        {
            WriteWholeLine(string.Format(format, args));
        }

        static void WriteWholeLine(string o)
        {
            Console.Write(o);
            int i = ConsoleWidth();
            while (i > o.Length+1)
            {
                Console.Write(" ");
                i--;
            }
            Console.Write("\n");

        }

        static int ConsoleWidth()
        {
            return Environment.GetEnvironmentVariable("TERM") == null ? Console.WindowWidth : 80;
        }
    }
}
