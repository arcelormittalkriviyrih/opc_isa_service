using System;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace KEPServerSenderService
{
    /// <summary>
    /// Property values class
    /// </summary>
    public class PrintPropertiesValue
    {
        public string TypeProperty { get; set; }
        public int ClassPropertyID { get; set; }
        public string ValueProperty { get; set; }
    }

    /// <summary>
    /// Class for processing of input queue and generation of list of labels for printing
    /// </summary>
    public class ServicedbData
    {
        private string webServiceUrl;

        /// <summary>
        /// Production response data
        /// </summary>
        private class ProductionResponseValue
        {
            public int ID { get; set; }
            public object ProductSegmentID { get; set; }
            public object ProcessSegmentID { get; set; }
        }

        private class ProductionResponseRoot
        {
            [JsonProperty("odata.metadata")]
            public string Metadata { get; set; }
            public List<ProductionResponseValue> value { get; set; }
        }

        private class PrintPropertiesRoot
        {
            [JsonProperty("odata.metadata")]
            public string Metadata { get; set; }
            public List<PrintPropertiesValue> value { get; set; }
        }

        /// <summary>
        /// Label template file data
        /// </summary>
        private class LabelTemplateValue
        {
            public byte[] Data { get; set; }
        }

        private class LabelTemplateRoot
        {
            [JsonProperty("odata.metadata")]
            public string Metadata { get; set; }
            public List<LabelTemplateValue> value { get; set; }
        }

        private List<ProductionResponseValue> DeserializeProdResponse(string json)
        {
            ProductionResponseRoot prRoot = JsonConvert.DeserializeObject<ProductionResponseRoot>(json);
            return prRoot.value;
        }

        private List<PrintPropertiesValue> DeserializePrintProperties(string json)
        {
            PrintPropertiesRoot ppRoot = JsonConvert.DeserializeObject<PrintPropertiesRoot>(json);
            return ppRoot.value;
        }

        private List<LabelTemplateValue> DeserializeLabelTemplate(string json)
        {
            LabelTemplateRoot ltRoot = JsonConvert.DeserializeObject<LabelTemplateRoot>(json);
            return ltRoot.value;
        }

        /// <summary>
        /// Create final url for web service
        /// </summary>
        private string CreateRequest(string queryString)
        {
            string UrlRequest = webServiceUrl + queryString;
            ///http://mssql2014srv/odata_unified_svc/api/Dynamic/
            return UrlRequest;
        }

        /// <summary>
        /// Request data from web service
        /// </summary>
        static string MakeRequest(string requestUrl)
        {
            string responseText = "";
            HttpWebRequest request = WebRequest.Create(requestUrl) as HttpWebRequest;
#if (DEBUG)
            request.Credentials = new NetworkCredential("atokar", "qcAL0ZEV", "ask-ad");
#endif
            using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
            {
                if (response.StatusCode != HttpStatusCode.OK)
                    throw new Exception(String.Format(
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

        public ServicedbData(string aWebServiceUrl)
        {
            webServiceUrl = aWebServiceUrl;
        }

        /// <summary>
        /// Processing of input queue and generation of list of labels for printing
        /// </summary>
        public void fillJobData(ref List<commandProps> resultData)
        {
            byte[] XlFile = null;
            string ProductionUrl = CreateRequest("v_ProductionResponse?$filter=ResponseState%20eq%20%27ToPrint%27&$select=ID,ProductSegmentID,ProcessSegmentID");
            string ProductionResponse = MakeRequest(ProductionUrl);
            //JsonValue json = JsonValue.Parse(result);

            List<ProductionResponseValue> ProductionResponseObj = DeserializeProdResponse(ProductionResponse);
            foreach (ProductionResponseValue prValue in ProductionResponseObj)
            {
                string PropertiesUrl = CreateRequest(String.Format("v_PrintProperties?$filter=ProductionResponse%20eq%20{0}&$select=TypeProperty,ClassPropertyID,ValueProperty", prValue.ID));
                string PropertiesResponse = MakeRequest(PropertiesUrl);
                List<PrintPropertiesValue> PrintPropertiesObj = DeserializePrintProperties(PropertiesResponse);

                string TemplateUrl = CreateRequest(String.Format("v_ProductionParameter_Files?$filter=ProductSegmentID%20eq%20{0}%20and%20ProcessSegmentID%20eq%20{1}%20and%20Value%20eq%20%27{2}%27&$select=Data",
                                                                 prValue.ProductSegmentID == null ? 0 : prValue.ProductSegmentID, prValue.ProcessSegmentID == null ? 0 : prValue.ProcessSegmentID, "TEMPLATE"));
                string TemplateResponse = MakeRequest(TemplateUrl);
                List<LabelTemplateValue> LabelTemplateObj = DeserializeLabelTemplate(TemplateResponse);
                if (LabelTemplateObj.Count > 0)
                {
                    XlFile = LabelTemplateObj[0].Data;
                }

                resultData.Add(new commandProps(prValue.ID));
            }
        }

        /// <summary>
        /// Update status of label print
        /// </summary>
        public void updateJobStatus(int aProductionResponseID, string aPrintState)
        {
            string UpdateStatusUrl = CreateRequest(String.Format("ProductionResponse({0})", aProductionResponseID));
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(UpdateStatusUrl);

            string payload = "{" + string.Format(@"""ResponseState"":""{0}""", aPrintState) + "}";

            byte[] body = Encoding.UTF8.GetBytes(payload);
            request.Method = "PATCH";
            request.ContentLength = body.Length;
            request.ContentType = "application/json";
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
                    throw new Exception(String.Format(
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
}