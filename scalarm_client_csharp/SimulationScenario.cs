using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Scalarm.ExperimentInput;
using RestSharp;
using RestSharp.Deserializers;
using System.Net;

namespace Scalarm
{	
	public class SimulationScenario : ScalarmObject
    {
        [DeserializeAs(Name = "_id")]
        public string Id {get; private set;}

        [JsonProperty(PropertyName = "name")]
        public string Name {get; private set;}

        [JsonProperty(PropertyName = "description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description {get; private set;}

        [JsonProperty(PropertyName = "input_specification")]
        public List<Category> InputSpecification {get; private set;}

        [JsonProperty(PropertyName = "user_id")]
        public string UserId {get; private set;}

        [JsonProperty(PropertyName = "created_at")]
        public DateTime CreatedAt {get; private set;}

        [JsonProperty(PropertyName = "input_writer_id")]
        public string InputWriterId {get; private set;}

        [JsonProperty(PropertyName = "executor_id")]
        public string ExecutorId {get; private set;}

        [JsonProperty(PropertyName = "output_reader_id")]
        public string OutputReaderId {get; private set;}

        [JsonProperty(PropertyName = "progress_monitor_id")]
        public string ProgressMonitorId {get; private set;}

        [JsonProperty(PropertyName = "simulation_binaries_id")]
        public string SimulationBinariesId {get; private set;}

        public SimulationScenario()
        {}
		public List<string> GetAllSimulationScenarioIds(){
			return Client.GetSimulationScenarioIds();
		}
		// TODO: change to use CustomPointsExperiment
        public Experiment CreateExperimentWithPoints(List<ValuesMap> points, Dictionary<string, object> experimentParams)
        {
            Experiment experiment = Client.CreateExperimentWithPoints(Id, points, experimentParams);
			experiment.InputSpecification = this.InputSpecification;
			return experiment;
        }

		public SupervisedExperiment CreateSupervisedExperiment(string supervisorId, Dictionary<string, object> experimentParams)
		{
			SupervisedExperiment experiment = Client.CreateSupervisedExperiment(Id, supervisorId, experimentParams);
			experiment.InputSpecification = this.InputSpecification;
			return experiment;
		}
	}

}

