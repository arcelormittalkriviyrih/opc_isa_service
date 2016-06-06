using System;
using System.Data;
using System.IO;
using System.Collections.Generic;

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
        }
    }
}