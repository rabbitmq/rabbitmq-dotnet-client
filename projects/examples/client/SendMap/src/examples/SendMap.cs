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
using System.Collections;
using System.Net;
using System.Text;

using RabbitMQ.Client;
using RabbitMQ.Client.Content;

namespace RabbitMQ.Client.Examples {
    public class SendMap {
        public static int Main(string[] args) {
            bool persistMode = false;

            int optionIndex = 0;
            while (optionIndex < args.Length) {
                if (args[optionIndex] == "/persist") {
                    persistMode = true;
                } else {
                    break;
                }
                optionIndex++;
            }

            if (((args.Length - optionIndex) < 1) ||
                (((args.Length - optionIndex - 1) % 2) == 1))
                {
                    Console.Error.WriteLine("Usage: SendMap [<option> ...] <exchange-uri> [[<key> <value>] ...]");
                    Console.Error.WriteLine("RabbitMQ .NET client version "+typeof(IModel).Assembly.GetName().Version.ToString());
                    Console.Error.WriteLine("Exchange-URI: amqp://host[:port]/exchange[/routingkey][?type=exchangetype]");
                    Console.Error.WriteLine("Keys must start with '+' or with '-'. Those starting with '+' are placed in");
                    Console.Error.WriteLine("the body of the message, and those starting with '-' are placed in the headers.");
                    Console.Error.WriteLine("Values must start with a single character typecode and a colon.");
                    Console.Error.WriteLine("Supported typecodes are:");
                    Console.Error.WriteLine(" S - string/byte array");
                    Console.Error.WriteLine(" x - byte array (base-64)");
                    Console.Error.WriteLine(" t - boolean");
                    Console.Error.WriteLine(" i - 32-bit integer");
                    Console.Error.WriteLine(" d - double-precision float");
                    Console.Error.WriteLine(" D - fixed-point decimal");
                    Console.Error.WriteLine("Note that some types are valid only in the body of a message, and some are");
                    Console.Error.WriteLine("valid only in the headers.");
                    Console.Error.WriteLine("The exchange \"amq.default\" is an alias for the default (\"\") AMQP exchange,");
                    Console.Error.WriteLine("introduced so that the default exchange can be addressed via URI syntax.");
                    Console.Error.WriteLine("Available options:");
                    Console.Error.WriteLine("  /persist     send message in 'persistent' mode");
                    return 2;
                }
                
            Uri uri = new Uri(args[optionIndex++]);
            string exchange = uri.Segments[1].TrimEnd(new char[] { '/' });
            string exchangeType = uri.Query.StartsWith("?type=") ? uri.Query.Substring(6) : null;
            string routingKey = uri.Segments.Length > 2 ? uri.Segments[2] : "";

            if (exchange == "amq.default") {
                exchange = "";
            }

            ConnectionFactory cf = new ConnectionFactory();
            cf.Endpoint = new AmqpTcpEndpoint(uri);

            using (IConnection conn = cf.CreateConnection())
                {
                    using (IModel ch = conn.CreateModel()) {

                        if (exchangeType != null) {
                            ch.ExchangeDeclare(exchange, exchangeType);
                        }

                        IMapMessageBuilder b = new MapMessageBuilder(ch);
                        while ((optionIndex + 1) < args.Length) {
                            string keyAndDiscriminator = args[optionIndex++];
                            string valueAndType = args[optionIndex++];

                            if (keyAndDiscriminator.Length < 1) {
                                Console.Error.WriteLine("Invalid key: '{0}'", keyAndDiscriminator);
                                return 2;
                            }
                            string key = keyAndDiscriminator.Substring(1);
                            char discriminator = keyAndDiscriminator[0];

                            IDictionary target;
                            switch (discriminator) {
                              case '-':
                                  target = b.Headers;
                                  break;
                              case '+':
                                  target = b.Body;
                                  break;
                              default:
                                  Console.Error.WriteLine("Invalid key: '{0}'",
                                                          keyAndDiscriminator);
                                  return 2;
                            }

                            if (valueAndType.Length < 2 || valueAndType[1] != ':') {
                                Console.Error.WriteLine("Invalid value: '{0}'", valueAndType);
                                return 2;
                            }
                            string valueStr = valueAndType.Substring(2);
                            char typeCode = valueAndType[0];

                            object value;
                            switch (typeCode) {
                              case 'S':
                                  value = valueStr;
                                  break;
                              case 'x':
                                  value = new BinaryTableValue(Convert.FromBase64String(valueStr));
                                  break;
                              case 't':
                                  value = (valueStr.ToLower() == "true" ||
                                           valueStr.ToLower() == "yes" ||
                                           valueStr.ToLower() == "on" ||
                                           valueStr == "1");
                                  break;
                              case 'i':
                                  value = int.Parse(valueStr);
                                  break;
                              case 'd':
                                  value = double.Parse(valueStr);
                                  break;
                              case 'D':
                                  value = decimal.Parse(valueStr);
                                  break;
                              default:
                                  Console.Error.WriteLine("Invalid type code: '{0}'", typeCode);
                                  return 2;
                            }

                            target[key] = value;
                        }
                        if (persistMode) {
                            ((IBasicProperties) b.GetContentHeader()).DeliveryMode = 2;
                        }
                        ch.BasicPublish(exchange,
                                        routingKey,
                                        (IBasicProperties) b.GetContentHeader(),
                                        b.GetContentBody());
                        return 0;
                    }
                }
        }
    }
}
