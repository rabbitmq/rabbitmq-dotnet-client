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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;

using RabbitMQ.Client.Apigen.Attributes;

namespace RabbitMQ.Client.Apigen
{
    public static class ApigenExtensions
    {
        public static T Attribute<T>(this MemberInfo mi) where T : Attribute
        {
            return mi.GetCustomAttribute<T>(false);
        }

        public static T Attribute<T>(this ParameterInfo pi) where T : Attribute
        {
            return pi.GetCustomAttribute<T>(false);
        }

        public static T Attribute<T>(this ICustomAttributeProvider p) where T : Attribute
        {
            return p.GetCustomAttributes(typeof(T), false).Cast<T>().SingleOrDefault();
        }
    }

    public class Apigen
    {
        ///////////////////////////////////////////////////////////////////////////
        // Entry point

        public static void Main(string[] args)
        {
            Apigen instance = new Apigen(new List<string>(args));
            instance.Generate();
        }

        ///////////////////////////////////////////////////////////////////////////
        // XML utilities

        public static XmlNodeList GetNodes(XmlNode n0, string path)
        {
            return n0.SelectNodes(path);
        }

        public static string GetString(XmlNode n0, string path, string d)
        {
            XmlNode n = n0.SelectSingleNode(path);
            string pathOrDefault = (n == null) ? d : n.InnerText;
            return xmlStringMapper(pathOrDefault);
        }

        public static string GetString(XmlNode n0, string path)
        {
            string s = GetString(n0, path, null);
            if (s == null)
            {
                throw new Exception($"Missing spec XML node: {path}");
            }
            return s;
        }

        public static int GetInt(XmlNode n0, string path, int d)
        {
            string s = GetString(n0, path, null);
            return (s == null) ? d : int.Parse(s);
        }

        public static int GetInt(XmlNode n0, string path)
        {
            return int.Parse(GetString(n0, path));
        }

        /// <summary>
        /// Rename all instances of an entire string from the XML spec
        /// </summary>
        /// <param name="xmlString">input string from XML spec</param>
        /// <returns>renamed string</returns>
        private static string xmlStringMapper(string xmlString)
        {
            switch (xmlString)
            {
                case "no-wait":
                    return "nowait";
                default:
                    return xmlString;
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        // Name manipulation and mangling for C#

        public static string MangleConstant(string name)
        {
            // Previously, we used C_STYLE_CONSTANT_NAMES:
            /*
              return name
              .Replace(" ", "_")
              .Replace("-", "_").
              ToUpper();
            */
            // ... but TheseKindsOfNames are more in line with .NET style guidelines.
            return MangleClass(name);
        }

        public static IList<string> IdentifierParts(string name)
        {
            IList<string> result = new List<string>();
            foreach (string s1 in name.Split(new char[] { '-', ' ' }))
            {
                result.Add(s1);
            }
            return result;
        }

        public static string MangleClass(string name)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string s in IdentifierParts(name))
            {
                sb.Append(char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant());
            }
            return sb.ToString();
        }

        public static string MangleMethod(string name)
        {
            StringBuilder sb = new StringBuilder();
            bool useUpper = false;
            foreach (string s in IdentifierParts(name))
            {
                if (useUpper)
                {
                    sb.Append(char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant());
                }
                else
                {
                    sb.Append(s.ToLowerInvariant());
                    useUpper = true;
                }
            }
            return sb.ToString();
        }

        public static string MangleMethodClass(AmqpClass c, AmqpMethod m)
        {
            return MangleClass(c.Name) + MangleClass(m.Name);
        }

        ///////////////////////////////////////////////////////////////////////////

        public string m_inputXmlFilename;
        public string m_outputFilename;

        public XmlDocument m_spec = null;
        public TextWriter m_outputFile = null;

        public int m_majorVersion;
        public int m_minorVersion;
        public int m_revision = 0;
        public string m_apiName;
        public bool m_emitComments = false;

        public Type m_modelType = typeof(RabbitMQ.Client.Impl.IFullModel);
        public IList<Type> m_modelTypes = new List<Type>();
        public IList<KeyValuePair<string, int>> m_constants = new List<KeyValuePair<string, int>>();
        public IList<AmqpClass> m_classes = new List<AmqpClass>();
        public IDictionary<string, string> m_domains = new Dictionary<string, string>();

        public static IDictionary<string, string> m_primitiveTypeMap;
        public static IDictionary<string, bool> m_primitiveTypeFlagMap;

        static Apigen()
        {
            m_primitiveTypeMap = new Dictionary<string, string>();
            m_primitiveTypeFlagMap = new Dictionary<string, bool>();
            InitPrimitiveType("octet", "byte", false);
            InitPrimitiveType("shortstr", "string", true);
            InitPrimitiveType("longstr", "byte[]", true);
            InitPrimitiveType("short", "ushort", false);
            InitPrimitiveType("long", "uint", false);
            InitPrimitiveType("longlong", "ulong", false);
            InitPrimitiveType("bit", "bool", false);
            InitPrimitiveType("table", "IDictionary<string, object>", true);
            InitPrimitiveType("timestamp", "AmqpTimestamp", false);
            InitPrimitiveType("content", "byte[]", true);
        }

        public static void InitPrimitiveType(string amqpType, string dotnetType, bool isReference)
        {
            m_primitiveTypeMap[amqpType] = dotnetType;
            m_primitiveTypeFlagMap[amqpType] = isReference;
        }

        public void HandleOption(string opt)
        {
            if (opt.StartsWith("--apiName:"))
            {
                m_apiName = opt.Substring(9);
            }
            else if (opt == "-c")
            {
                m_emitComments = true;
            }
            else
            {
                Console.Error.WriteLine($"Unsupported command-line option: {opt}");
                Usage();
            }
        }

        public void Usage()
        {
            Console.Error.WriteLine("Usage: Apigen.exe [options ...] <input-spec-xml> <output-csharp-file>");
            Console.Error.WriteLine("  Options include:");
            Console.Error.WriteLine("    --apiName:<identifier>");
            Console.Error.WriteLine("  The apiName option is required.");
            Environment.Exit(1);
        }

        public Apigen(IList<string> args)
        {
            while (args.Count > 0 && args[0].StartsWith("--"))
            {
                HandleOption(args[0]);
                args.RemoveAt(0);
            }
            if ((args.Count < 2)
                || (m_apiName == null))
            {
                Usage();
            }
            m_inputXmlFilename = args[0];
            m_outputFilename = args[1];
        }

        ///////////////////////////////////////////////////////////////////////////

        public static string ApiNamespaceBase => "RabbitMQ.Client.Framing";

        public static string ImplNamespaceBase => "RabbitMQ.Client.Framing.Impl";

        public void Generate()
        {
            LoadSpec();
            ParseSpec();
            ReflectModel();
            GenerateOutput();
        }

        public void LoadSpec()
        {
            Console.WriteLine($"* Loading spec from '{m_inputXmlFilename}'");
            m_spec = new XmlDocument();

            using (var stream = new FileStream(m_inputXmlFilename, FileMode.Open, FileAccess.Read))
            {
                m_spec.Load(stream);
            }
        }

        public void ParseSpec()
        {
            m_majorVersion = GetInt(m_spec, "/amqp/@major");
            m_minorVersion = GetInt(m_spec, "/amqp/@minor");
            m_revision = GetInt(m_spec, "/amqp/@revision");

            Console.WriteLine("* Parsing spec for version {0}.{1}.{2}", m_majorVersion, m_minorVersion, m_revision);
            foreach (XmlNode n in m_spec.SelectNodes("/amqp/constant"))
            {
                m_constants.Add(new KeyValuePair<string, int>(GetString(n, "@name"), GetInt(n, "@value")));
            }
            foreach (XmlNode n in m_spec.SelectNodes("/amqp/class"))
            {
                m_classes.Add(new AmqpClass(n));
            }
            foreach (XmlNode n in m_spec.SelectNodes("/amqp/domain"))
            {
                m_domains[GetString(n, "@name")] = GetString(n, "@type");
            }
        }

        public void ReflectModel()
        {
            m_modelTypes.Add(m_modelType);
            for (int i = 0; i < m_modelTypes.Count; i++)
            {
                foreach (Type intf in m_modelTypes[i].GetInterfaces())
                {
                    m_modelTypes.Add(intf);
                }
            }
        }

        public string ResolveDomain(string d)
        {
            while (m_domains.ContainsKey(d))
            {
                string newD = m_domains[d];
                if (d.Equals(newD))
                {
                    break;
                }

                d = newD;
            }
            return d;
        }

        public string MapDomain(string d)
        {
            return m_primitiveTypeMap[ResolveDomain(d)];
        }

        public string VersionToken()
        {
            return $"v{m_majorVersion}_{m_minorVersion}";
        }

        public void GenerateOutput()
        {
            Console.WriteLine($"* Generating code into '{m_outputFilename}'");

            string directory = Path.GetDirectoryName(m_outputFilename);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var stream = new FileStream(m_outputFilename, FileMode.Create, FileAccess.Write))
            {
                m_outputFile = new StreamWriter(stream);
                EmitPrelude();
                EmitPublic();
                EmitPrivate();
                m_outputFile.Dispose();
            }
        }

        public void Emit(object o)
        {
            m_outputFile.Write(o);
        }

        public void EmitLine(object o)
        {
            m_outputFile.WriteLine(o);
        }

        public void EmitSpecComment(object o)
        {
            if (m_emitComments)
            {
                EmitLine(o);
            }
        }

        public void EmitPrelude()
        {
            const string prelude =
@"// Autogenerated code. Do not edit.

// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2020 VMware, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the ""License"");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       https://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an ""AS IS"" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//   The contents of this file are subject to the Mozilla Public License
//   Version 1.1 (the ""License""); you may not use this file except in
//   compliance with the License. You may obtain a copy of the License at
//   https://www.rabbitmq.com/mpl.html
//
//   Software distributed under the License is distributed on an ""AS IS""
//   basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
//   License for the specific language governing rights and limitations
//   under the License.
//
//   The Original Code is RabbitMQ.
//
//   The Initial Developer of the Original Code is Pivotal Software, Inc.
//   Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;

using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;
";
            EmitLine(prelude);
        }

        public void EmitPublic()
        {
            int port = GetInt(m_spec, "/amqp/@port");
            string publicVars =
$@"namespace {ApiNamespaceBase}
{{
  internal sealed class Protocol : {ImplNamespaceBase}.ProtocolBase
  {{
    ///<summary>Protocol major version (= {m_majorVersion})</summary>
    public override int MajorVersion => {m_majorVersion};

    ///<summary>Protocol minor version (= {m_minorVersion})</summary>
    public override int MinorVersion => {m_minorVersion};

    ///<summary>Protocol revision (= {m_revision})</summary>
    public override int Revision => {m_revision};

    ///<summary>Protocol API name (= {m_apiName})</summary>
    public override string ApiName => ""{m_apiName}"";

    ///<summary>Default TCP port (= {port})</summary>
    public override int DefaultPort => {port};
";

            EmitLine("namespace RabbitMQ.Client");
            EmitLine("{");
            EmitLine("  public static class Constants");
            EmitLine("  {");
            foreach (KeyValuePair<string, int> de in m_constants)
            {
                EmitLine($"    ///<summary>(= {de.Value})</summary>");
                EmitLine($"    public const int {MangleConstant(de.Key)} = {de.Value};");
            }
            EmitLine("  }");
            EmitLine("}");
            EmitLine(publicVars);
            EmitMethodArgumentReader();
            EmitLine("");
            EmitContentHeaderReader();
            EmitLine("  }");
            foreach (AmqpClass c in m_classes)
            {
                EmitClassMethods(c);
            }
            foreach (AmqpClass c in m_classes)
            {
                if (c.NeedsProperties)
                {
                    EmitClassProperties(c);
                }
            }
            EmitLine("}");
        }

        public void EmitAutogeneratedSummary(string prefixSpaces, string extra)
        {
            EmitLine($"{prefixSpaces}/// <summary>Autogenerated type. {extra}</summary>");
        }

        public void EmitClassMethods(AmqpClass c)
        {
            foreach (AmqpMethod m in c.m_Methods)
            {
                EmitAutogeneratedSummary("  ", $"AMQP specification method \"{c.Name}.{m.Name}\".");
                EmitSpecComment(m.DocumentationCommentVariant("  ", "remarks"));
                EmitLine($"  internal interface I{MangleMethodClass(c, m)} : IMethod");
                EmitLine("  {");
                foreach (AmqpField f in m.m_Fields)
                {
                    EmitSpecComment(f.DocumentationComment("    "));
                    EmitLine($"    {MapDomain(f.Domain)} {MangleClass(f.Name)} {{ get; }}");
                }
                EmitLine("  }");
            }
        }

        public bool HasFactoryMethod(AmqpClass c)
        {
            foreach (Type t in m_modelTypes)
            {
                foreach (MethodInfo method in t.GetMethods())
                {
                    AmqpContentHeaderFactoryAttribute f = method.Attribute<AmqpContentHeaderFactoryAttribute>();
                    if (f != null && MangleClass(f.m_contentClass) == MangleClass(c.Name))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool IsBoolean(AmqpField f)
        {
            return ResolveDomain(f.Domain) == "bit";
        }

        public bool IsReferenceType(AmqpField f)
        {
            return m_primitiveTypeFlagMap[ResolveDomain(f.Domain)];
        }

        public bool IsAmqpClass(Type t)
        {
            foreach (AmqpClass c in m_classes)
            {
                if (c.Name == t.Name)
                {
                    return true;
                }
            }
            return false;
        }

        public void EmitClassProperties(AmqpClass c)
        {
            bool hasCommonApi = HasFactoryMethod(c);
            string propertiesBaseClass = $"RabbitMQ.Client.Impl.{(hasCommonApi ? $"{MangleClass(c.Name)}Properties" : "ContentHeaderBase")}";
            string maybeOverride = hasCommonApi ? "override " : "";

            EmitAutogeneratedSummary("  ", $"AMQP specification content header properties for content class \"{c.Name}\"");
            EmitSpecComment(c.DocumentationCommentVariant("  ", "remarks"));
            EmitLine($"  internal sealed class {MangleClass(c.Name)}Properties : {propertiesBaseClass}");
            EmitLine("  {");
            foreach (AmqpField f in c.m_Fields)
            {
                EmitLine($"    private {MapDomain(f.Domain)} _{MangleMethod(f.Name)};");
            }
            EmitLine("");
            foreach (AmqpField f in c.m_Fields)
            {
                EmitSpecComment(f.DocumentationComment("    ", "@label"));
                EmitLine($"    public {maybeOverride}{MapDomain(f.Domain)} {MangleClass(f.Name)}");
                EmitLine("    {");
                EmitLine($"      get => _{MangleMethod(f.Name)};");
                EmitLine($"      set => _{MangleMethod(f.Name)} = value;");
                EmitLine("    }");
                EmitLine("");
            }
            foreach (AmqpField f in c.m_Fields)
            {
                if (!IsBoolean(f))
                {
                    EmitLine($"    public {maybeOverride}void Clear{MangleClass(f.Name)}() => _{MangleMethod(f.Name)} = default;");
                    EmitLine("");
                }
            }

            foreach (AmqpField f in c.m_Fields)
            {
                if (!IsBoolean(f))
                {
                    EmitLine($"    public {maybeOverride}bool Is{MangleClass(f.Name)}Present() => _{MangleMethod(f.Name)} != default;");
                    EmitLine("");
                }
            }

            EmitLine($"    public {MangleClass(c.Name)}Properties() {{ }}");
            EmitLine($"    public override ushort ProtocolClassId => {c.Index};");
            EmitLine($"    public override string ProtocolClassName => \"{c.Name}\";");
            EmitLine("");
            EmitLine("    internal override void ReadPropertiesFrom(ref Client.Impl.ContentHeaderPropertyReader reader)");
            EmitLine("    {");
            foreach (AmqpField f in c.m_Fields)
            {
                if (IsBoolean(f))
                {
                    EmitLine($"      var {MangleMethod(f.Name)} = reader.ReadBit();");
                }
                else
                {
                    EmitLine($"      var {MangleMethod(f.Name)}_present = reader.ReadPresence();");
                }
            }
            EmitLine("      reader.FinishPresence();");
            foreach (AmqpField f in c.m_Fields)
            {
                if (!IsBoolean(f))
                {
                    EmitLine($"      if ({MangleMethod(f.Name)}_present) {{ _{MangleMethod(f.Name)} = reader.Read{MangleClass(ResolveDomain(f.Domain))}(); }}");
                }
            }
            EmitLine("    }");
            EmitLine("");
            EmitLine("    internal override void WritePropertiesTo(ref Client.Impl.ContentHeaderPropertyWriter writer)");
            EmitLine("    {");
            foreach (AmqpField f in c.m_Fields)
            {
                if (IsBoolean(f))
                {
                    EmitLine($"      writer.WriteBit(_{MangleMethod(f.Name)});");
                }
                else
                {
                    EmitLine($"      writer.WritePresence(Is{MangleClass(f.Name)}Present());");
                }
            }
            EmitLine("      writer.FinishPresence();");
            foreach (AmqpField f in c.m_Fields)
            {
                if (!IsBoolean(f))
                {
                    EmitLine($"      if (Is{MangleClass(f.Name)}Present()) {{ writer.Write{MangleClass(ResolveDomain(f.Domain))}(_{MangleMethod(f.Name)}); }}");
                }
            }
            EmitLine("    }");
            EmitLine("");
            EmitLine("    public override int GetRequiredPayloadBufferSize()");
            EmitLine("    {");
            EmitLine($"      int bufferSize = {Math.Max((int)Math.Ceiling(c.m_Fields.Count / 15.0), 1) * 2}; // number of presence fields ({c.m_Fields.Count}) in 2 bytes blocks");
            foreach (AmqpField f in c.m_Fields)
            {
                switch (MapDomain(f.Domain))
                {
                    case "byte":
                        EmitLine($"      if (Is{MangleClass(f.Name)}Present()) {{ bufferSize++; }} // _{MangleMethod(f.Name)} in bytes");
                        break;
                    case "string":
                        EmitLine($"      if (Is{MangleClass(f.Name)}Present()) {{ bufferSize += 1 + Encoding.UTF8.GetByteCount(_{MangleMethod(f.Name)}); }} // _{MangleMethod(f.Name)} in bytes");
                        break;
                    case "byte[]":
                        EmitLine($"      if (Is{MangleClass(f.Name)}Present()) {{ bufferSize += 4 + _{MangleMethod(f.Name)}.Length; }} // _{MangleMethod(f.Name)} in bytes");
                        break;
                    case "ushort":
                        EmitLine($"      if (Is{MangleClass(f.Name)}Present()) {{ bufferSize += 2; }} // _{MangleMethod(f.Name)} in bytes");
                        break;
                    case "uint":
                        EmitLine($"      if (Is{MangleClass(f.Name)}Present()) {{ bufferSize += 4; }} // _{MangleMethod(f.Name)} in bytes");
                        break;
                    case "ulong":
                    case "AmqpTimestamp":
                        EmitLine($"      if (Is{MangleClass(f.Name)}Present()) {{ bufferSize += 8; }} // _{MangleMethod(f.Name)} in bytes");
                        break;
                    case "bool":
                        // TODO: implement if used, not used anywhere yet
                        break;
                    case "IDictionary<string, object>":
                        EmitLine($"      if (Is{MangleClass(f.Name)}Present()) {{ bufferSize += WireFormatting.GetTableByteCount(_{MangleMethod(f.Name)}); }} // _{MangleMethod(f.Name)} in bytes");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Can't handle size calculations for type = {f.Domain};");
                }
            }

            EmitLine("      return bufferSize;");
            EmitLine("    }");
            EmitLine("");
            EmitLine("    public override void AppendPropertyDebugStringTo(StringBuilder sb)");
            EmitLine("    {");
            EmitLine("      sb.Append(\"(\");");
            {
                int remaining = c.m_Fields.Count;
                foreach (AmqpField f in c.m_Fields)
                {
                    Emit($"      sb.Append(\"{f.Name}=\")");
                    if (IsBoolean(f))
                    {
                        Emit($".Append(_{MangleMethod(f.Name)})");
                    }
                    else
                    {
                        Emit($".Append(Is{MangleClass(f.Name)}Present() ? _{MangleMethod(f.Name)}.ToString() : \"_\")");
                    }
                    remaining--;
                    if (remaining > 0)
                    {
                        EmitLine(".Append(\", \");");
                    }
                    else
                    {
                        EmitLine(";");
                    }
                }
            }
            EmitLine("      sb.Append(\")\");");
            EmitLine("    }");
            EmitLine("  }");
        }

        public void EmitPrivate()
        {
            EmitLine($"namespace {ImplNamespaceBase}");
            EmitLine("{");
            EmitLine("  internal static class ClassConstants");
            EmitLine("  {");
            foreach (AmqpClass c in m_classes)
            {
                EmitLine($"    internal const ushort {MangleConstant(c.Name)} = {c.Index};");
            }
            EmitLine("  }");
            EmitLine("");
            EmitLine("  internal enum ClassId");
            EmitLine("  {");
            foreach (AmqpClass c in m_classes)
            {
                EmitLine($"      {MangleConstant(c.Name)} = {c.Index},");
            }
            EmitLine("    Invalid = -1,");
            EmitLine("  }");
            EmitLine("");
            foreach (AmqpClass c in m_classes)
            {
                EmitLine($"  internal static class {MangleClass(c.Name)}MethodConstants");
                EmitLine("  {");
                foreach (AmqpMethod m in c.m_Methods)
                {
                    EmitLine($"    internal const ushort {MangleConstant(m.Name)} = {m.Index};");
                }
                EmitLine("  }");
            }
            EmitLine("");
            foreach (AmqpClass c in m_classes)
            {
                EmitClassMethodImplementations(c);
            }
            EmitLine("");
            EmitModelImplementation();
            EmitLine("}");
        }

        public void EmitClassMethodImplementations(AmqpClass c)
        {
            foreach (AmqpMethod m in c.m_Methods)
            {
                EmitAutogeneratedSummary("  ", "Private implementation class - do not use directly.");
                EmitLine($"  internal sealed class {MangleMethodClass(c, m)} : Client.Impl.MethodBase, I{MangleMethodClass(c, m)}");
                EmitLine("  {");
                foreach (AmqpField f in m.m_Fields)
                {
                    EmitLine($"    public {MapDomain(f.Domain)} _{MangleMethod(f.Name)};");
                }
                EmitLine("");
                foreach (AmqpField f in m.m_Fields)
                {
                    EmitLine($"    {MapDomain(f.Domain)} I{MangleMethodClass(c, m)}.{MangleClass(f.Name)} => _{MangleMethod(f.Name)};");
                }
                EmitLine("");
                if (m.m_Fields.Count > 0)
                {
                    EmitLine($"    public {MangleMethodClass(c, m)}() {{}}");
                }
                EmitLine($"    public {MangleMethodClass(c, m)}({string.Join(", ", m.m_Fields.Select(f => $"{MapDomain(f.Domain)} @{MangleClass(f.Name)}"))})");
                EmitLine("    {");
                foreach (AmqpField f in m.m_Fields)
                {
                    EmitLine($"      _{MangleMethod(f.Name)} = @{MangleClass(f.Name)};");
                }
                EmitLine("    }");
                EmitLine("");
                EmitLine($"    public override ushort ProtocolClassId => ClassConstants.{MangleConstant(c.Name)};");
                EmitLine($"    public override ushort ProtocolMethodId => {MangleConstant(c.Name)}MethodConstants.{MangleConstant(m.Name)};");
                EmitLine($"    public override string ProtocolMethodName => \"{c.Name}.{m.Name}\";");
                EmitLine($"    public override bool HasContent => {(m.HasContent ? "true" : "false")};");
                EmitLine("");
                EmitLine("    public override void ReadArgumentsFrom(ref Client.Impl.MethodArgumentReader reader)");
                EmitLine("    {");
                foreach (AmqpField f in m.m_Fields)
                {
                    EmitLine($"      _{MangleMethod(f.Name)} = reader.Read{MangleClass(ResolveDomain(f.Domain))}();");
                }
                EmitLine("    }");
                EmitLine("");
                EmitLine("    public override void WriteArgumentsTo(ref Client.Impl.MethodArgumentWriter writer)");
                EmitLine("    {");
                var lastWasBitClass = false;
                foreach (AmqpField f in m.m_Fields)
                {
                    string mangleClass = MangleClass(ResolveDomain(f.Domain));
                    if (mangleClass != "Bit")
                    {
                        if (lastWasBitClass)
                        {
                            EmitLine($"      writer.EndBits();");
                            lastWasBitClass = false;
                        }
                    }
                    else
                    {
                        lastWasBitClass = true;
                    }

                    EmitLine($"      writer.Write{mangleClass}(_{MangleMethod(f.Name)});");
                }
                if (lastWasBitClass)
                {
                    EmitLine($"      writer.EndBits();");
                }
                EmitLine("    }");
                EmitLine("");
                EmitLine("    public override int GetRequiredBufferSize()");
                EmitLine("    {");

                int bitCount = 0;
                int bytesSize = 0;
                var commentBuilder = new StringBuilder(" // bytes for ");
                foreach (AmqpField f in m.m_Fields)
                {
                    switch (MapDomain(f.Domain))
                    {
                        case "byte":
                            bytesSize++;
                            commentBuilder.Append('_').Append(MangleMethod(f.Name)).Append(", ");
                            break;
                        case "string":
                            bytesSize++;
                            commentBuilder.Append("length of _").Append(MangleMethod(f.Name)).Append(", ");
                            break;
                        case "byte[]":
                            bytesSize += 4;
                            commentBuilder.Append("length of _").Append(MangleMethod(f.Name)).Append(", ");
                            break;
                        case "ushort":
                            bytesSize += 2;
                            commentBuilder.Append('_').Append(MangleMethod(f.Name)).Append(", ");
                            break;
                        case "uint":
                            bytesSize += 4;
                            commentBuilder.Append('_').Append(MangleMethod(f.Name)).Append(", ");
                            break;
                        case "ulong":
                        case "AmqpTimestamp":
                            bytesSize += 8;
                            commentBuilder.Append('_').Append(MangleMethod(f.Name)).Append(", ");
                            break;
                        case "bool":
                            if (bitCount == 0)
                            {
                                commentBuilder.Append("bit fields, ");
                            }
                            bitCount++;
                            break;
                        case "IDictionary<string, object>":
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"Can't handle size calculations for type = {f.Domain};");
                    }
                }

                // 13 = " // bytes for "
                if (commentBuilder.Length > 14)
                {
                    // cut of last ", "
                    commentBuilder.Length -= 2;
                }
                else
                {
                    commentBuilder.Clear();
                }
                bytesSize += (int)Math.Ceiling(bitCount / 8.0);
                EmitLine($"      int bufferSize = {bytesSize};{commentBuilder}");
                foreach (AmqpField f in m.m_Fields)
                {
                    switch (MapDomain(f.Domain))
                    {
                        case "byte":
                        case "ushort":
                        case "uint":
                        case "ulong":
                        case "AmqpTimestamp":
                        case "bool":
                            // size already calculated
                            break;
                        case "string":
                            EmitLine($"      bufferSize += Encoding.UTF8.GetByteCount(_{MangleMethod(f.Name)}); // _{MangleMethod(f.Name)} in bytes");
                            break;
                        case "byte[]":
                            EmitLine($"      bufferSize += _{MangleMethod(f.Name)}.Length; // _{MangleMethod(f.Name)} in bytes");
                            break;
                        case "IDictionary<string, object>":
                            EmitLine($"      bufferSize += WireFormatting.GetTableByteCount(_{MangleMethod(f.Name)}); // _{MangleMethod(f.Name)} in bytes");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"Can't handle size calculations for type = {f.Domain};");
                    }
                }

                EmitLine("      return bufferSize;");
                EmitLine("    }");
                EmitLine("");
                EmitLine("    public override void AppendArgumentDebugStringTo(StringBuilder sb)");
                EmitLine("    {");
                EmitLine("      sb.Append('(');");
                {
                    int remaining = m.m_Fields.Count;
                    foreach (AmqpField f in m.m_Fields)
                    {
                        Emit($"      sb.Append(_{MangleMethod(f.Name)})");
                        remaining--;
                        if (remaining > 0)
                        {
                            EmitLine(".Append(',');");
                        }
                        else
                        {
                            EmitLine(";");
                        }
                    }
                }
                EmitLine("      sb.Append(')');");
                EmitLine("    }");
                EmitLine("  }");
            }
        }

        public void EmitMethodArgumentReader()
        {
            EmitLine("    internal override Client.Impl.MethodBase DecodeMethodFrom(ReadOnlySpan<byte> span)");
            EmitLine("    {");
            EmitLine("      ushort classId = Util.NetworkOrderDeserializer.ReadUInt16(span);");
            EmitLine("      ushort methodId = Util.NetworkOrderDeserializer.ReadUInt16(span.Slice(2));");
            EmitLine("      Client.Impl.MethodBase result = DecodeMethodFrom(classId, methodId);");
            EmitLine("      if(result != null)");
            EmitLine("      {");
            EmitLine("        Client.Impl.MethodArgumentReader reader = new Client.Impl.MethodArgumentReader(span.Slice(4));");
            EmitLine("        result.ReadArgumentsFrom(ref reader);");
            EmitLine("        return result;");
            EmitLine("      }");
            EmitLine("");
            EmitLine("      throw new Client.Exceptions.UnknownClassOrMethodException(classId, methodId);");
            EmitLine("    }");
            EmitLine("");
            EmitLine("    internal Client.Impl.MethodBase DecodeMethodFrom(ushort classId, ushort methodId)");
            EmitLine("    {");
            EmitLine("      switch ((classId << 16) | methodId)");
            EmitLine("      {");
            foreach (AmqpClass c in m_classes)
            {
                foreach (AmqpMethod m in c.m_Methods)
                {
                    EmitLine($"        case (ClassConstants.{MangleConstant(c.Name)} << 16) | {MangleConstant(c.Name)}MethodConstants.{MangleConstant(m.Name)}: return new Impl.{MangleMethodClass(c, m)}();");
                }
            }
            EmitLine("        default: return null;");
            EmitLine("      }");
            EmitLine("    }");
            EmitLine("");
        }

        public void EmitContentHeaderReader()
        {
            EmitLine("    internal override Client.Impl.ContentHeaderBase DecodeContentHeaderFrom(ushort classId)");
            EmitLine("    {");
            EmitLine("      switch (classId)");
            EmitLine("      {");
            foreach (AmqpClass c in m_classes)
            {
                if (c.NeedsProperties)
                {
                    EmitLine($"        case {c.Index}: return new {MangleClass(c.Name)}Properties();");
                }
            }
            EmitLine("        default: throw new Client.Exceptions.UnknownClassOrMethodException(classId, 0);");
            EmitLine("      }");
            EmitLine("    }");
        }

        public Attribute Attribute(MemberInfo mi, Type t)
        {
            return Attribute(mi.GetCustomAttributes(t, false), t);
        }

        public Attribute Attribute(ParameterInfo pi, Type t)
        {
            return Attribute(pi.GetCustomAttributes(t, false), t);
        }

        public Attribute Attribute(ICustomAttributeProvider p, Type t)
        {
            return Attribute(p.GetCustomAttributes(t, false), t);
        }

        public Attribute Attribute(IEnumerable<object> attributes, Type t)
        {
            TypeInfo typeInfo = t.GetTypeInfo();

            if (typeInfo.IsSubclassOf(typeof(AmqpApigenAttribute)))
            {
                AmqpApigenAttribute result = null;
                foreach (AmqpApigenAttribute candidate in attributes)
                {
                    if (candidate.m_namespaceName == null && result == null)
                    {
                        result = candidate;
                    }
                    if (candidate.m_namespaceName == ApiNamespaceBase)
                    {
                        result = candidate;
                    }
                }
                return result;
            }
            else
            {
                foreach (Attribute attribute in attributes)
                {
                    return attribute;
                }
                return null;
            }
        }

        public void EmitModelImplementation()
        {
            EmitLine("  internal class Model: Client.Impl.ModelBase {");
            EmitLine("    public Model(Client.Impl.ISession session): base(session) {}");
            EmitLine("    public Model(Client.Impl.ISession session, ConsumerWorkService workService): base(session, workService) {}");
            IList<MethodInfo> asynchronousHandlers = new List<MethodInfo>();
            foreach (Type t in m_modelTypes)
            {
                foreach (MethodInfo method in t.GetMethods())
                {
                    if (method.DeclaringType.Namespace != null &&
                        method.DeclaringType.Namespace.StartsWith("RabbitMQ.Client"))
                    {
                        if (method.Name.StartsWith("Handle") ||
                            (Attribute(method, typeof(AmqpAsynchronousHandlerAttribute)) != null))
                        {
                            if ((Attribute(method, typeof(AmqpMethodDoNotImplementAttribute)) == null) && Attribute(method, typeof(AmqpUnsupportedAttribute)) == null)
                            {
                                asynchronousHandlers.Add(method);
                            }
                        }
                        else
                        {
                            MaybeEmitModelMethod(method);
                        }
                    }
                }
            }
            EmitAsynchronousHandlers(asynchronousHandlers);
            EmitLine("  }");
        }

        public void EmitContentHeaderFactory(MethodInfo method)
        {
            AmqpContentHeaderFactoryAttribute factoryAnnotation = (AmqpContentHeaderFactoryAttribute)
                Attribute(method, typeof(AmqpContentHeaderFactoryAttribute));
            string contentClass = factoryAnnotation.m_contentClass;
            EmitModelMethodPreamble(method);
            EmitLine("    {");
            if (Attribute(method, typeof(AmqpUnsupportedAttribute)) != null)
            {
                EmitLine($"      throw new UnsupportedMethodException(\"{method.Name}\");");
            }
            else
            {
                EmitLine($"      return new {MangleClass(contentClass)}Properties();");
            }
            EmitLine("    }");
        }

        public void MaybeEmitModelMethod(MethodInfo method)
        {
            if (method.IsSpecialName)
            {
                // It's some kind of event- or property-related method.
                // It shouldn't be autogenerated.
            }
            else if (method.Name.EndsWith("NoWait"))
            {
                // Skip *Nowait versions
            }
            else if (Attribute(method, typeof(AmqpMethodDoNotImplementAttribute)) != null)
            {
                // Skip this method, by request (AmqpMethodDoNotImplement)
            }
            else if (Attribute(method, typeof(AmqpContentHeaderFactoryAttribute)) != null)
            {
                EmitContentHeaderFactory(method);
            }
            else if (Attribute(method, typeof(AmqpUnsupportedAttribute)) != null)
            {
                EmitModelMethodPreamble(method);
                EmitLine("    {");
                EmitLine($"      throw new UnsupportedMethodException(\"{method.Name}\");");
                EmitLine("    }");
            }
            else
            {
                EmitModelMethod(method);
            }
        }

        public string SanitisedFullName(Type t)
        {
            if (t.Equals(typeof(IDictionary<string, object>)))
            {
                return "IDictionary<string, object>";
            }
            if (t.Equals(typeof(ReadOnlyMemory<byte>)))
            {
                return "ReadOnlyMemory<byte>";
            }

            switch (t.FullName)
            {
                case "System.Boolean":
                    return "bool";
                case "System.Byte[]":
                    return "byte[]";
                case "System.String":
                    return "string";
                case "System.UInt16":
                    return "ushort";
                case "System.UInt32":
                    return "uint";
                case "System.UInt64":
                    return "ulong";
                case "System.Void":
                    return "void";
                default:
                    return t.FullName;
            };
        }

        public void EmitModelMethodPreamble(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            EmitLine($"    public override {SanitisedFullName(method.ReturnType)} {method.Name} ({string.Join(", ", parameters.Select(pi => $"{SanitisedFullName(pi.ParameterType)} @{pi.Name}"))})");
        }

        public void LookupAmqpMethod(MethodInfo method,
                                     string methodName,
                                     out AmqpClass amqpClass,
                                     out AmqpMethod amqpMethod)
        {
            amqpClass = null;
            amqpMethod = null;

            // First, try autodetecting the class/method via the
            // IModel method name.

            foreach (AmqpClass c in m_classes)
            {
                foreach (AmqpMethod m in c.m_Methods)
                {
                    if (methodName.Equals(MangleMethodClass(c, m)))
                    {
                        amqpClass = c;
                        amqpMethod = m;
                        goto stopSearching; // wheee
                    }
                }
            }
        stopSearching:

            // If an explicit mapping was provided as an attribute,
            // then use that instead, whether the autodetect worked or
            // not.

            {
                if (Attribute(method, typeof(AmqpMethodMappingAttribute)) is AmqpMethodMappingAttribute methodMapping)
                {
                    amqpClass = null;
                    foreach (AmqpClass c in m_classes)
                    {
                        if (c.Name == methodMapping.m_className)
                        {
                            amqpClass = c;
                            break;
                        }
                    }
                    amqpMethod = amqpClass.MethodNamed(methodMapping.m_methodName);
                }
            }

            // At this point, if can't find either the class or the
            // method, we can't proceed. Complain.

            if (amqpClass == null || amqpMethod == null)
            {
                throw new Exception($"Could not find AMQP class or method for IModel method {method.Name}");
            }
        }

        public void EmitModelMethod(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();

            LookupAmqpMethod(method, method.Name, out AmqpClass amqpClass, out AmqpMethod amqpMethod);

            string requestImplClass = MangleMethodClass(amqpClass, amqpMethod);

            // At this point, we know which request method to
            // send. Now compute whether it's an RPC or not.

            AmqpMethod amqpReplyMethod = null;
            AmqpMethodMappingAttribute replyMapping =
                Attribute(method.ReturnTypeCustomAttributes, typeof(AmqpMethodMappingAttribute))
                as AmqpMethodMappingAttribute;
            if (Attribute(method, typeof(AmqpForceOneWayAttribute)) == null &&
                (amqpMethod.IsSimpleRpcRequest || replyMapping != null))
            {
                // We're not forcing oneway, and either are a simple
                // RPC request, or have an explicit replyMapping
                amqpReplyMethod = amqpClass.MethodNamed(replyMapping == null
                                                        ? amqpMethod.m_ResponseMethods[0]
                                                        : replyMapping.m_methodName);
                if (amqpReplyMethod == null)
                {
                    throw new Exception($"Could not find AMQP reply method for IModel method {method.Name}");
                }
            }

            // If amqpReplyMethod is null at this point, it's a
            // one-way operation, and no continuation needs to be
            // consed up. Otherwise, we should expect a reply of kind
            // identified by amqpReplyMethod - unless there's a nowait
            // parameter thrown into the equation!
            //
            // Examine the parameters to discover which might be
            // nowait, content header or content body.

            ParameterInfo nowaitParameter = null;
            string nowaitExpression = "null";
            ParameterInfo contentHeaderParameter = null;
            ParameterInfo contentBodyParameter = null;
            foreach (ParameterInfo pi in parameters)
            {
                if (Attribute(pi, typeof(AmqpNowaitArgumentAttribute)) is AmqpNowaitArgumentAttribute nwAttr)
                {
                    nowaitParameter = pi;
                    if (nwAttr.m_replacementExpression != null)
                    {
                        nowaitExpression = nwAttr.m_replacementExpression;
                    }
                }
                if (Attribute(pi, typeof(AmqpContentHeaderMappingAttribute)) != null)
                {
                    contentHeaderParameter = pi;
                }
                if (Attribute(pi, typeof(AmqpContentBodyMappingAttribute)) != null)
                {
                    contentBodyParameter = pi;
                }
            }

            // Compute expression text for the content header and body.

            string contentHeaderExpr =
                contentHeaderParameter == null
                ? "null"
                : $" ({MangleClass(amqpClass.Name)}Properties) {contentHeaderParameter.Name}";
            string contentBodyExpr =
                contentBodyParameter == null ? "null" : contentBodyParameter.Name;

            // Emit the method declaration and preamble.

            EmitModelMethodPreamble(method);
            EmitLine("    {");

            // Emit the code to build the request.
            if (parameters.Length > 0)
            {
                foreach (ParameterInfo unsupportedParameter in parameters.Where(x => x.Attribute<AmqpUnsupportedAttribute>() != null))
                {
                    EmitLine($"      if (@{unsupportedParameter.Name} != null)");
                    EmitLine("      {");
                    EmitLine($"        throw new UnsupportedMethodFieldException(\"{method.Name}\", \"{unsupportedParameter.Name}\");");
                    EmitLine("      }");
                }
                EmitLine($"      {requestImplClass} __req = new {requestImplClass}()");
                EmitLine("      {");
                foreach (ParameterInfo pi in parameters)
                {
                    if (pi != contentHeaderParameter && pi != contentBodyParameter)
                    {
                        if (pi.Attribute<AmqpFieldMappingAttribute>() is AmqpFieldMappingAttribute fieldMapping)
                        {
                            EmitLine($"        _{fieldMapping.m_fieldName} = @{pi.Name},");
                        }
                        else
                        {
                            EmitLine($"        _{pi.Name} = @{pi.Name},");
                        }
                    }
                }
                EmitLine("      };");
            }
            else
            {
                EmitLine($"      {requestImplClass} __req = new {requestImplClass}();");
            }

            // If we have a nowait parameter, sometimes that can turn
            // a ModelRpc call into a ModelSend call.

            if (nowaitParameter != null)
            {
                EmitLine($"      if ({nowaitParameter.Name}) {{");
                EmitLine($"        ModelSend(__req,{contentHeaderExpr},{contentBodyExpr});");
                if (method.ReturnType == typeof(void))
                {
                    EmitLine("        return;");
                }
                else
                {
                    EmitLine($"        return {nowaitExpression};");
                }
                EmitLine("      }");
            }

            // At this point, perform either a ModelRpc or a
            // ModelSend.

            if (amqpReplyMethod == null)
            {
                EmitLine($"      ModelSend(__req,{contentHeaderExpr},{contentBodyExpr});");
            }
            else
            {
                string replyImplClass = MangleMethodClass(amqpClass, amqpReplyMethod);

                EmitLine($"      Client.Impl.MethodBase __repBase = ModelRpc(__req, {contentHeaderExpr}, {contentBodyExpr});");
                EmitLine($"      if (!(__repBase is {replyImplClass}{(method.ReturnType == typeof(void) ? "" : " __rep")}))");
                EmitLine($"      {{");
                EmitLine($"        throw new UnexpectedMethodException(__repBase);");
                EmitLine($"      }}");

                if (method.ReturnType == typeof(void))
                {
                    // No need to further examine the reply.
                }
                else
                {
                    // At this point, we have the reply method. Extract values from it.
                    if (!(Attribute(method.ReturnTypeCustomAttributes, typeof(AmqpFieldMappingAttribute)) is AmqpFieldMappingAttribute returnMapping))
                    {
                        string fieldPrefix = IsAmqpClass(method.ReturnType) ? "_" : "";

                        // No field mapping --> it's assumed to be a struct to fill in.
                        EmitLine($"      {method.ReturnType} __result = new {method.ReturnType}();");
                        foreach (FieldInfo fi in method.ReturnType.GetFields())
                        {
                            if (Attribute(fi, typeof(AmqpFieldMappingAttribute)) is AmqpFieldMappingAttribute returnFieldMapping)
                            {
                                EmitLine($"      __result.{fi.Name} = __rep.{fieldPrefix}{returnFieldMapping.m_fieldName};");
                            }
                            else
                            {
                                EmitLine($"      __result.{fi.Name} = __rep.{fieldPrefix}{fi.Name};");
                            }
                        }
                        EmitLine("      return __result;");
                    }
                    else
                    {
                        // Field mapping --> return just the field we're interested in.
                        EmitLine($"      return __rep._{returnMapping.m_fieldName};");
                    }
                }
            }

            // All the IO and result-extraction has been done. Emit
            // the method postamble.

            EmitLine("    }");
        }

        public void EmitAsynchronousHandlers(IList<MethodInfo> asynchronousHandlers)
        {
            string GetParameterString(ParameterInfo pi)
            {
                if (Attribute(pi, typeof(AmqpContentHeaderMappingAttribute)) != null)
                {
                    return $"({pi.ParameterType}) cmd.Header";
                }
                else if (Attribute(pi, typeof(AmqpContentBodyMappingAttribute)) != null)
                {
                    return "cmd.Body";
                }
                else if (Attribute(pi, typeof(AmqpContentBodyArrayMappingAttribute)) != null)
                {
                    return "cmd.TakeoverPayload()";
                }
                else
                {
                    return $"__impl._{(!(Attribute(pi, typeof(AmqpFieldMappingAttribute)) is AmqpFieldMappingAttribute fieldMapping) ? pi.Name : fieldMapping.m_fieldName)}";
                }

                throw new NotImplementedException();
            }

            EmitLine("    public override bool DispatchAsynchronous(in IncomingCommand cmd) {");
            EmitLine("      switch ((cmd.Method.ProtocolClassId << 16) | cmd.Method.ProtocolMethodId)");
            EmitLine("      {");
            foreach (MethodInfo method in asynchronousHandlers)
            {
                string methodName = method.Name;
                if (methodName.StartsWith("Handle"))
                {
                    methodName = methodName.Substring(6);
                }

                LookupAmqpMethod(method, methodName, out AmqpClass amqpClass, out AmqpMethod amqpMethod);

                string implClass = MangleMethodClass(amqpClass, amqpMethod);

                EmitLine($"        case (ClassConstants.{MangleConstant(amqpClass.Name)} << 16) | {MangleClass(amqpClass.Name)}MethodConstants.{MangleConstant(amqpMethod.Name)}:");
                EmitLine("        {");
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length > 0)
                {
                    EmitLine($"          var __impl = ({implClass})cmd.Method;");
                    EmitLine($"          {method.Name}({string.Join(", ", parameters.Select(GetParameterString))});");
                }
                else
                {
                    EmitLine($"          {method.Name}();");
                }
                EmitLine("          return true;");
                EmitLine("        }");
            }
            EmitLine("        default: return false;");
            EmitLine("      }");
            EmitLine("    }");
        }
    }
}
