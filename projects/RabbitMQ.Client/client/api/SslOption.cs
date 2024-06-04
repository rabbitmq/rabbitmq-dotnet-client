// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------


using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Represents a set of configurable TLS options for a connection. Use this class to configure
    /// TLS version used, client certificate list or file location, peer certificate verification
    /// (validation) functions, expected server name (Subject Alternative Name or Common Name),
    /// and so on.
    /// </summary>
    public class SslOption
    {
        private X509CertificateCollection _certificateCollection;

        /// <summary>
        /// Constructs an <see cref="SslOption"/> specifying both the server canonical name and the client's certificate path.
        /// </summary>
        public SslOption(string serverName, string certificatePath = "", bool enabled = false)
        {
            Version = SslProtocols.None;
            AcceptablePolicyErrors = SslPolicyErrors.None;
            ServerName = serverName;
            CertPath = certificatePath;
            Enabled = enabled;
            CertificateValidationCallback = null;
            CertificateSelectionCallback = null;
        }

        /// <summary>
        /// Constructs an <see cref="SslOption"/> with no parameters set.
        /// </summary>
        public SslOption()
            : this(string.Empty)
        {
        }

        /// <summary>
        /// Retrieve or set the set of TLS policy (peer verification) errors that are deemed acceptable.
        /// </summary>
        public SslPolicyErrors AcceptablePolicyErrors { get; set; }

        /// <summary>
        /// Retrieve or set the client certificate passphrase.
        /// </summary>
        public string CertPassphrase { get; set; }

        /// <summary>
        /// Retrieve or set the path to client certificate.
        /// </summary>
        public string CertPath { get; set; }

        /// <summary>
        /// An optional client TLS certificate selection callback. If this is not specified,
        /// the first valid certificate found will be used.
        /// </summary>
        public LocalCertificateSelectionCallback CertificateSelectionCallback { get; set; }

        /// <summary>
        /// An optional peer verification (TLS certificate validation) callback. If this is not specified,
        /// the default callback will be used in conjunction with the <see cref="AcceptablePolicyErrors"/> property to
        /// determine if the peer's (server's) certificate should be considered valid (acceptable).
        /// </summary>
        public RemoteCertificateValidationCallback CertificateValidationCallback { get; set; }

        /// <summary>
        /// Retrieve or set the X509CertificateCollection containing the client certificate.
        /// If no collection is set, the client will attempt to load one from the specified <see cref="CertPath"/>.
        /// </summary>
        public X509CertificateCollection Certs
        {
            get
            {
                if (_certificateCollection != null)
                {
                    return _certificateCollection;
                }
                if (string.IsNullOrEmpty(CertPath))
                {
                    return null;
                }
                var collection = new X509CertificateCollection
                {
                    new X509Certificate2(CertPath, CertPassphrase)
                };
                return collection;
            }
            set { _certificateCollection = value; }
        }

        /// <summary>
        /// Attempts to check certificate revocation status. Default is false.
        /// Set to true to check peer certificate for revocation.
        /// </summary>
        /// <remarks>
        /// Uses the built-in .NET TLS implementation machinery for checking a certificate against
        /// certificate revocation lists.
        /// </remarks>
        public bool CheckCertificateRevocation { get; set; }

        /// <summary>
        /// Controls if TLS should indeed be used. Set to false to disable TLS
        /// on the connection.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Retrieve or set server's expected name.
        /// This MUST match the Subject Alternative Name (SAN) or CN on the peer's (server's) leaf certificate,
        /// otherwise the TLS connection will fail.
        /// </summary>
        public string ServerName { get; set; }

        /// <summary>
        /// Retrieve or set the TLS protocol version.
        /// The client will let the OS pick a suitable version by using <see cref="SslProtocols.None" />.
        /// If this option is disabled, e.g.see via app context, the client will attempt to fall back
        /// to TLSv1.2.
        /// </summary>
        /// <seealso cref="SslProtocols" />
        /// <seealso href="https://www.rabbitmq.com/ssl.html#dotnet-client" />
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/framework/network-programming/tls?view=netframework-4.6.2" />
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/api/system.security.authentication.sslprotocols?view=netframework-4.8" />
        public SslProtocols Version { get; set; }

        /// <summary>
        /// Reconfigures the instance to enable/use TLSv1.2.
        /// Only used in environments where System.Security.Authentication.SslProtocols.None
        /// is unavailable or effectively disabled, as reported by System.Net.ServicePointManager.
        /// </summary>
        internal SslProtocols UseFallbackTlsVersions()
        {
            Version = SslProtocols.Tls12;
            return Version;
        }
    }
}
