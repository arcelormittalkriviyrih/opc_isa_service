using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Reflection;
using System.Globalization;
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
    public class SenderJobs : IDisposable
    {
        #region Const

        private const string cServiceTitle = "Сервис отправки команд в KEP server";
        /// <summary>
        /// The name of the system event source used by this service.
        /// </summary>
        private const string cSystemEventSourceName = "AM.OPCCommandsSenderService.EventSource";

        /// <summary>
        /// The name of the system event log used by this service.
        /// </summary>
        private const string cSystemEventLogName = "AM.OPCCommandsSenderService.Log";

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
        private readonly string OdataServiceUrl;
        private readonly string OPCServerUrl = "opc.tcp://127.0.0.1:49320";
        #endregion

        #region vpEventLog

        /// <summary>
        /// The value of the vpEventLog property.
        /// </summary>
        private EventLog m_EventLog;

        /// <summary>
        /// Gets the event log which is used by the service.
        /// </summary>
        public EventLog EventLog
        {
            get
            {
                lock (this)
                {
                    if (m_EventLog == null)
                    {
                        string lSystemEventLogName = cSystemEventLogName;
                        m_EventLog = new EventLog();

                        if (!EventLog.SourceExists(cSystemEventSourceName))
                        {
                            EventLog.CreateEventSource(cSystemEventSourceName, lSystemEventLogName);
                        }
                        else
                        {
                            lSystemEventLogName = EventLog.LogNameFromSourceName(cSystemEventSourceName, ".");
                        }
                        m_EventLog.Source = cSystemEventSourceName;
                        m_EventLog.Log = lSystemEventLogName;
                        ClientUtils.eventLog = m_EventLog;

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
            SenderMonitorEvent.SendMonitorEvent(EventLog, string.Format("ODataServiceUrl = {0}", OdataServiceUrl), EventLogEntryType.Information, 1);
            SenderMonitorEvent.SendMonitorEvent(EventLog, string.Format("OPCServerUrl = {0}", OPCServerUrl), EventLogEntryType.Information, 1);

            try
            {
                wmiProductInfo = new KEPSenderServiceProductInfo(cServiceTitle,
                                                                 Environment.MachineName,
                                                                 Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                                                                 DateTime.Now,
                                                                 sendCommandFrequencyInSeconds,
                                                                 OdataServiceUrl);
            }
            catch// (Exception ex)
            {
                //SenderMonitorEvent.sendMonitorEvent(vpEventLog, string.Format("Failed to initialize WMI = {0}", ex.ToString()), EventLogEntryType.Error);
            }

            m_SenderTimer = new System.Timers.Timer
            {
                Interval = sendCommandFrequencyInSeconds * 1000 // seconds to milliseconds
            };
            m_SenderTimer.Elapsed += new System.Timers.ElapsedEventHandler(this.OnSenderTimer);

            SenderMonitorEvent.SendMonitorEvent(EventLog, string.Format("Send Command Frequncy = {0}", sendCommandFrequencyInSeconds), EventLogEntryType.Information, 1);
        }

        #endregion

        #region Destructor

        /// <summary>
        /// Constructor that prevents a default instance of this class from being created.
        /// </summary>
        //~SenderJobs()
        //{
        //    if (m_EventLog != null)
        //        m_EventLog.Dispose();

        //    if (m_SenderTimer != null)
        //        m_SenderTimer.Dispose();

        //    Dispose(false);
        //}

        #endregion

        #region Destructor

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if(m_EventLog != null)
                m_EventLog.Dispose();

                if(m_SenderTimer != null)
                    m_SenderTimer.Dispose();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Start of processing of input queue
        /// </summary>
        public void StartJob()
        {
            SenderMonitorEvent.SendMonitorEvent(EventLog, "Starting send command service...", EventLogEntryType.Information, 1);

            m_SenderTimer.Start();

            SenderMonitorEvent.SendMonitorEvent(EventLog, "Send command service has been started", EventLogEntryType.Information, 1);
            fJobStarted = true;
        }

        /// <summary>
        /// Stop of processing of input queue
        /// </summary>
        public void StopJob()
        {
            SenderMonitorEvent.SendMonitorEvent(EventLog, "Stopping send command service...", EventLogEntryType.Information, 2);

            //stop timers if working
            if (m_SenderTimer.Enabled)
                m_SenderTimer.Stop();

            SenderMonitorEvent.SendMonitorEvent(EventLog, "Send command service has been stopped", EventLogEntryType.Information, 2);
            fJobStarted = false;
        }

        /// <summary>
        /// Processing of input queue
        /// </summary>
        public void OnSenderTimer(object sender, System.Timers.ElapsedEventArgs args)
        {
            SenderMonitorEvent.SendMonitorEvent(EventLog, "Monitoring the send command activity", EventLogEntryType.Information, 3);
            m_SenderTimer.Stop();

            string lLastError = string.Empty;
            int CountJobsToProcess = 0;

            try
            {
                KEPSSenderdbData senderDbData = new KEPSSenderdbData(OdataServiceUrl);
                JobOrders jobsToProcess = senderDbData.GetJobsToProcess();
                CountJobsToProcess = jobsToProcess.JobOrdersObj.Count;
                SenderMonitorEvent.SendMonitorEvent(EventLog, "Jobs to process: " + CountJobsToProcess, EventLogEntryType.Information, 3);

                string sendState = string.Empty;
                if (CountJobsToProcess > 0)
                {
                    // Step 1 -- Connect to UA server
                    SenderMonitorEvent.SendMonitorEvent(EventLog, "Step 1 -- Connect to OPC server", EventLogEntryType.Information, 4);
                    //string discoveryUrl = "opc.tcp://127.0.0.1:49320";
                    using (Session AMSession = ClientUtils.CreateSession(OPCServerUrl, "ArcelorMittal.UA.SenderCommand"))
                    {
                        SenderMonitorEvent.SendMonitorEvent(EventLog, "Loop through jobs", EventLogEntryType.Information, 4);
                        foreach (JobOrders.JobOrdersValue jobVal in jobsToProcess.JobOrdersObj)
                        {
                            try
                            {
                                SenderMonitorEvent.SendMonitorEvent(EventLog, "new SenderJobProps", EventLogEntryType.Information, 4);
                                SenderJobProps job = new SenderJobProps(jobVal.ID,
                                                                        jobVal.Command,
                                                                        (string)(jobVal.CommandRule));
                                SenderMonitorEvent.SendMonitorEvent(EventLog, "WriteToKEPServer", EventLogEntryType.Information, 4);
                                if (WriteToKEPServer(AMSession, job))
                                {
                                    sendState = "Done";
									if(wmiProductInfo!=null){
										wmiProductInfo.LastActivityTime = DateTime.Now;
									}
                                }
                                else
                                {
                                    sendState = "Failed";
                                }
                                lLastError = string.Format("JobOrderID: {0}. Send to KEP Server element {1} = {2}. Status: {3}", job.JobOrderID, job.Command, job.CommandRule, sendState);

                                if (sendState == "Done")
                                {
                                    Requests.UpdateJobStatus(OdataServiceUrl, job.JobOrderID, sendState);
                                }
                                else if (sendState == "Failed")
                                {
									if(wmiProductInfo!=null){
                                        wmiProductInfo.LastServiceError = string.Format("{0}. On {1}", lLastError, DateTime.Now);
									}
                                }
                                SenderMonitorEvent.SendMonitorEvent(EventLog, lLastError, EventLogEntryType.Information, 5);
                            }
                            catch (Exception ex)
                            {
                                lLastError = "Error sending command: " + ex.ToString();
                                SenderMonitorEvent.SendMonitorEvent(EventLog, lLastError, EventLogEntryType.Error, 4);
                                lLastError = "Reconecting...";
                                SenderMonitorEvent.SendMonitorEvent(EventLog, lLastError, EventLogEntryType.Information, 4);
                                AMSession.Reconnect();
                            }
                        }
                        // Step 3 -- Clean up
                        //AMSession.Close();
                        AMSession.KeepAlive -= ClientUtils.Session_KeepAlive;
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    string details = string.Empty;
                    if (ex is System.Net.WebException)
                    {
                        var resp = new System.IO.StreamReader((ex as System.Net.WebException).Response.GetResponseStream()).ReadToEnd();

                        try
                        {
                            dynamic obj = Newtonsoft.Json.JsonConvert.DeserializeObject(resp);
                            details = obj.error.message;
                        }
                        catch
                        {
                            details = resp;
                        }
                    }
                    lLastError = "Error getting jobs: " + ex.ToString() + " Details: " + details;
                    SenderMonitorEvent.SendMonitorEvent(EventLog, lLastError, EventLogEntryType.Error, 4);
					if(wmiProductInfo!=null){
                    wmiProductInfo.LastServiceError = string.Format("{0}. On {1}", lLastError, DateTime.Now);
					}
                }
                catch (Exception exc)
                {
                    SenderMonitorEvent.SendMonitorEvent(EventLog, exc.Message, EventLogEntryType.Error, 4);
                }                
            }
			if(wmiProductInfo!=null){
            wmiProductInfo.SendCommandsCount += CountJobsToProcess;
            wmiProductInfo.PublishInfo();
			}
            SenderMonitorEvent.SendMonitorEvent(EventLog, string.Format("Send command is done. {0} tasks", CountJobsToProcess), EventLogEntryType.Information, 3);

            m_SenderTimer.Start();
        }

        private object ConvertToObject(string elementValue)
        {
            object resultValue = null;
            int rightBracketPos = elementValue.IndexOf(')');

            if (rightBracketPos > 0)
            {
                NumberFormatInfo nfi = new NumberFormatInfo
                {
                    NumberDecimalSeparator = "."
                };

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
                            resultValue = double.Parse(sValue, nfi);
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
                            resultValue = float.Parse(sValue, nfi);
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

            if (AMSession.NodeCache.Find(nodeToRead) is Node node)
            {
                DataValue value = AMSession.ReadValue(nodeToRead);
                value.ServerTimestamp = new DateTime(0);
                value.SourceTimestamp = new DateTime(0);
                WriteValue Wvalue = new WriteValue
                {
                    NodeId = nodeToRead,
                    AttributeId = Attributes.Value
                };

                if ((node.NodeClass & (NodeClass.Variable | NodeClass.VariableType)) == 0)
                {
                    Wvalue.AttributeId = Attributes.DisplayName;
                }

                Wvalue.IndexRange = null;
                Wvalue.Value = value;
                Wvalue.Value.Value = ConvertToObject(job.CommandRule);
                
                if (Wvalue.Value.Value != null)
                {
                    WriteValueCollection nodesToWrite = new WriteValueCollection
                    {
                        Wvalue
                    };

                    ResponseHeader responseHeader = AMSession.Write(
                            null,
                            nodesToWrite,
                            out StatusCodeCollection results,
                            out DiagnosticInfoCollection diagnosticInfos);

                    Session.ValidateResponse(results, nodesToWrite);
                    //Session.ValidateDiagnosticInfos(diagnosticInfos, nodesToWrite);

                    if (results[0].Code == 0)
                    {
                        return true;
                    }
                    else
                    {
                        SenderMonitorEvent.SendMonitorEvent(EventLog, string.Format("Can not send command to KEP Server. ErrorCode: {0}. ErrorText: {1}. Job order ID: {2}", results[0].Code, results[0].ToString(), job.JobOrderID), EventLogEntryType.Error, 5);
                        return false;
                    }
                }
                else
                {
                    SenderMonitorEvent.SendMonitorEvent(EventLog, string.Format("Can not convert command value: {0}. Job order ID: {1}", job.CommandRule, job.JobOrderID), EventLogEntryType.Error, 5);
                    return false;
                }
            }
            else
            {
                SenderMonitorEvent.SendMonitorEvent(EventLog, string.Format("Command not valid: {0}. Job order ID: {1}", job.Command, job.JobOrderID), EventLogEntryType.Error, 5);
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
                              string cCommandRule) : base(cJobOrderID,
                                                           cCommand,
                                                           cCommandRule)
        { }
    }
    /// <summary>
    /// Class for processing of input queue and generation of list of commands for KEP Server
    /// </summary>
    public class KEPSSenderdbData
    {
        private readonly string webServiceUrl;

        /// <summary>	Constructor. </summary>
        ///
        /// <param name="webServiceUrl">	URL of the web service. </param>
        public KEPSSenderdbData(string webServiceUrl)
        {
            this.webServiceUrl = webServiceUrl;
        }

        /// <summary>
        /// Get KEP Server commands jobs to process
        /// </summary>
        public JobOrders GetJobsToProcess()
        {
            return new JobOrders(webServiceUrl, "KEPCommands", "ToSend");
        }
    }
}
