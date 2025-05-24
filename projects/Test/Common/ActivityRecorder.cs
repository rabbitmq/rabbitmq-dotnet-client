// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Xunit;

namespace Test
{
    public class ActivityRecorder : IDisposable
    {
        private string _activitySourceName;
        private string _activityName;

        private readonly ActivityListener _listener;
        private List<Activity> _finishedActivities = new();

        private int _started;
        private int _stopped;

        public int Started => _started;
        public int Stopped => _stopped;

        public Predicate<Activity> Filter { get; set; } = _ => true;
        public bool VerifyParent { get; set; } = true;
        public Activity ExpectedParent { get; set; }

        public Activity LastStartedActivity { get; private set; }
        public Activity LastFinishedActivity { get; private set; }
        public IEnumerable<Activity> FinishedActivities => _finishedActivities;

        public ActivityRecorder(string activitySourceName, string activityName)
        {
            _activitySourceName = activitySourceName;
            _activityName = activityName;
            _listener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == _activitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
                ActivityStarted = (activity) =>
                {
                    if (activity.OperationName == _activityName && Filter(activity))
                    {
                        if (VerifyParent)
                        {
                            Assert.Same(ExpectedParent, activity.Parent);
                        }

                        Interlocked.Increment(ref _started);

                        LastStartedActivity = activity;
                    }
                },
                ActivityStopped = (activity) =>
                {
                    if (activity.OperationName == _activityName && Filter(activity))
                    {
                        if (VerifyParent)
                        {
                            Assert.Same(ExpectedParent, activity.Parent);
                        }

                        Interlocked.Increment(ref _stopped);

                        lock (_finishedActivities)
                        {
                            LastFinishedActivity = activity;
                            _finishedActivities.Add(activity);
                        }
                    }
                }
            };

            ActivitySource.AddActivityListener(_listener);
        }

        public void Dispose() => _listener.Dispose();

        public void VerifyActivityRecorded(int times)
        {
            Assert.Equal(times, Started);
            Assert.Equal(times, Stopped);
        }

        public Activity VerifyActivityRecordedOnce()
        {
            VerifyActivityRecorded(1);
            return LastFinishedActivity;
        }
    }

    public static class ActivityAssert
    {
        public static KeyValuePair<string, object> HasTag(this Activity activity, string name)
        {
            KeyValuePair<string, object> tag = activity.TagObjects.SingleOrDefault(t => t.Key == name);
            if (tag.Key is null)
            {
                Assert.Fail($"The Activity tags should contain {name}.");
            }

            return tag;
        }

        public static void HasTag<T>(this Activity activity, string name, T expectedValue)
        {
            KeyValuePair<string, object> tag = HasTag(activity, name);
            Assert.Equal(expectedValue, (T)tag.Value);
        }

        public static void HasRecordedException(this Activity activity, Exception exception)
        {
            activity.HasRecordedException(exception.GetType().ToString());
        }

        public static void HasRecordedException(this Activity activity, string exceptionTypeName)
        {
            var exceptionEvent = activity.Events.First();
            Assert.Equal("exception", exceptionEvent.Name);
            Assert.Equal(exceptionTypeName,
                exceptionEvent.Tags.SingleOrDefault(t => t.Key == "exception.type").Value);
        }

        public static void IsInError(this Activity activity)
        {
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
        }

        public static void HasNoTag(this Activity activity, string name)
        {
            bool contains = activity.TagObjects.Any(t => t.Key == name);
            Assert.False(contains, $"The Activity tags should not contain {name}.");
        }

        public static void FinishedInOrder(this Activity first, Activity second)
        {
            Assert.True(first.StartTimeUtc + first.Duration < second.StartTimeUtc + second.Duration,
                $"{first.OperationName} should stop before {second.OperationName}");
        }

        public static string CamelToSnake(string camel)
        {
            if (string.IsNullOrEmpty(camel)) return camel;
            StringBuilder bld = new();
            bld.Append(char.ToLower(camel[0]));
            for (int i = 1; i < camel.Length; i++)
            {
                char c = camel[i];
                if (char.IsUpper(c))
                {
                    bld.Append('_');
                }

                bld.Append(char.ToLower(c));
            }

            return bld.ToString();
        }
    }
}
