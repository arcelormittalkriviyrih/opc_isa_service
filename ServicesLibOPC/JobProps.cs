using System;

namespace JobPropsService
{
    /// <summary>
    /// Class of command properties
    /// </summary>
    public class JobProps
    {
        private int jobOrderID;
        private string command;
        private string commandRule;
        /// <summary>
        /// Job order ID
        /// </summary>
        public int JobOrderID
        {
            get { return jobOrderID; }
        }
        /// <summary>
        /// Job order command
        /// </summary>
        public string Command
        {
            get { return command; }
        }
        /// <summary>
        /// Job order command
        /// </summary>
        public string CommandRule
        {
            get { return commandRule; }
        }

        /// <summary>	Constructor. </summary>
        ///
        /// <param name="cJobOrderID"> 	Identifier for the job order. </param>
        /// <param name="cCommand">	   	The command. </param>
        /// <param name="cCommandRule">	The command rule. </param>s
        public JobProps(int cJobOrderID,
                        string cCommand,
                        string cCommandRule)
        {
            jobOrderID = cJobOrderID;
            command = cCommand;
            commandRule = cCommandRule;
        }
    }
}
