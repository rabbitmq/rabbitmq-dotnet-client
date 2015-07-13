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
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Represents an <see cref="SslHelper"/> which does the actual heavy lifting to set up an SSL connection,
    ///  using the config options in an <see cref="SslOption"/> to make things cleaner.
    /// </summary>
    public class SslHelper
    {
        private readonly SslOption _sslOption;

        private SslHelper(SslOption sslOption)
        {
            _sslOption = sslOption;
        }

        /// <summary>
        /// Upgrade a Tcp stream to an Ssl stream using the SSL options provided.
        /// </summary>
        public static Stream TcpUpgrade(Stream tcpStream, SslOption sslOption)
        {
            var helper = new SslHelper(sslOption);

            RemoteCertificateValidationCallback remoteCertValidator =
                sslOption.CertificateValidationCallback ?? helper.CertificateValidationCallback;
            LocalCertificateSelectionCallback localCertSelector =
                sslOption.CertificateSelectionCallback ?? helper.CertificateSelectionCallback;

            var sslStream = new SslStream(tcpStream, false, remoteCertValidator, localCertSelector);

            sslStream.AuthenticateAsClient(sslOption.ServerName, sslOption.Certs, sslOption.Version, false);

            return sslStream;
        }

        private X509Certificate CertificateSelectionCallback(object sender, string targetHost,
            X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
        {
            if (acceptableIssuers != null && acceptableIssuers.Length > 0 &&
                localCertificates != null && localCertificates.Count > 0)
            {
                foreach (X509Certificate certificate in localCertificates)
                {
                    if (Array.IndexOf(acceptableIssuers, certificate.Issuer) != -1)
                    {
                        return certificate;
                    }
                }
            }
            if (localCertificates != null && localCertificates.Count > 0)
            {
                return localCertificates[0];
            }

            return null;
        }

        private bool CertificateValidationCallback(object sender, X509Certificate certificate,
            X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return (sslPolicyErrors & ~_sslOption.AcceptablePolicyErrors) == SslPolicyErrors.None;
        }
    }
}
