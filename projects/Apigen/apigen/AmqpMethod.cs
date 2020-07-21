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
using System.Xml;

namespace RabbitMQ.Client.Apigen
{
    public class AmqpMethod: AmqpEntity {
        public IList<AmqpField> m_Fields;
        public IList<string> m_ResponseMethods;

        public AmqpMethod(XmlNode n)
            : base(n)
        {
            m_Fields = new List<AmqpField>();
            foreach (XmlNode f in n.SelectNodes("field")) {
                m_Fields.Add(new AmqpField(f));
            }
            m_ResponseMethods = new List<string>();
            foreach (XmlNode r in n.SelectNodes("response")) {
                m_ResponseMethods.Add(Apigen.GetString(r, "@name"));
            }
        }

        public bool HasContent {
            get {
                return GetString("@content", null) != null;
            }
        }

        public bool IsSimpleRpcRequest {
            get {
                return m_ResponseMethods.Count == 1;
            }
        }

        public int Index {
            get {
                return GetInt("@index");
            }
        }
    }
}
