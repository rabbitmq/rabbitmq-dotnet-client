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

using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Represents a configurable SSL option, used in setting up an SSL connection.
    /// </summary>
    public class SslOption
    {
        private X509CertificateCollection _certificateCollection;

        /// <summary>
        /// Constructs an SslOption specifying both the server cannonical name and the client's certificate path.
        /// </summary>
        public SslOption(string serverName, string certificatePath = "", bool enabled = false)
        {
            Version = SslProtocols.Tls;
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
        public SslOption() : this(string.Empty)
        {
        }

        /// <summary>
        /// Retrieve or set the set of ssl policy errors that are deemed acceptable.
        /// </summary>
        public SslPolicyErrors AcceptablePolicyErrors { get; set; }

        /// <summary>
        /// Retrieve or set the path to client certificate.
        /// </summary>
        public string CertPassphrase { get; set; }

        /// <summary>
        /// Retrieve or set the path to client certificate.
        /// </summary>
        public string CertPath { get; set; }

        /// <summary>
        /// An optional client specified SSL certificate selection callback.  If this is not specified,
        /// the first valid certificate found will be used.
        /// </summary>
        public LocalCertificateSelectionCallback CertificateSelectionCallback { get; set; }

        /// <summary>
        /// An optional client specified SSL certificate validation callback.  If this is not specified,
        /// the default callback will be used in conjunction with the <see cref="AcceptablePolicyErrors"/> property to 
        /// determine if the remote server certificate is valid.
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
        /// Flag specifying if Ssl should indeed be used.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Retrieve or set server's Canonical Name.
        /// This MUST match the CN on the Certificate else the SSL connection will fail.
        /// </summary>
        public string ServerName { get; set; }

        /// <summary>
        /// Retrieve or set the Ssl protocol version.
        /// </summary>
        public SslProtocols Version { get; set; }
    }
}