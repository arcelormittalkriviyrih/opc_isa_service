using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography.X509Certificates;
//using CommonEventSender;
using Opc.Ua;
using Opc.Ua.Client;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace KEPServerSenderService
{
    /// <summary>
    /// Defines numerous re-useable utility functions.
    /// </summary>
    public static class ClientUtils
    {
        public static EventLog eventLog;
        /// <summary>
        /// Finds the endpoint that best matches the current settings.
        /// </summary>
        /// <param name="discoveryUrl">The discovery URL.</param>
        /// <param name="useSecurity">if set to <c>true</c> select an endpoint that uses security.</param>
        /// <returns>The best available endpoint.</returns>
        public static EndpointDescription SelectEndpoint(string discoveryUrl, bool useSecurity)
        {
            // needs to add the '/discovery' back onto non-UA TCP URLs.
            if (!discoveryUrl.StartsWith(Utils.UriSchemeOpcTcp))
            {
                if (!discoveryUrl.EndsWith("/discovery"))
                {
                    discoveryUrl += "/discovery";
                }
            }

            // parse the selected URL.
            Uri uri = new Uri(discoveryUrl);

            // set a short timeout because this is happening in the drop down event.
            EndpointConfiguration configuration = EndpointConfiguration.Create();
            configuration.OperationTimeout = 5000;

            EndpointDescription selectedEndpoint = null;

            // Connect to the server's discovery endpoint and find the available configuration.
            using (DiscoveryClient client = DiscoveryClient.Create(uri, configuration))
            {
                EndpointDescriptionCollection endpoints = client.GetEndpoints(null);

                // select the best endpoint to use based on the selected URL and the UseSecurity checkbox. 
                for (int ii = 0; ii < endpoints.Count; ii++)
                {
                    EndpointDescription endpoint = endpoints[ii];

                    // pick the first available endpoint by default.
                    if (selectedEndpoint == null)
                    {
                        selectedEndpoint = endpoint;
                    }

                    // check for a match on the URL scheme.
                    if (endpoint.EndpointUrl.StartsWith(uri.Scheme))
                    {
                        // check if security was requested.
                        if (useSecurity)
                        {
                            if (endpoint.SecurityMode != MessageSecurityMode.None)
                            {
                                // The security level is a relative measure assigned by the server to the 
                                // endpoints that it returns. Clients should always pick the highest level
                                // unless they have a reason not too.
                                if (endpoint.SecurityLevel > selectedEndpoint.SecurityLevel)
                                {
                                    selectedEndpoint = endpoint;
                                }
                            }
                        }

                        // look for an unsecured endpoint if requested.
                        else
                        {
                            if (endpoint.SecurityMode == MessageSecurityMode.None)
                            {
                                selectedEndpoint = endpoint;
                            }
                        }
                    }
                }
            }

            // return the selected endpoint.
            return selectedEndpoint;
        }

        /// <summary>
        /// Creates an application instance certificate if one does not already exist.
        /// </summary>
        public static void CheckApplicationInstanceCertificate(ApplicationConfiguration configuration)
        {
            // create a default certificate id none specified.
            CertificateIdentifier id = configuration.SecurityConfiguration.ApplicationCertificate;

            if (id == null)
            {
                id = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.X509Store,
                    StorePath = "LocalMachine\\My",
                    SubjectName = configuration.ApplicationName
                };
            }

            // check for certificate with a private key.
            X509Certificate2 certificate = id.Find(true).Result;

            if (certificate != null)
            {
                //This UA application already has an instance certificate
                SaveCertificate(certificate);
                return;
            }

            //This UA application does not have an instance certificate. Create one automatically

            // construct the subject name from the 
            List<string> hostNames = new List<string>
            {
                System.Net.Dns.GetHostName()
            };

            string commonName = Utils.Format("CN={0}", configuration.ApplicationName);
            string domainName = Utils.Format("DC={0}", hostNames[0]);
            string subjectName = Utils.Format("{0}, {1}", commonName, domainName);

            // check if a distinguished name was specified.
            if (id.SubjectName.IndexOf("=", StringComparison.Ordinal) != -1)
            {
                List<string> fields = Utils.ParseDistinguishedName(id.SubjectName);

                bool commonNameFound = false;
                bool domainNameFound = false;

                for (int ii = 0; ii < fields.Count; ii++)
                {
                    string field = fields[ii];

                    if (field.StartsWith("CN="))
                    {
                        fields[ii] = commonName;
                        commonNameFound = true;
                        continue;
                    }

                    if (field.StartsWith("DC="))
                    {
                        fields[ii] = domainName;
                        domainNameFound = true;
                        continue;
                    }
                }

                if (!commonNameFound)
                {
                    fields.Insert(0, commonName);
                }

                if (!domainNameFound)
                {
                    fields.Insert(0, domainName);
                }

                StringBuilder buffer = new StringBuilder();

                for (int ii = 0; ii < fields.Count; ii++)
                {
                    if (buffer.Length > 0)
                    {
                        buffer.Append(", ");
                    }

                    buffer.Append(fields[ii]);
                }

                subjectName = buffer.ToString();
            }

            // create a new certificate with a new public key pair.
            //certificate = CertificateFactory.CreateCertificate(
            //    id.StoreType,
            //    id.StorePath,
            //    configuration.ApplicationUri,
            //    configuration.ApplicationName,
            //    subjectName,
            //    hostNames,
            //    1024,
            //    120);

            ushort minimumKeySize = CertificateFactory.defaultKeySize;
            ushort lifeTimeInMonths = CertificateFactory.defaultLifeTime;

            certificate = CertificateFactory.CreateCertificate(
                id.StoreType,
                id.StorePath,
                null,
                configuration.ApplicationUri,
                configuration.ApplicationName,
                id.SubjectName,
                hostNames,
                minimumKeySize,
                DateTime.UtcNow - TimeSpan.FromDays(1),
                lifeTimeInMonths,
                CertificateFactory.defaultHashSize,
                false,
                null,
                null);

            // update and save the configuration file.
            id.Certificate = certificate;
            configuration.SaveToFile(configuration.SourceFilePath);

            // add certificate to the trusted peer store so other applications will trust it.
            ICertificateStore store = configuration.SecurityConfiguration.TrustedPeerCertificates.OpenStore();

            try
            {
                X509Certificate2 certificate2 = store.FindByThumbprint(certificate.Thumbprint).Result[0];

                if (certificate2 == null)
                {
                    store.Add(certificate);
                }
            }
            finally
            {
                store.Close();
            }

            // tell the certificate validator about the new certificate.
            configuration.CertificateValidator.Update(configuration.SecurityConfiguration);
            SaveCertificate(certificate);
        }

        static void SaveCertificate(X509Certificate2 certificate)
        {
            string CertificateFileName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\am_cert.cer";
            if (!File.Exists(CertificateFileName))
            {
                byte[] certFile = certificate.Export(X509ContentType.Cert);
                using (FileStream fs = new FileStream(CertificateFileName, FileMode.Create))
                {
                    fs.Write(certFile, 0, certFile.Length);
                    //fs.Close();
                }
            }
        }

        /// <summary>
        /// Handles an error validating the server certificate.
        /// </summary>
        /// <remarks>
        /// Applications should never accept certificates silently. Doing so will create the illusion of security
        /// that will come back to haunt the vendor in the future. Compliance tests by the OPC Foundation will
        /// fail products that silently accept untrusted certificates.
        /// </remarks>
        static void CertificateValidator_CertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
        {
            e.Accept = true;
            //SenderMonitorEvent.sendMonitorEvent(eventLog, String.Format("{0}. WARNING: Accepting Untrusted Certificate: {1}", e.Error, e.Certificate.Subject), EventLogEntryType.Warning);
            //Console.Error.WriteLine(e.Error);
            //Console.Error.WriteLine("WARNING: Accepting Untrusted Certificate: {0}", e.Certificate.Subject);
        }

        /// <summary>
        /// Create a session with the given UA server
        /// </summary>
        /// <param name="discoveryUrl"></param>
        /// <param name="configSection"></param>
        /// <returns></returns>
        public static Session CreateSession(string discoveryUrl, string configSection)
        {
            // Step 1 -- Load configuration
            var configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configSection + ".Config.xml");
            var configFileInfo = new FileInfo(configFilePath);
            ApplicationConfiguration configuration = ApplicationConfiguration.Load(configFileInfo, ApplicationType.Client, null).Result;
            //ApplicationConfiguration configuration = ApplicationConfiguration.Load(configSection, ApplicationType.Client).Result;
            ClientUtils.CheckApplicationInstanceCertificate(configuration);

            // Step 2 -- Select an endpoint
            // create the endpoint description
            EndpointDescription endpointDescription = ClientUtils.SelectEndpoint(discoveryUrl, false);
            endpointDescription.SecurityMode = MessageSecurityMode.None;
            endpointDescription.SecurityPolicyUri = @"http://opcfoundation.org/UA/SecurityPolicy#None";

            // create the endpoint configuration
            EndpointConfiguration endpointConfiguration = EndpointConfiguration.Create(configuration);

            // the default timeout for a requests sent using the channel.
            endpointConfiguration.OperationTimeout = 600000;
            // create the endpoint.
            ConfiguredEndpoint endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);

            // choose the encoding strategy: true: BinaryEncoding; false: XmlEncoding
            endpoint.Configuration.UseBinaryEncoding = true;

            //// create the binding factory.
            //BindingFactory bindingFactory = BindingFactory.Create(configuration, configuration.CreateMessageContext());

            //// update endpoint description using the discovery endpoint.
            //if (endpoint.UpdateBeforeConnect)
            //{
            //    endpoint.UpdateFromServer(bindingFactory);

            //    //Console.Error.WriteLine("Updated endpoint description for url: {0}\n", endpointDescription.EndpointUrl);

            //    endpointDescription = endpoint.Description;
            //    endpointConfiguration = endpoint.Configuration;
            //}

            X509Certificate2 clientCertificate = configuration.SecurityConfiguration.ApplicationCertificate.Find().Result;
            configuration.SecurityConfiguration.RejectSHA1SignedCertificates = false;
            ushort minimumKeySize = CertificateFactory.defaultKeySize / 2;
            configuration.SecurityConfiguration.MinimumCertificateKeySize = minimumKeySize;
            configuration.CertificateValidator.Update(configuration.SecurityConfiguration);

            // set up a callback to handle certificate validation errors.
            configuration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);

            // Step 3 -- Initialize the channel which will be created with the server.
            var channel = SessionChannel.Create(
                configuration,
                endpointDescription,
                endpointConfiguration,
                //bindingFactory,
                clientCertificate,
                new ServiceMessageContext());

            // Step 4 -- Create a session
            Session session = new Session(channel, configuration, endpoint, clientCertificate)
            {
                ReturnDiagnostics = DiagnosticsMasks.All
            };
            string sessionName = "SenderSession";
            UserIdentity identity = new UserIdentity(); // anonymous
            session.KeepAlive += new KeepAliveEventHandler(Session_KeepAlive);
            session.Open(sessionName, identity);
            return session;
        }

        /// <summary>
        /// Raised when a keep alive response is returned from the server.
        /// </summary>
        public static void Session_KeepAlive(Session session, KeepAliveEventArgs e)
        {
            Utils.Trace("===>>> Session KeepAlive: {0} ServerTime: {1:HH:MM:ss}", e.CurrentState, e.CurrentTime.ToLocalTime());
        }
    }
}
