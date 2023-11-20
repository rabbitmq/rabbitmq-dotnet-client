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

using System;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace RabbitMQ.Client.Impl
{
    /// <summary>
    /// Represents an <see cref="SslHelper"/> which does the actual heavy lifting to set up an SSL connection,
    ///  using the config options in an <see cref="SslOption"/> to make things cleaner.
    /// </summary>
    internal class SslHelper
    {
        private readonly SslOption _sslOption;

        private SslHelper(SslOption sslOption)
        {
            _sslOption = sslOption;
        }

        /// <summary>
        /// Upgrade a Tcp stream to an Ssl stream using the TLS options provided.
        /// </summary>
        // TODO async
        public static Stream TcpUpgrade(Stream tcpStream, SslOption options)
        {
            var helper = new SslHelper(options);

            RemoteCertificateValidationCallback remoteCertValidator =
                options.CertificateValidationCallback ?? helper.CertificateValidationCallback;
            LocalCertificateSelectionCallback localCertSelector =
                options.CertificateSelectionCallback ?? helper.CertificateSelectionCallback;

            var sslStream = new SslStream(tcpStream, false, remoteCertValidator, localCertSelector);

            Action<SslOption> TryAuthenticating = (SslOption opts) =>
            {
                sslStream.AuthenticateAsClientAsync(opts.ServerName, opts.Certs, opts.Version,
                    opts.CheckCertificateRevocation).EnsureCompleted();
            };

            try
            {
                // TODO async
                TryAuthenticating(options);
            }
            catch (ArgumentException e) when (e.ParamName == "sslProtocolType" && options.Version == SslProtocols.None)
            {
                // SslProtocols.None is dysfunctional in this environment, possibly due to TLS version restrictions
                // in the app context, system or .NET version-specific behavior. See rabbitmq/rabbitmq-dotnet-client#764
                // for background.
                options.UseFallbackTlsVersions();
                TryAuthenticating(options);
            }

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
