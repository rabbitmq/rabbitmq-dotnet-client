// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007, 2008 LShift Ltd., Cohesive Financial
//   Technologies LLC., and Rabbit Technologies Ltd.
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
//   The contents of this file are subject to the Mozilla Public License
//   Version 1.1 (the "License"); you may not use this file except in
//   compliance with the License. You may obtain a copy of the License at
//   http://www.rabbitmq.com/mpl.html
//
//   Software distributed under the License is distributed on an "AS IS"
//   basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
//   License for the specific language governing rights and limitations
//   under the License.
//
//   The Original Code is The RabbitMQ .NET Client.
//
//   The Initial Developers of the Original Code are LShift Ltd.,
//   Cohesive Financial Technologies LLC., and Rabbit Technologies Ltd.
//
//   Portions created by LShift Ltd., Cohesive Financial Technologies
//   LLC., and Rabbit Technologies Ltd. are Copyright (C) 2007, 2008
//   LShift Ltd., Cohesive Financial Technologies LLC., and Rabbit
//   Technologies Ltd.;
//
//   All Rights Reserved.
//
//   Contributor(s): ______________________________________.
//
//---------------------------------------------------------------------------
using System;
using System.IO;
using System.Threading;

using RabbitMQ.Util;

namespace RabbitMQ.Client.Examples {
    public class SemaphoreStress {
        public const int THREADCOUNT = 1000;
        public const int ACQUIRECOUNT = 100;

        public static RabbitMQ.Util.Semaphore sem = new RabbitMQ.Util.Semaphore();
        public static RabbitMQ.Util.Semaphore done = new RabbitMQ.Util.Semaphore(-THREADCOUNT + 1);

        public static volatile object counterlock = new object();
        public static volatile int started = 0;
        public static volatile int ended = 0;
        public static volatile int retrycounter = 0;
        public static volatile int acquirecounter = 0;

        public static int Main(string[] args) {
            Console.Out.WriteLine("Starting...");
            DateTime start = DateTime.Now;
            for (int i = 0; i < THREADCOUNT; i++) {
                new Thread(new ThreadStart(OneThread)).Start();
            }
            done.Wait();
            DateTime stop = DateTime.Now;
            double delta = ((stop - start).TotalMilliseconds) / 1000.0;
            Console.Out.WriteLine("Elapsed:    {0} s", delta);
            Console.Out.WriteLine("Started:    {0}", started);
            Console.Out.WriteLine("Ended:      {0}", ended);
            Console.Out.WriteLine("Retries:    {0}", retrycounter);
            Console.Out.WriteLine("Acquires:   {0}", acquirecounter);
            Console.Out.WriteLine("Acquires/s: {0} Hz", acquirecounter / delta);
            Console.Out.WriteLine("Final sem counter:  {0}", sem.Value);
            Console.Out.WriteLine("Final done counter: {0}", done.Value);
            return 0;
        }

        public static void OneThread() {
            lock (counterlock) { started++; }
            for (int i = 0; i < ACQUIRECOUNT; i++) {
                // Interestingly, when we use TryWait() like this,
                // sometimes it speeds up the stress test by an order
                // of magnitude or more. I guess we get lucky with
                // lock timing on my hyperthreaded processor.
                //
                // The timings are much more consistent when the whole
                // if statement below is replaced with a simple
                // Wait().
                //
                if (!sem.TryWait()) {
                    lock (counterlock) { retrycounter++; }
                    sem.Wait();
                }
                lock (counterlock) { acquirecounter++; }
                sem.Release();
            }
            lock (counterlock) { ended++; }
            done.Release();
        }
    }
}
