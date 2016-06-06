using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Reflection;

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

        private ProductInfo wmiProductInfo;
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

            wmiProductInfo = new ProductInfo(cServiceTitle,
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
            List<commandProps> JobData = new List<commandProps>();
            try
            {
                ServicedbData lDbData = new ServicedbData(OdataServiceUrl);
                lDbData.fillJobData(ref JobData);

                foreach (commandProps job in JobData)
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
        #endregion
    }
}
