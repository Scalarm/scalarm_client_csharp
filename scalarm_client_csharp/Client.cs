using System;
using System.Net;
using System.Collections.Generic;
using Newtonsoft.Json;
using RestSharp;
using System.Linq;
using System.Collections;

namespace Scalarm
{	
	public abstract class Client : RestClient
	{
		public static string PrepareStringForHeader(string value) {
			return value.Replace ("\n", "\\r\\n");
		}

		public Client(string baseUrl)
		{
			// TODO: certificate check by default, optional insecure
			ServicePointManager.ServerCertificateValidationCallback +=
        		(sender, certificate, chain, sslPolicyErrors) => true;
			
			this.BaseUrl = new Uri(baseUrl);
            // Cannot use this because of bug in JSON.net for Mono!
            // this.AddHandler("application/json", new JsonConvertDeserializer());
		}

		public SimulationScenario GetScenarioById(string scenarioId)
		{
            var request = new RestRequest("/simulation_scenarios/{id}", Method.GET);
            request.AddUrlSegment("id", scenarioId);

            var response = this.Execute<ResourceEnvelope<SimulationScenario>>(request);

			ValidateResponseStatus(response);

            var resource = response.Data;

            if (resource.Status == "ok") {
                SimulationScenario scenario = resource.Data;
                scenario.Client = this;
                return scenario;
            } else if (resource.Status == "error") {
                throw new ScalarmResourceException<SimulationScenario>(resource);
            } else {
                throw new InvalidResponseException(response);
            }
		}

		/// <summary>
		///  Fetches the model object of Experiment from Scalarm.
		/// Note, that it will represent state of Experiment from time of this method invocation.
		/// To update the state (eg. to check Experiment's state) please use this method again to fetch new data.
		/// </summary>
		/// <returns>The experiment by identifier.</returns>
		/// <param name="experimentId">Experiment identifier.</param>
		/// <typeparam name="T">The class of Experiment. It can be Experiment or SupervisedExperiment.</typeparam>
        public T GetExperimentById<T>(string experimentId) where T : Experiment
        {
			var request = new RestRequest("/experiments/{id}", Method.GET);
			request.AddUrlSegment("id", experimentId);

			var response = this.Execute<ResourceEnvelope<T>>(request);

			ValidateResponseStatus(response);

			var resource = response.Data;

			if (resource.Status == "ok") {
				T experiment = resource.Data;
				experiment.Client = this;
				return experiment;
			} else if (resource.Status == "error") {
				throw new ScalarmResourceException<T>(resource);
			} else {
				throw new InvalidResponseException(response);
			}
        }

        // TODO: handle wrong scenario id
        public Experiment CreateExperimentWithPoints(
            string simulationScenarioId,
            List<ValuesMap> points,
            Dictionary<string, object> additionalParameters = null
            )
        {
			// assuming, that all point objects have the same keys
			var pointsKeys = points[0].Keys;

			string csv = String.Join(",", pointsKeys) + "\n";
			foreach (var point in points) {
				csv += string.Join(",", point.Values) + "\n";
			}

            var request = new RestRequest("experiments/start_import_based_experiment", Method.POST);
			AddAdditionalParameters(request, additionalParameters);

            // Create special usage-parameters for used experiment parameters
			foreach (var expParamName in pointsKeys) request.AddParameter("param_" + expParamName, 1);
            // Add CSV file
            request.AddParameter("parameter_space_file", csv);
            request.AddParameter("simulation_id", simulationScenarioId);

			// TODO: default replication level
			request.AddParameter ("replication_level", 1);

            var response = this.Execute<ExperimentCreationResult>(request);
            return HandleExperimentCreationResponse<Experiment>(response);
        }

		// TODO: handle wrong scenario id
		public SupervisedExperiment CreateSupervisedExperiment(
			string simulationScenarioId,
			Dictionary<string, object> additionalParameters = null
			)
		{
			var request = new RestRequest("experiments/start_supervised_experiment", Method.POST);
			// Add user additional parameters
			if (additionalParameters != null) {
				foreach (var p in additionalParameters) request.AddParameter(p.Key, p.Value);
			}

			request.AddParameter("simulation_id", simulationScenarioId);

			var response = this.Execute<ExperimentCreationResult>(request);
			return HandleExperimentCreationResponse<SupervisedExperiment>(response);
		}

        private T HandleExperimentCreationResponse<T>(IRestResponse<ExperimentCreationResult> response)
			where T : Experiment
        {
            ValidateResponseStatus(response);

            var creationResult = response.Data;

            if (creationResult.status == "ok")
            {
				return (T)Activator.CreateInstance(typeof(T), creationResult.experiment_id, this);
            } else if (creationResult.status == "error") {
                throw new CreateScenarioException(creationResult.message);
            } else {
                throw new InvalidResponseException(response);
            }
        }

        public static void ValidateResponseStatus(IRestResponse response)
        {
            if (response.ResponseStatus != ResponseStatus.Completed) {
                throw new InvalidResponseStatusException(response);
            } else if (response.StatusCode != HttpStatusCode.OK) {
                throw new InvalidHttpStatusCodeException(response);
            }
        }

        public SimulationScenario RegisterSimulationScenario(
              string simulationName,
              string simulationBinariesPath,
              string simulationInputPath,
              string executorPath,
              Dictionary<string, object> attributes = null) {

            var simulationInput = System.IO.File.ReadAllText(simulationInputPath);

            return _registerSimulationScenario(simulationName, simulationBinariesPath, simulationInput, executorPath, attributes);
        }

        public SimulationScenario RegisterSimulationScenario(
              string simulationName,
              string simulationBinariesPath,
              List<ExperimentInput.Parameter> simpleInputDefinition,
              string executorPath,
              Dictionary<string, object> attributes = null) {


            return _registerSimulationScenario(simulationName, simulationBinariesPath,
                                               _createInputDefinition(simpleInputDefinition),
                                               executorPath);
        }

        private string _createInputDefinition(IList<ExperimentInput.Parameter> simpleInputDefinition) {
            var cats = new List<ExperimentInput.Category>() {
                new ExperimentInput.Category(null, null) { Entities = new List<ExperimentInput.Entity>() {
                        new ExperimentInput.Entity(null, null) { Parameters = new List<ExperimentInput.Parameter>() }
                    }}
            };

            foreach (var p in simpleInputDefinition) {
                cats[0].Entities[0].Parameters.Add(p);
            }

            return JsonConvert.SerializeObject(cats);
        }

        public SimulationScenario _registerSimulationScenario(
              string simulationName,
              string simulationBinariesPath,
              string simulationInputDefinition,
              string executorPath,
              Dictionary<string, object> attributes = null) {
              
            var request = new RestRequest("simulations", Method.POST);
                         
            request.AddParameter("simulation_name", simulationName);
              
            request.AddFile("simulation_binaries", simulationBinariesPath);
            request.AddParameter("simulation_input", simulationInputDefinition);
            request.AddFile("executor", executorPath);

            if (attributes != null) {
                foreach (var p in new string[] {"input_writer", "output_reader", "progress_monitor"}) {
                    if (attributes.ContainsKey(p)) {
                        request.AddFile(p, (string)attributes[p]);
                        attributes.Remove(p);
                    }
                }
    
                foreach (var entry in attributes) {
                    request.AddParameter(entry.Key, entry.Value);
                }
            }

            var response = this.Execute<ScenarioCreationResult>(request);
            return HandleScenarioCreationResponse(response);

        }

        private SimulationScenario HandleScenarioCreationResponse(IRestResponse<ScenarioCreationResult> response) {
            ValidateResponseStatus(response);

            var creationResult = response.Data;

            if (creationResult.status == "ok")
            {
                return GetScenarioById(creationResult.simulation_id);
            } else if (creationResult.status == "error") {
                throw new CreateScenarioException(creationResult.message);
            } else {
                throw new InvalidResponseException(response);
            }
        }

        public IList<SimulationManager> ScheduleSimulationManagers(
            string experimentId, string infrastructureName, int jobCounter, IDictionary<string, object> additionalParams = null) {
            
            var request = new RestRequest("infrastructure/schedule_simulation_managers", Method.POST);
            
            request.AddParameter("experiment_id", experimentId);
            request.AddParameter("infrastructure_name", infrastructureName);
            request.AddParameter("job_counter", jobCounter);
        
			AddAdditionalParameters(request, additionalParams);
            
            return HandleScheduleSimulationManagerResult(this.Execute<ScheduleSimulationManagersResult>(request));
        }

        private IList<SimulationManager> HandleScheduleSimulationManagerResult(IRestResponse<ScheduleSimulationManagersResult> response) {
            ValidateResponseStatus(response);

            var scheduleResult = response.Data;

            if (scheduleResult.status == "ok")
            {
                return GetSimulationManagersByIds(scheduleResult.records_ids, scheduleResult.infrastructure);
            } else if (scheduleResult.status == "error") {
                throw new ScheduleSimulationManagerException(scheduleResult.error_code,
                                                             scheduleResult.msg);
            } else {
                throw new InvalidResponseException(response);
            }
        }

        // TODO: swap parameters order
        public string GetResourceStatus(string infrastructureName, string id)
        {
            // TODO
            throw new NotImplementedException();
        }

        public List<SimulationManager> GetSimulationManagersByIds(IList<string> ids, string infrastructure)
        {
            // TODO: add method in Scalarm to handle queries with multiple ids
            return ids.Select(id => this.GetSimulationManagerById(id, infrastructure)).ToList();
        }

        public SimulationManager GetSimulationManagerById(string recordId, string infrastructure)
        {
            var request = new RestRequest("/simulation_managers/{id}", Method.GET);
            request.AddUrlSegment("id", recordId);
            request.AddParameter("infrastructure", infrastructure);

            var response = this.Execute<SimulationManagerResource>(request);

            ValidateResponseStatus(response);

            return HandleGetSimulationManagerResult(response);
        }

		/// <summary>
		/// Gets all simulation managers objects.
		/// </summary>
		/// <param name="additionalParams">Optional params for request:
		/// 	- string infrastructure - Get objects only for specified infrastructure name
		/// 	- string experiment_id - Get objects only for experiment with specified ID
		/// 	- IList<String> states - Get objects only in specified states, allowed: created, initializing, running, terminating, error
		/// 	- IList<String> states_not - Get objects that are not in specified states, allowed: created, initializing, running, terminating, error
		/// 	- bool onsite_monitoring - Get objects that are onsite_monitored (defatult: true)
		/// </param>
		/// <returns>List of SimulationManager objects.</returns>
		/// <param name="additionalParams">Additional parameters.</param>
		public IList<SimulationManager> GetAllSimulationManagers(IDictionary<string, object> additionalParams = null)
		{
			var request = new RestRequest("/simulation_managers", Method.GET);

			AddAdditionalParameters(request, additionalParams);

			var response = this.Execute<SimulationManagersList>(request);

			ValidateResponseStatus(response);

			return HandleGetSimulationManagersResult(response);

		}

		private IList<SimulationManager> HandleGetSimulationManagersResult(IRestResponse<SimulationManagersList> response)
		{
			var data = response.Data;

			if (data.status == "ok") {
				List<SimulationManager> simulationManagers = data.sm_records;
				foreach (SimulationManager sim in simulationManagers) {
					sim.Client = this;
				}
				return simulationManagers;
			} else {
				throw new InvalidResponseException(response);
			}
		}

        private SimulationManager HandleGetSimulationManagerResult(IRestResponse<SimulationManagerResource> response)
        {
            var resource = response.Data;

            if (resource.status == "ok") {
                SimulationManager simulationManager = resource.record;
                simulationManager.Client = this;
                return simulationManager;
                // TODO
//            } else if (resource.Status == "error") {
//                throw new ScalarmResourceException<SimulationScenario>(resource);
            } else {
                throw new InvalidResponseException(response);
            }
        }

        public ExperimentStatistics GetExperimentStatistics(string experimentId)
        {
            var request = new RestRequest("/experiments/{id}/experiment_stats", Method.GET);
            request.AddUrlSegment("id", experimentId);

            var response = this.Execute<ExperimentStatistics>(request);

            ValidateResponseStatus(response);

            return HandleExperimentsStatisticsResponse(response);
        }

        // TODO: envelope needed?
        private ExperimentStatistics HandleExperimentsStatisticsResponse(IRestResponse<ExperimentStatistics> response)
        {
            return response.Data;
        }

		public IList<ValuesMap> GetExperimentResults(string experimentId)
		{
			var request = new RestRequest("/experiments/{id}/file_with_configurations", Method.GET);
			request.AddUrlSegment("id", experimentId);

			var response = this.Execute(request);

			ValidateResponseStatus(response);

			return ParseExperimentsConfigurationsCsv(response.Content);
		}

		public static IList<ValuesMap> ParseExperimentsConfigurationsCsv(string responseCsv)
		{
            string[] lines = responseCsv.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);

			string[] headers = lines[0].Split(',');

			// TODO: a memory usage disaster!
			var recordsList = new List<ValuesMap>();

			for (int lineIndex=1; lineIndex<lines.Length; ++lineIndex) {
				var line = lines[lineIndex];
				var values = line.Split(',');

				// TODO: check values count in each line?
				if (values.Length > 0 && values[0] != "") {
					var record = new ValuesMap();

					for (int valIndex=0; valIndex<values.Length; ++valIndex) {
						record.Add(headers [valIndex], values [valIndex]);
					}

					recordsList.Add(record);
				}
			}

			return recordsList;
		}

		public IList<T> GetInfrastructureCredentials<T>(string infrastructureName, IDictionary<string, object> queryParams = null)
			where T : InfrastructureCredentials
		{
			var request = new RestRequest("/infrastructure/get_infrastructure_credentials", Method.GET);
			request.AddParameter("infrastructure", infrastructureName);
			AddAdditionalParameters(request, queryParams);

			var response = this.Execute<ResourceEnvelope<List<T>>>(request);

			ValidateResponseStatus(response);

			var resource = response.Data;

			if (resource.Status == "ok") {
				List<T> credentialsList = resource.Data;
				foreach (T c in credentialsList) {
					c.Client = this;
				}
				return credentialsList;
			} else {
				throw new InvalidResponseException(response);
			}

		}

		public T AddInfrastructureCredentials<T>(string infrastructureName, IDictionary<string, object> additionalParams = null)
			where T : InfrastructureCredentials
		{
			var request = new RestRequest("/infrastructure/add_infrastructure_credentials", Method.POST);
			request.AddParameter("infrastructure_name", infrastructureName);
			AddAdditionalParameters(request, additionalParams);

			var response = this.Execute<AddCredentialsResult>(request);

			ValidateResponseStatus(response);

			var resource = response.Data;

			if (resource.status == "ok") {
				return GetInfrastructureCredentials<T>(infrastructureName, new Dictionary<string, object> () {
					{"id", resource.record_id}
				}) [0];
			} else if (resource.status == "error" && resource.error_code == "invalid") {
				throw new CredentialsValidationException(resource.record_id);
			} else {
				throw new InvalidResponseException(response);
			}
		}

		public PrivateMachineCredentials AddPrivateMachineCredentials(string host, string login, string password, int port=22)
		{
			return AddInfrastructureCredentials<PrivateMachineCredentials>("private_machine", new Dictionary<string, object> () {
				{"host", host},
				{"login", login},
				{"secret_password", password},
				{"port", port},
			});
		}

		public IList<PrivateMachineCredentials> GetPrivateMachineCredentials(IDictionary<string, object> queryParams)
		{
			return GetInfrastructureCredentials<PrivateMachineCredentials>("private_machine", queryParams);
		}

		public IList<PrivateMachineCredentials> GetPrivateMachineCredentials(string host=null, string login=null, int port=-1)
		{
			var query = new Dictionary<string, object> ();

			if (host != null)
				query.Add("host", host);

			if (login != null)
				query.Add("login", login);

			if (port > 0)
				query.Add("port", port);

			return GetPrivateMachineCredentials(query);
		}

		/// <summary>
		/// Helper method to add any parameters to request. It supports C# Arrays as a parameter that are converted to REST paramteer arrays.
		/// TODO: support Dictionaries
		/// </summary>
		/// <param name="request">The RestRequest that will be modified by adding params. Cannot be null.</param>
		/// <param name="parameters">Dictionary of HTTP parameters. If null, method does nothing.</param>
		public static void AddAdditionalParameters(IRestRequest request, IDictionary<string, object> parameters)
		{
			if (parameters != null) {
				foreach (var p in parameters) {
					if (!(p.Value is string) && p.Value is IEnumerable) {
						string arrayName = string.Format("{0}[]", p.Key);
						foreach (string arrayValue in (p.Value as IEnumerable)) {
							request.AddParameter(arrayName, arrayValue);
						}
					} else {
						request.AddParameter(p.Key, p.Value);
					}
				}
			}
		}
    }

}

