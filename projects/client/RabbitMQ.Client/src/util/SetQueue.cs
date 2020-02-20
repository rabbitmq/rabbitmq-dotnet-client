// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at https://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace RabbitMQ.Util
{
    public class SetQueue<T>
    {
        private HashSet<T> _members = new HashSet<T>();
        private LinkedList<T> _queue = new LinkedList<T>();

        public bool Enqueue(T item)
        {
            if(_members.Contains(item))
            {
                return false;
            }
            _members.Add(item);
            _queue.AddLast(item);
            return true;
        }

        public T Dequeue()
        {
            if (_queue.Count == 0)
            {
                return default;
            }
            T item = _queue.First.Value;
            _queue.RemoveFirst();
            _members.Remove(item);
            return item;
        }

        public bool Contains(T item)
        {
            return _members.Contains(item);
        }

        public bool IsEmpty()
        {
            return _members.Count == 0;
        }

        public bool Remove(T item)
        {
            _queue.Remove(item);
            return _members.Remove(item);
        }

        public void Clear()
        {
            _queue.Clear();
            _members.Clear();
        }
    }
}
