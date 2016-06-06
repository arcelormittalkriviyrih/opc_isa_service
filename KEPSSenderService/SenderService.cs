using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;

namespace KEPServerSenderService
{
	public partial class SenderService : ServiceBase
	{
		private SenderJobs pJobs;

        #region Constructor

        public SenderService()
		{
			InitializeComponent();

            // Set up a timer to trigger every print task frequency.
            pJobs = new SenderJobs();
        }

        #endregion

        #region Methods

        protected override void OnStart(string[] args)
		{
            pJobs.StartJob();
        }

		protected override void OnStop()
		{
            pJobs.StopJob();
        }
        #endregion
    }
}
