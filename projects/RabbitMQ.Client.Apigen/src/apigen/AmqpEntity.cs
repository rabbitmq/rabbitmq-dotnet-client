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

using System;
using System.Xml;

namespace RabbitMQ.Client.Apigen {
    public class AmqpEntity {
        public XmlNode m_node;

        public AmqpEntity(XmlNode n) {
            m_node = n;
        }

        public string GetString(string path) {
            return Apigen.GetString(m_node, path);
        }

        public string GetString(string path, string d) {
            return Apigen.GetString(m_node, path, d);
        }

        public int GetInt(string path) {
            return Apigen.GetInt(m_node, path);
        }

        public string Name {
            get {
                return GetString("@name");
            }
        }

        public string DocumentationComment(string prefixSpaces) {
            return DocumentationComment(prefixSpaces, "doc");
        }

        public string DocumentationCommentVariant(string prefixSpaces, string tagname) {
            return DocumentationComment(prefixSpaces, "doc", tagname);
        }

        public string DocumentationComment(string prefixSpaces, string docXpath) {
            return DocumentationComment(prefixSpaces, docXpath, "summary");
        }

        public string DocumentationComment(string prefixSpaces, string docXpath, string tagname) {
            string docStr = GetString(docXpath, "").Trim();
            if (docStr.Length > 0) {
                return (prefixSpaces + "/// <"+tagname+">\n" +
                        GetString(docXpath, "") + "\n</"+tagname+">")
                    .Replace("\n", "\n" + prefixSpaces + "/// ");
            } else {
                return prefixSpaces + "// (no documentation)";
            }
        }
    }
}
