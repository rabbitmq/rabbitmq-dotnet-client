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

using System.Collections.Generic;

using NUnit.Framework;

using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Unit
{

    [TestFixture]
  public class TestIModelAllocation
  {
    public const int CHANNEL_COUNT = 100;

    IConnection _c;

    public int ModelNumber(IModel model)
    {
      return ((ModelBase)((AutorecoveringModel)model).Delegate).Session.ChannelNumber;
    }

    [SetUp] public void Connect()
    {
      _c = new ConnectionFactory().CreateConnection();
    }

    [TearDown] public void Disconnect()
    {
      _c.Close();
    }


    [Test] public void AllocateInOrder()
    {
      for(int i = 1; i <= CHANNEL_COUNT; i++)
        Assert.AreEqual(i, ModelNumber(_c.CreateModel()));
    }

    [Test] public void AllocateAfterFreeingLast() {
      IModel ch = _c.CreateModel();
      Assert.AreEqual(1, ModelNumber(ch));
      ch.Close();
      ch = _c.CreateModel();
      Assert.AreEqual(1, ModelNumber(ch));
    }

    public int CompareModels(IModel x, IModel y)
    {
      int i = ModelNumber(x);
      int j = ModelNumber(y);
      return (i < j) ? -1 : (i == j) ? 0 : 1;
    }

    [Test] public void AllocateAfterFreeingMany() {
      List<IModel> channels = new List<IModel>();

      for(int i = 1; i <= CHANNEL_COUNT; i++)
        channels.Add(_c.CreateModel());

      foreach(IModel channel in channels){
        channel.Close();
      }

      channels = new List<IModel>();

      for(int j = 1; j <= CHANNEL_COUNT; j++)
        channels.Add(_c.CreateModel());

      // In the current implementation the list should actually
      // already be sorted, but we don't want to force that behaviour
      channels.Sort(CompareModels);

      int k = 1;
      foreach(IModel channel in channels)
        Assert.AreEqual(k++, ModelNumber(channel));
    }
  }
}
