// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2014 GoPivotal, Inc.
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
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;
using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using System.Collections.Generic;

namespace RabbitMQ.Client.Unit
{

  [TestFixture]
  public class TestIModelAllocation
  {
    public const int CHANNEL_COUNT = 100;

    IConnection C;

    public int ModelNumber(IModel model)
    {
      return ((ModelBase)model).m_session.ChannelNumber;
    }

    [SetUp] public void Connect()
    {
      C = new ConnectionFactory().CreateConnection();
    }

    [TearDown] public void Disconnect()
    {
      C.Close();
    }


    [Test] public void AllocateInOrder()
    {
      for(int i = 1; i <= CHANNEL_COUNT; i++)
        Assert.AreEqual(i, ModelNumber(C.CreateModel()));
    }

    [Test] public void AllocateAfterFreeingLast() {
      IModel ch = C.CreateModel();
      Assert.AreEqual(1, ModelNumber(ch));
      ch.Close();
      ch = C.CreateModel();
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
        channels.Add(C.CreateModel());

      foreach(IModel channel in channels){
        channel.Close();
      }

      channels = new List<IModel>();

      for(int j = 1; j <= CHANNEL_COUNT; j++)
        channels.Add(C.CreateModel());

      // In the current implementation the list should actually
      // already be sorted, but we don't want to force that behaviour
      channels.Sort(CompareModels);

      int k = 1;
      foreach(IModel channel in channels)
        Assert.AreEqual(k++, ModelNumber(channel));
    }
  }
}
