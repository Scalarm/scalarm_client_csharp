using System;
using System.Collections.Generic;


namespace Scalarm
{
    public class ExperimentCreationResult
    {
        public string status {get; set;}
        public string experiment_id {get; set;}
        public string message {get; set;}
    }

    public class ScenarioCreationResult
    {
        public string status {get; set;}
        public string simulation_id {get; set;}
        public string message {get; set;}
    }

    public class ScheduleSimulationManagersResult
    {
        public string status {get; set;}
        public string msg {get; set;}
        public string error_code {get; set;}
        public string infrastructure {get; set;}
        public List<string> records_ids {get; set;}
    }

    public class SimulationManagerResource
    {
        public string status {get; set;}
        public SimulationManager record {get; set;}
    }

	public class SimulationManagersList
	{
		public string status { get; set; }
		public List<SimulationManager> sm_records { get; set; }
	}

	public class AddCredentialsResult
	{
		public string status { get; set; }
		public string record_id { get; set; }
		public string msg { get; set; }
		public string error_code { get; set; }
	}

	public class SimulationManagerCommandResult
	{
		public string status { get; set; }
		public string msg { get; set; }
		public string cmd { get; set; }
		public string error_code { get; set; }
	}
}

