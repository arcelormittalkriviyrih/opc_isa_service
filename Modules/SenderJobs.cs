using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Principal;
using System.Reflection;
using CommonEventSender;
using Opc.Ua;
using Opc.Ua.Client;

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
        private const string cSystemEventLogName = "ArcelorMittal.KEPSSenderService.Log";

        /// <summary>
        /// The name of the configuration parameter for the send commands frequency in seconds.
        /// </summary>
        private const string cSendCommandFrequencyName = "SendCommandFrequency";

        /// <summary>
        /// The name of the configuration parameter for the Odata service url.
        /// </summary>
        private const string cOdataService = "OdataServiceUri";

        #endregion

        #region Fields

        /// <summary>
        /// Time interval for checking send commands
        /// </summary>
        private System.Timers.Timer m_SenderTimer;

        private KEPSenderServiceProductInfo wmiProductInfo;
        private bool fJobStarted = false;
        private string OdataServiceUrl;
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

        public SenderJobs()
        {
            // Set up a timer to trigger every send command frequency.
            int sendCommandFrequencyInSeconds = int.Parse(System.Configuration.ConfigurationManager.AppSettings[cSendCommandFrequencyName]);
            OdataServiceUrl = System.Configuration.ConfigurationManager.AppSettings[cOdataService];

            wmiProductInfo = new KEPSenderServiceProductInfo(cServiceTitle,
                                                             Environment.MachineName,
                                                             Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                                                             DateTime.Now,
                                                             sendCommandFrequencyInSeconds,
                                                             OdataServiceUrl);

            m_SenderTimer = new System.Timers.Timer();
            m_SenderTimer.Interval = sendCommandFrequencyInSeconds * 1000; // seconds to milliseconds
            m_SenderTimer.Elapsed += new System.Timers.ElapsedEventHandler(this.OnSenderTimer);

            senderMonitorEvent.sendMonitorEvent(vpEventLog, string.Format("Send Command Frequncy = {0}", sendCommandFrequencyInSeconds), EventLogEntryType.Information);
        }

        #endregion

        #region Destructor

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
            senderMonitorEvent.sendMonitorEvent(vpEventLog, "Starting send command service...", EventLogEntryType.Information);

            m_SenderTimer.Start();

            senderMonitorEvent.sendMonitorEvent(vpEventLog, "Send command service has been started", EventLogEntryType.Information);
            fJobStarted = true;
        }

        /// <summary>
        /// Stop of processing of input queue
        /// </summary>
        public void StopJob()
        {
            senderMonitorEvent.sendMonitorEvent(vpEventLog, "Stopping send command service...", EventLogEntryType.Information);

            //stop timers if working
            if (m_SenderTimer.Enabled)
                m_SenderTimer.Stop();

            senderMonitorEvent.sendMonitorEvent(vpEventLog, "Send command service has been stopped", EventLogEntryType.Information);
            fJobStarted = false;
        }

        /// <summary>
        /// Processing of input queue
        /// </summary>
        public void OnSenderTimer(object sender, System.Timers.ElapsedEventArgs args)
        {
            senderMonitorEvent.sendMonitorEvent(vpEventLog, "Monitoring the send command activity", EventLogEntryType.Information);
            m_SenderTimer.Stop();

            string lLastError = "";
            List<SenderJobProps> JobData = new List<SenderJobProps>();
            try
            {
                KEPSSenderdbData lDbData = new KEPSSenderdbData(OdataServiceUrl);
                //lDbData.fillJobData(JobData);

                foreach (SenderJobProps job in JobData)
                {
                    ;
                    
                }
            }
            catch (Exception ex)
            {
                lLastError = "Get data from DB. Error: " + ex.ToString();
                senderMonitorEvent.sendMonitorEvent(vpEventLog, lLastError, EventLogEntryType.Error);
                wmiProductInfo.LastServiceError = string.Format("{0}. On {1}", lLastError, DateTime.Now);
            }
            wmiProductInfo.SendCommandsCount += JobData.Count;
            wmiProductInfo.PublishInfo();
            senderMonitorEvent.sendMonitorEvent(vpEventLog, string.Format("Send command is done. {0} tasks", JobData.Count), EventLogEntryType.Information);

            m_SenderTimer.Start();
        }

        private void WriteToKEPServer()
        {
            // Step 1 -- Connect to UA server
            string discoveryUrl = "opc.tcp://127.0.0.1:49320";

            using (Session AMSession = ClientUtils.CreateSession(discoveryUrl, "ArcelorMittal.UA.SenderCommand"))
            {
                // Step 2 -- Read the value of a node representing a PI Point data under the hood
                NodeId nodeToRead = new NodeId(@"Channel1.Device1.Tag1", 2);
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
                    Wvalue.Value.Value = (UInt16)27;

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
                    Session.ValidateDiagnosticInfos(diagnosticInfos, nodesToWrite);

                    if (results[0].Code == 0)
                    {
                        Console.WriteLine("Good job");
                    }
                    else
                    {
                        Console.WriteLine(String.Format("Code {0} text {1}", results[0].Code, results[0].ToString()));
                    }
                    Console.WriteLine("Press Enter to finish the program \n");
                }
                else
                {
                    Console.WriteLine("Item not found");
                }
                Console.ReadKey();
                // Step 3 -- Clean up
                AMSession.Close();
            }
        }
        #endregion
    }
}
