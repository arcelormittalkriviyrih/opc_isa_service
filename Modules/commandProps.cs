using System;
using System.Data;
using System.IO;
using System.Collections.Generic;
using Opc.Ua;
using Opc.Ua.Client;

namespace KEPServerSenderService
{
    /// <summary>
    /// Class of command properties
    /// </summary>
    public class commandProps
    {
        private int productionResponseID;
        /// <summary>
        /// Production response ID
        /// </summary>
        public int ProductionResponseID
        {
            get { return productionResponseID; }
        }
        public commandProps(int cProductionResponseID)
        {
            productionResponseID = cProductionResponseID;

            // Step 1 -- Connect to UA server
            string discoveryUrl = "opc.tcp://127.0.0.1:49320";

            Session mySession = ClientUtils.CreateSession(discoveryUrl, "OSIsoft.UA.ConsoleClientDemo");

            // Step 2 -- Read the value of a node representing a PI Point data under the hood
            NodeId nodeToRead = new NodeId(@"Channel1.Device1.Tag2", 2);
            DataValue value = mySession.ReadValue(nodeToRead);
            //???value.Value = 10;

            Console.WriteLine("\nRead: Current value of intense0001 = {0}, status = {1} \n", value.Value, value.StatusCode);
            Console.WriteLine("Press Enter to finish the program \n");
            Console.ReadKey();

            // Step 3 -- Clean up
            mySession.Close();
        }
    }
}