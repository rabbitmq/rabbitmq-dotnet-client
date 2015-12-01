// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2015 Pivotal Software, Inc.
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
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace RabbitMQ.ServiceModel
{
    public static class DebugHelper
    {
        static long m_started;

        static DebugHelper()
        {
            DebugHelper.Timer = new Stopwatch();
        }

        public static void Start()
        {
            m_started = Timer.ElapsedMilliseconds;
            Timer.Start();
        }

        public static void Stop(string messageFormat, params object[] args)
        {
            Timer.Stop();

            if (Enabled)
            {
                object[] args1 = new object[args.Length + 1];
                args.CopyTo(args1, 1);
                args1[0] = Timer.ElapsedMilliseconds - m_started;
                if (Console.CursorLeft != 0)
                    Console.WriteLine();
                Console.WriteLine(messageFormat, args1);
            }
        }

        private static Stopwatch timer;
        public static Stopwatch Timer {
            get { return timer; }
            set { timer = value; }
        }

        private static bool enabled;
        public static bool Enabled {
            get { return enabled; }
            set { enabled = value; }
        }
    }
}
