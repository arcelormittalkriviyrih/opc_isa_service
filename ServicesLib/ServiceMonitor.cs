using System;
using System.Diagnostics;
using System.Management.Instrumentation;

[assembly: Instrumented("root\\PrintWindowsService")]

namespace CommonEventSender
{
    [InstrumentationClass(InstrumentationType.Event)]
    /// <summary>
    /// Event for a grant in WMI
    /// </summary>
    public class SenderMonitorEvent
    {
        private string message;
        private EventLogEntryType eventType;
        private DateTime eventTime;

        /// <summary>
        /// Text of event massage
        /// </summary>
        public string Message
        {
            get { return message; }
        }
        /// <summary>
        /// Type of event massage
        /// </summary>
        public string EventTypeName
        {
            get { return eventType.ToString(); }
        }
        /// <summary>
        /// Time of event massage
        /// </summary>
        public DateTime EventTime
        {
            get { return eventTime; }
        }

        public SenderMonitorEvent(EventLog eventLog, string message, EventLogEntryType eventType)
        {
            this.message = message;
            this.eventType = eventType;
            eventTime = DateTime.Now;
            if (eventLog != null)
            {
                eventLog.WriteEntry(message, eventType);
            }
        }

        /// <summary>
        /// Create and fire event
        /// </summary>
        public static void sendMonitorEvent(EventLog eventLog, string message, EventLogEntryType eventType)
        {
            SenderMonitorEvent MonitorEvent = new SenderMonitorEvent(eventLog, message, eventType);
            Instrumentation.Fire(MonitorEvent);
        }
    }
}

namespace PrintWindowsService
{
    [InstrumentationClass(InstrumentationType.Instance)]
    /// <summary>
    /// Class for a grant print service info in WMI
    /// </summary>
    public class PrintServiceProductInfo
    {
        private string prAppName;
        private string prComputerName;
        private string prVersion;
        private DateTime prStartTime;
        private int prPrintTaskFrequencyInSeconds;
        //private int prPingTimeoutInSeconds;
        //private string prDBConnectionString;
        private string prOdataServiceUrl;
        private DateTime prLastActivityTime;
        private string prLastServiceError;
        private int prPrintedLabelsCount;

        /// <summary>
        /// Application name
        /// </summary>
        public string AppName
        {
            get { return prAppName; }
        }
        /// <summary>
        /// Computer name
        /// </summary>
        public string ComputerName
        {
            get { return prComputerName; }
        }
        /// <summary>
        /// Version
        /// </summary>
        public string Version
        {
            get { return prVersion; }
        }
        /// <summary>
        /// Time of app start
        /// </summary>
        public DateTime StartTime
        {
            get { return prStartTime; }
        }
        /// <summary>
        /// Print task frequency in seconds
        /// </summary>
        public int PrintTaskFrequencyInSeconds
        {
            get { return prPrintTaskFrequencyInSeconds; }
        }        
        /// <summary>
        /// DB connection string
        /// </summary>
        public string OdataServiceUrl
        {
            get { return prOdataServiceUrl; }
        }
        /// <summary>
        /// Time from the moment of start
        /// </summary>
        public TimeSpan TimeFromStart
        {
            get { return DateTime.Now - prStartTime; }
        }
        /// <summary>
        /// Time of the last activity of service
        /// </summary>
        public DateTime LastActivityTime
        {
            get { return prLastActivityTime; }
            set { prLastActivityTime = value; }
        }
        /// <summary>
        /// Last error of service
        /// </summary>
        public string LastServiceError
        {
            get { return prLastServiceError; }
            set { prLastServiceError = value; }
        }
        /// <summary>
        /// Count of the printed labels
        /// </summary>
        public int PrintedLabelsCount
        {
            get { return prPrintedLabelsCount; }
            set { prPrintedLabelsCount = value; }
        }

        /// <summary>	Constructor. </summary>
        ///
        /// <param name="appName">					   	Name of the application. </param>
        /// <param name="computerName">			   	Name of the computer. </param>
        /// <param name="version">					   	The version. </param>
        /// <param name="startTime">				   	The start time. </param>
        /// <param name="odataServiceUrl">			   	URL of the odata service. </param>
        public PrintServiceProductInfo(string appName,
                                       string computerName,
                                       string version,
                                       DateTime startTime,
                                       string odataServiceUrl)
        {
            prAppName = appName;
            prComputerName = computerName;
            prVersion = version;
            prStartTime = startTime;
            prOdataServiceUrl = odataServiceUrl;

            LastActivityTime = new DateTime(0);
			LastServiceError = string.Empty;
            PrintedLabelsCount = 0;

            PublishInfo();
        }

        /// <summary>	Publish information. </summary>
        public void PublishInfo()
        {
            Instrumentation.Publish(this);
        }
    }
}

namespace KEPServerSenderService
{
    [InstrumentationClass(InstrumentationType.Instance)]
    /// <summary>
    /// Class for a grant KEP server sender in WMI
    /// </summary>
    public class KEPSenderServiceProductInfo
    {
        private string prAppName;
        private string prComputerName;
        private string prVersion;
        private DateTime prStartTime;
        private int prSendCommandFrequencyInSeconds;
        private string prOdataServiceUrl;
        private DateTime prLastActivityTime;
        private string prLastServiceError;
        private int prSendCommandsCount;

        /// <summary>
        /// Application name
        /// </summary>
        public string AppName
        {
            get { return prAppName; }
        }
        /// <summary>
        /// Computer name
        /// </summary>
        public string ComputerName
        {
            get { return prComputerName; }
        }
        /// <summary>
        /// Version
        /// </summary>
        public string Version
        {
            get { return prVersion; }
        }
        /// <summary>
        /// Time of app start
        /// </summary>
        public DateTime StartTime
        {
            get { return prStartTime; }
        }
        /// <summary>
        /// Send command frequency in seconds
        /// </summary>
        public int SendCommandFrequencyInSeconds
        {
            get { return prSendCommandFrequencyInSeconds; }
        }
        /// <summary>
        /// Odata service connection string
        /// </summary>
        public string OdataServiceUrl
        {
            get { return prOdataServiceUrl; }
        }
        /// <summary>
        /// Time from the moment of start
        /// </summary>
        public TimeSpan TimeFromStart
        {
            get { return DateTime.Now - prStartTime; }
        }
        /// <summary>
        /// Time of the last activity of service
        /// </summary>
        public DateTime LastActivityTime
        {
            get { return prLastActivityTime; }
            set { prLastActivityTime = value; }
        }
        /// <summary>
        /// Last error of service
        /// </summary>
        public string LastServiceError
        {
            get { return prLastServiceError; }
            set { prLastServiceError = value; }
        }
        /// <summary>
        /// Count of the printed labels
        /// </summary>
        public int SendCommandsCount
        {
            get { return prSendCommandsCount; }
            set { prSendCommandsCount = value; }
        }

        /// <summary>	Constructor. </summary>
        ///
        /// <param name="cAppName">						 	Name of the application. </param>
        /// <param name="cComputerName">				 	Name of the computer. </param>
        /// <param name="cVersion">						 	The version. </param>
        /// <param name="cStartTime">					 	The start time. </param>
        /// <param name="cSendCommandFrequencyInSeconds">	The send command frequency in seconds. </param>
        /// <param name="cOdataServiceUrl">				 	URL of the odata service. </param>
        public KEPSenderServiceProductInfo(string cAppName,
                                           string cComputerName,
                                           string cVersion,
                                           DateTime cStartTime,
                                           int cSendCommandFrequencyInSeconds,
                                           string cOdataServiceUrl)
        {
            prAppName = cAppName;
            prComputerName = cComputerName;
            prVersion = cVersion;
            prStartTime = cStartTime;
            prSendCommandFrequencyInSeconds = cSendCommandFrequencyInSeconds;
            prOdataServiceUrl = cOdataServiceUrl;

            LastActivityTime = new DateTime(0);
            LastServiceError = string.Empty;
            SendCommandsCount = 0;

            PublishInfo();
        }

        /// <summary>	Publish information. </summary>
        public void PublishInfo()
        {
            Instrumentation.Publish(this);
        }
    }
}