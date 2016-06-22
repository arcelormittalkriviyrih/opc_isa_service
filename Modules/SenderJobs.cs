using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Principal;
using System.Reflection;
using CommonEventSender;
using Opc.Ua;
using Opc.Ua.Client;
using JobOrdersService;
using JobPropsService;

namespace KEPServerSenderService
{
    /// <summary>
    /// Class for the management of processing of input queue on sending of commands
    /// </summary>
    public class SenderJobs
    {
        #region Const

        private const string cServiceTitle = "Сервис отправки команд в KEP server";
        /// <summary>
        /// The name of the system event source used by this service.
        /// </summary>
        private const string cSystemEventSourceName = "ArcelorMittal.KEPSSenderService.EventSource";

        /// <summary>
        /// The name of the system event log used by this service.
        /// </summary>
        private const string cSystemEventLogName = "KEPSSenderService.ArcelorMittal.Log";

        /// <summary>
        /// The name of the configuration parameter for the send commands frequency in seconds.
        /// </summary>
        private const string cSendCommandFrequencyName = "SendCommandFrequency";

        /// <summary>
        /// The name of the configuration parameter for the Odata service url.
        /// </summary>
        private const string cOdataService = "OdataServiceUri";

        /// <summary>
        /// The name of the configuration parameter for the OPC server url.
        /// </summary>
        private const string cOPCServerUrl = "OPCServerUrl";

        #endregion

        #region Fields

        /// <summary>
        /// Time interval for checking send commands
        /// </summary>
        private System.Timers.Timer m_SenderTimer;

        private KEPSenderServiceProductInfo wmiProductInfo;
        private bool fJobStarted = false;
        private string OdataServiceUrl;
        private string OPCServerUrl = "opc.tcp://127.0.0.1:49320";
        #endregion

        #region vpEventLog

        /// <summary>
        /// The value of the vpEventLog property.
        /// </summary>
        private EventLog m_EventLog;

        /// <summary>
        /// Gets the event log which is used by the service.
        /// </summary>
        public EventLog vpEventLog
        {
            get
            {
                lock (this)
                {
                    if (m_EventLog == null)
                    {
                        string lSystemEventLogName = cSystemEventLogName;
                        m_EventLog = new EventLog();
                        if (!System.Diagnostics.EventLog.SourceExists(cSystemEventSourceName))
                        {
                            System.Diagnostics.EventLog.CreateEventSource(cSystemEventSourceName, lSystemEventLogName);
                        }
                        else
                        {
                            lSystemEventLogName = EventLog.LogNameFromSourceName(cSystemEventSourceName, ".");
                        }
                        m_EventLog.Source = cSystemEventSourceName;
                        m_EventLog.Log = lSystemEventLogName;

                        WindowsIdentity identity = WindowsIdentity.GetCurrent();
                        WindowsPrincipal principal = new WindowsPrincipal(identity);
                        if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                        {
                            m_EventLog.ModifyOverflowPolicy(OverflowAction.OverwriteAsNeeded, 7);
                        }
                    }
                    return m_EventLog;
                }
            }
        }

        #endregion

        /// <summary>
        /// Status of processing of queue
        /// </summary>
        public bool JobStarted
        {
            get
            {
                return fJobStarted;
            }
        }

        #region Constructor

        /// <summary>	Default constructor. </summary>
        public SenderJobs()
        {
            // Set up a timer to trigger every send command frequency.
            int sendCommandFrequencyInSeconds = int.Parse(System.Configuration.ConfigurationManager.AppSettings[cSendCommandFrequencyName]);
            OdataServiceUrl = System.Configuration.ConfigurationManager.AppSettings[cOdataService];
            OPCServerUrl = System.Configuration.ConfigurationManager.AppSettings[cOPCServerUrl];

            wmiProductInfo = new KEPSenderServiceProductInfo(cServiceTitle,
                                                             Environment.MachineName,
                                                             Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                                                             DateTime.Now,
                                                             sendCommandFrequencyInSeconds,
                                                             OdataServiceUrl);

            m_SenderTimer = new System.Timers.Timer();
            m_SenderTimer.Interval = sendCommandFrequencyInSeconds * 1000; // seconds to milliseconds
            m_SenderTimer.Elapsed += new System.Timers.ElapsedEventHandler(this.OnSenderTimer);

            SenderMonitorEvent.sendMonitorEvent(vpEventLog, string.Format("Send Command Frequncy = {0}", sendCommandFrequencyInSeconds), EventLogEntryType.Information);
        }

        #endregion

        #region Destructor

        /// <summary>
        /// Constructor that prevents a default instance of this class from being created.
        /// </summary>
        ~ SenderJobs()
        {
            if (m_EventLog != null)
            {
                m_EventLog.Close();
                m_EventLog.Dispose();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Start of processing of input queue
        /// </summary>
        public void StartJob()
        {
            SenderMonitorEvent.sendMonitorEvent(vpEventLog, "Starting send command service...", EventLogEntryType.Information);

            m_SenderTimer.Start();

            SenderMonitorEvent.sendMonitorEvent(vpEventLog, "Send command service has been started", EventLogEntryType.Information);
            fJobStarted = true;
        }

        /// <summary>
        /// Stop of processing of input queue
        /// </summary>
        public void StopJob()
        {
            SenderMonitorEvent.sendMonitorEvent(vpEventLog, "Stopping send command service...", EventLogEntryType.Information);

            //stop timers if working
            if (m_SenderTimer.Enabled)
                m_SenderTimer.Stop();

            SenderMonitorEvent.sendMonitorEvent(vpEventLog, "Send command service has been stopped", EventLogEntryType.Information);
            fJobStarted = false;
        }

        /// <summary>
        /// Processing of input queue
        /// </summary>
        public void OnSenderTimer(object sender, System.Timers.ElapsedEventArgs args)
        {
            SenderMonitorEvent.sendMonitorEvent(vpEventLog, "Monitoring the send command activity", EventLogEntryType.Information);
            m_SenderTimer.Stop();

			string lLastError = string.Empty;
            KEPSSenderdbData senderDbData = new KEPSSenderdbData(OdataServiceUrl);
            List<SenderJobProps> JobData = senderDbData.fillSenderJobData();
            try
            {
                string sendState;
                // Step 1 -- Connect to UA server
                //string discoveryUrl = "opc.tcp://127.0.0.1:49320";
                using (Session AMSession = ClientUtils.CreateSession(OPCServerUrl, "ArcelorMittal.UA.SenderCommand"))
                {
                    foreach (SenderJobProps job in JobData)
                    {
                        if (WriteToKEPServer(AMSession, job))
                        {
                            sendState = "Done";
                            wmiProductInfo.LastActivityTime = DateTime.Now;
                        }
                        else
                        {
                            sendState = "Failed";
                        }
                        lLastError = String.Format("JobOrderID: {0}. Send to KEP Server element {1} = {2}. Status: {3}", job.JobOrderID, job.Command, job.CommandRule, sendState);

                        if (sendState == "Done")
                        {
                            Requests.updateJobStatus(OdataServiceUrl, job.JobOrderID, sendState);
                        }
                        else if (sendState == "Failed")
                        {
                            wmiProductInfo.LastServiceError = string.Format("{0}. On {1}", lLastError, DateTime.Now);
                        }
                    }
                    // Step 3 -- Clean up
                    AMSession.Close();
                }
            }
            catch (Exception ex)
            {
                lLastError = "Get data from DB. Error: " + ex.ToString();
                SenderMonitorEvent.sendMonitorEvent(vpEventLog, lLastError, EventLogEntryType.Error);
                wmiProductInfo.LastServiceError = string.Format("{0}. On {1}", lLastError, DateTime.Now);
            }
            wmiProductInfo.SendCommandsCount += JobData.Count;
            wmiProductInfo.PublishInfo();
            SenderMonitorEvent.sendMonitorEvent(vpEventLog, string.Format("Send command is done. {0} tasks", JobData.Count), EventLogEntryType.Information);

            m_SenderTimer.Start();
        }

        private object ConvertToObject(string elementValue)
        {
            object resultValue = null;
            int rightBracketPos = elementValue.IndexOf(')');

            if (rightBracketPos > 0)
            {
                string sTypeValue = elementValue.Substring(1, rightBracketPos - 1).ToUpper();
                string sValue = elementValue.Substring(rightBracketPos + 1);
                try
                {
                    switch (sTypeValue)
                    {
                        case "BOOLEAN":
                            resultValue = bool.Parse(sValue);
                            break;
                        case "DOUBLE":
                            resultValue = double.Parse(sValue);
                            break;
                        case "LONG":
                            resultValue = int.Parse(sValue);
                            break;
                        case "SHORT":
                            resultValue = short.Parse(sValue);
                            break;
                        case "WORD":
                            resultValue = ushort.Parse(sValue);
                            break;
                        case "DWORD":
                            resultValue = uint.Parse(sValue);
                            break;
                        case "FLOAT":
                            resultValue = float.Parse(sValue);
                            break;
                        case "BYTE":
                            resultValue = byte.Parse(sValue);
                            break;
                        case "CHAR":
                            resultValue = char.Parse(sValue);
                            break;
                        case "STRING":
                            resultValue = sValue;
                            break;
                        /*case "BCD":
                            resultValue = Parse(sValue);
                            break;
                        case "LBCD":
                            resultValue = bool.Parse(sValue);
                            break;
                        case "DATE":
                            resultValue = bool.Parse(sValue);
                            break;
                        case "LLONG":
                            resultValue = bool.Parse(sValue);
                            break;
                        case "QLONG":
                            resultValue = bool.Parse(sValue);
                            break;*/
                    }
                }
                catch
                { }
                /*if (!TypeDescriptor.GetConverter(typeof(Type)).IsValid(sTypeValue))
                {
                    typeValue = (ValueType)TypeDescriptor.GetConverter(typeof(ValueType)).ConvertFromString(sTypeValue);
                    if (typeValue is Byte)
                       resultValue = Byte. Parse(sValue);

                    else if value is Int16 ||
                    else if value is Int32 ||
                    else if value is Int64 ||
                    else if value is SByte ||
                    else if value is UInt16 ||
                    else if value is UInt32 ||
                    else if value is UInt64 ||
                    else if value is BigInteger ||
                    else if value is Decimal ||
                    else if value is Double ||
                    else if value is Single
                }*/
            }

            return resultValue;
        }

        private bool WriteToKEPServer(Session AMSession, SenderJobProps job)
        {
            // Step 2 -- Read the value of a node representing a PI Point data under the hood
            NodeId nodeToRead = new NodeId(job.Command/*Element*/, 2);
            Node node = AMSession.NodeCache.Find(nodeToRead) as Node;


            if (node != null)
            {
                DataValue value = AMSession.ReadValue(nodeToRead);
                WriteValue Wvalue = new WriteValue();
                Wvalue.NodeId = nodeToRead;
                Wvalue.AttributeId = Attributes.Value;

                if ((node.NodeClass & (NodeClass.Variable | NodeClass.VariableType)) == 0)
                {
                    Wvalue.AttributeId = Attributes.DisplayName;
                }

                Wvalue.IndexRange = null;
                Wvalue.Value = value;
                Wvalue.Value.Value = ConvertToObject(job.CommandRule);

                if (Wvalue.Value.Value != null)
                {
                    WriteValueCollection nodesToWrite = new WriteValueCollection();
                    nodesToWrite.Add(Wvalue);

                    StatusCodeCollection results = null;
                    DiagnosticInfoCollection diagnosticInfos = null;

                    ResponseHeader responseHeader = AMSession.Write(
                            null,
                            nodesToWrite,
                            out results,
                            out diagnosticInfos);

                    Session.ValidateResponse(results, nodesToWrite);
                    //Session.ValidateDiagnosticInfos(diagnosticInfos, nodesToWrite);

                    if (results[0].Code == 0)
                    {
                        return true;
                    }
                    else
                    {
                        SenderMonitorEvent.sendMonitorEvent(vpEventLog, String.Format("Can not send command to KEP Server. ErrorCode {0} ErrorText {1}", results[0].Code, results[0].ToString()), EventLogEntryType.Error);
                        return false;
                    }
                }
                else
                {
                    SenderMonitorEvent.sendMonitorEvent(vpEventLog, String.Format("Can not convert command value: {0}", job.CommandRule), EventLogEntryType.Error);
                    return false;
                }
            }
            else
            {
                Console.WriteLine("Item not found");
                return false;
            }
        }
        #endregion
    }
    /// <summary>
    /// Class of command properties of sender command service
    /// </summary>
    public class SenderJobProps : JobProps
    {
        public SenderJobProps(int cJobOrderID,
                              string cCommand,
                              string cCommandRule) : base (cJobOrderID,
                                                           cCommand,
                                                           cCommandRule)
        { }
    }
    /// <summary>
    /// Class for processing of input queue and generation of list of commands for KEP Server
    /// </summary>
    public class KEPSSenderdbData
    {
        private string webServiceUrl;

        /// <summary>	Constructor. </summary>
        ///
        /// <param name="webServiceUrl">	URL of the web service. </param>
        public KEPSSenderdbData(string webServiceUrl)
        {
            this.webServiceUrl = webServiceUrl;
        }

        /// <summary>
        /// Processing of input queue and generation of list of KEP Server commands
        /// </summary>
        public List<SenderJobProps> fillSenderJobData()
        {
            List<SenderJobProps> lSenderJobProps = new List<SenderJobProps>();

            JobOrders jobOrders = new JobOrders(webServiceUrl, "KEPCommands", "ToSend");
            foreach (JobOrders.JobOrdersValue joValue in jobOrders.JobOrdersObj)
            {
                lSenderJobProps.Add(new SenderJobProps(joValue.ID,
                                                       joValue.Command,
                                                       (string)(joValue.CommandRule)));
            }

            return lSenderJobProps;
        }
    }
}
