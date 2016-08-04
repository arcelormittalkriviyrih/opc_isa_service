using System;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace JobOrdersService
{
    /// <summary>
    /// Prepare request data from web service
    /// </summary>
    public static class Requests
    {
        /// <summary>
        /// Create final url for web service
        /// </summary>
        public static string CreateRequest(string webServiceUrl, string queryString)
        {
            string UrlRequest = webServiceUrl + queryString;
            ///http://mssql2014srv/odata_unified_svc/api/Dynamic/
            return UrlRequest;
        }

        /// <summary>
        /// Request data from web service
        /// </summary>
        public static string MakeRequest(string requestUrl)
        {
            string responseText = string.Empty;
            HttpWebRequest request = WebRequest.Create(requestUrl) as HttpWebRequest;
            request.Credentials = CredentialCache.DefaultNetworkCredentials;
#if (DEBUG)
            request.Credentials = new NetworkCredential("atokar", "qcAL0ZEV", "ask-ad");
#endif
            using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
            {
                if (response.StatusCode != HttpStatusCode.OK)
                    throw new Exception(string.Format(
                    "Server error (HTTP {0}: {1}).",
                    response.StatusCode,
                    response.StatusDescription));
                var encoding = ASCIIEncoding.ASCII;
                using (var reader = new System.IO.StreamReader(response.GetResponseStream(), encoding))
                {
                    responseText = reader.ReadToEnd();
                }
                response.Close();
            }
            return responseText;
        }

        /// <summary>
        /// Update status of the job
        /// </summary>
        public static void updateJobStatus(string webServiceUrl, int aJobOrderID, string aActionState)
        {
            string UpdateStatusUrl = Requests.CreateRequest(webServiceUrl, string.Format("JobOrder({0})",
                                                            aJobOrderID));
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(UpdateStatusUrl);

            string currentDateTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.000Z");
            string payload = "{" + string.Format(@"""DispatchStatus"":""{0}""", "Done") + string.Format(@",""EndTime"":""{0}""", currentDateTime) + "}";

            byte[] body = Encoding.UTF8.GetBytes(payload);
            request.Method = "PATCH";
            request.ContentLength = body.Length;
            request.ContentType = "application/json";
            request.Credentials = CredentialCache.DefaultNetworkCredentials;
#if (DEBUG)
            request.Credentials = new NetworkCredential("atokar", "qcAL0ZEV", "ask-ad");
#endif

            using (Stream stream = request.GetRequestStream())
            {
                stream.Write(body, 0, body.Length);
                stream.Close();
            }

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.NoContent)
                    throw new Exception(string.Format(
                    "Server error (HTTP {0}: {1}).",
                    response.StatusCode,
                    response.StatusDescription));

                var encoding = ASCIIEncoding.ASCII;
                using (var reader = new System.IO.StreamReader(response.GetResponseStream(), encoding))
                {
                    string responseText = reader.ReadToEnd();
                }

                response.Close();
            }
        }
    }
    /// <summary>
    /// Job orders list
    /// </summary>
    public class JobOrders
    {
        /// <summary>	URL of the web service. </summary>
        private string webServiceUrl;
        /// <summary>	The job orders object. </summary>
        private List<JobOrdersValue> jobOrdersObj = null;

        /// <summary>	Gets the job orders object. </summary>
        ///
        /// <value>	The job orders object. </value>
        public List<JobOrdersValue> JobOrdersObj
        {
            get { return jobOrdersObj; }
        }

        /// <summary>	Constructor. </summary>
        ///
        /// <param name="webServiceUrl">	URL of the web service. </param>
        /// <param name="workType">		 	Type of the work. </param>
        /// <param name="dispatchStatus">	The dispatch status. </param>
        public JobOrders(string webServiceUrl, string workType, string dispatchStatus)
        {
            this.webServiceUrl = webServiceUrl;
            string JobOrdersUrl = Requests.CreateRequest(webServiceUrl, "v_JobOrders?$filter=WorkType%20eq%20%27" + workType + "%27%20and%20DispatchStatus%20eq%20%27" + dispatchStatus + "%27&$orderby=ID&$select=ID,Command,CommandRule");
            //test string JobOrdersUrl = Requests.CreateRequest(webServiceUrl, "v_JobOrders?$filter=ID%20eq%2040625&$select=ID,Command,CommandRule"); 
            string JobOrdersSerial = Requests.MakeRequest(JobOrdersUrl);
            jobOrdersObj = DeserializeJobOrders(JobOrdersSerial);
        }

        /// <summary>	A job orders value. </summary>
        public class JobOrdersValue
        {
            /// <summary>	Gets or sets the identifier. </summary>
            ///
            /// <value>	The identifier. </value>
            public int ID { get; set; }

            /// <summary>	Gets or sets the command. </summary>
            ///
            /// <value>	The command. </value>
            public string Command { get; set; }

            /// <summary>	Gets or sets the command rule. </summary>
            ///
            /// <value>	The command rule. </value>
            public object CommandRule { get; set; }
        }

        /// <summary>	A job orders root. </summary>
        private class JobOrdersRoot
        {
            /// <summary>	Gets or sets the metadata. </summary>
            ///
            /// <value>	The metadata. </value>
            [JsonProperty("odata.metadata")]
            public string Metadata { get; set; }

            /// <summary>	Gets or sets the value. </summary>
            ///
            /// <value>	The value. </value>
            public List<JobOrdersValue> value { get; set; }
        }

        /// <summary>	Deserialize job orders. </summary>
        ///
        /// <param name="json">	The JSON. </param>
        ///
        /// <returns>	A List&lt;JobOrdersValue&gt; </returns>
        private List<JobOrdersValue> DeserializeJobOrders(string json)
        {
            JobOrdersRoot prRoot = JsonConvert.DeserializeObject<JobOrdersRoot>(json);
            return prRoot.value;
        }
    }
}