using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Scalarm.ExperimentInput;
using System.Threading;
using System.Linq;
using System.Collections;
using System.ComponentModel;
using RestSharp.Deserializers;

namespace Scalarm
{	
	public delegate void ExperimentCompletedEventHandler(object sender, IList<SimulationParams> results);
	public delegate void NoResourcesEventHandler(object sender); // TODO: should got failed simulation managers list

	public class Experiment : ScalarmObject
	{
		public event ExperimentCompletedEventHandler ExperimentCompleted;
		public event NoResourcesEventHandler NoResources;

		private int _watchingIntervalMillis = 5000;
		public int WatchingIntervalSecs {
			get {
				return _watchingIntervalMillis;
			}
			set {
				_watchingIntervalMillis = value * 1000;
			}
		}

		// TODO support for parameter constraints

		#region model

		[DeserializeAs(Name = "_id")]
        public string Id {get; protected set;}

		[DeserializeAs(Name = "name")]
		public string Name {get; protected set;}

		[DeserializeAs(Name = "description")]
		public string Description {get; protected set;}

		[DeserializeAs(Name = "simulation_id")]
		public string SimulationId {get; protected set;}

		[DeserializeAs(Name = "is_running")]
		public bool IsRunning {get; protected set;}

		[DeserializeAs(Name = "replication_level")]
		public int ReplicationLevel {get; protected set;}

		[DeserializeAs(Name = "time_constraint_in_sec")]
		public int TimeConstraintSec {get; protected set;}

		[DeserializeAs(Name = "start_at")]
		public DateTime StartAt {get; protected set;}

		[DeserializeAs(Name = "user_id")]
		public string UserId {get; protected set;}

		[DeserializeAs(Name = "scheduling_policy")]
		public string SchedulingPolicy {get; protected set;}

		[DeserializeAs(Name = "experiment_input")]
		public List<Category> InputSpecification { get; set; }

		[DeserializeAs(Name = "size")]
		public int Size {get; protected set;}

		[DeserializeAs(Name = "supervised")]
		public bool IsSupervised {get; protected set;}

		#endregion

		public Experiment()
		{
		}

        public Experiment(string experimentId, Client client) : base(client)
        {
            Id = experimentId;
        }

		/// <summary>
		/// Get and save experiment binary package in .zip format.
		/// </summary>
		/// <param name="path">Local path to save results (.zip file will be created)</param>
		public virtual void GetBinaryResults(string path)
		{
			Client.GetExperimentBinaryResults(Id, path);
		}

		/// <summary>
		/// Get and save simulation run binary results in .tar.gz format for given simulation run index.
		/// </summary>
		/// <param name="path">Local path to save results (.tar.gz file will be created)</param>
		public virtual void GetSimulationRunBinaryResult(int simulationRunIndex, string path)
		{
			Client.GetSimulationRunBinaryResult(Id, simulationRunIndex, path);
		}

		protected virtual void OnExperimentCompleted(EventArgs e)
		{
			// TODO not this this
			if (ExperimentCompleted != null) ExperimentCompleted(this, GetResults());
		}

		protected virtual void OnNoResources(EventArgs e)
		{
			if (NoResources != null) NoResources(this); // TODO: should send failed SiM list
		}

		public virtual IList<SimulationManager> ScheduleSimulationManagers(string infrastructure, int count, IDictionary<string, object> parameters = null) {
            return Client.ScheduleSimulationManagers(Id, infrastructure, count, parameters);
        }

		public virtual IList<SimulationManager> ScheduleZeusJobs(int count, string plgridLogin, string plgridPassword)
        {
			var reqParams = new Dictionary<string, object> {
				{"time_limit", "60"}
			};

			if (plgridPassword == null) {
				new ArgumentNullException ("PL-Grid password must not be null");
			}
			reqParams ["plgrid_login"] = plgridLogin;
			reqParams ["plgrid_password"] = plgridPassword;
			reqParams ["onsite_monitoring"] = true;

            return ScheduleSimulationManagers("qsub", count, reqParams);
        }

		public virtual IList<SimulationManager> ScheduleZeusJobs(int count, IDictionary<string, object> parameters = null)
		{
			// default time limit
			var reqParams = new Dictionary<string, object> {
				{"time_limit", "60"}
			};

			// TODO: this is not true is user has credentials saved in Scalarm DB
			if (!(Client is ProxyCertClient)) {
				throw new Exception ("If not using ProxyCertClient, login and password should be used.");
			}

			if (parameters != null) {
				foreach (var param in parameters) {
					reqParams[param.Key] = param.Value;
				}
			}

			reqParams["onsite_monitoring"] = true;

			return ScheduleSimulationManagers("qsub", count, reqParams);
		}

		public virtual IList<SimulationManager> SchedulePrivateMachineJobs(int count, PrivateMachineCredentials credentials)
		{
			var reqParams = new Dictionary<string, object> {
				{"time_limit", "60"},
				{"credentials_id", credentials.Id}
			};

			return ScheduleSimulationManagers("private_machine", count, reqParams);
		}

		public virtual IList<SimulationManager> SchedulePrivateMachineJobs(int count, string credentialsId)
		{
			var reqParams = new Dictionary<string, object> {
				{"time_limit", "60"},
				{"credentials_id", credentialsId}
			};

			return ScheduleSimulationManagers("private_machine", count, reqParams);
		}

		/// <summary>
		///  Schedule jobs on PL-Grid for this experiment using PL-Grid UI login, password and Grid Certificate passphrase.
		/// </summary>
		/// <returns>The pl grid jobs.</returns>
		/// <param name="plgridCe">Target Computing Engine (cluster). Allowed values are stored in PLGridCE class. If null, "zeus.cyfronet.pl" is used.</param>
		/// <param name="count">How many jobs should be created (parallel computations).</param>
		public virtual IList<SimulationManager> SchedulePlGridJobs(string plgridCe, int count, string plgridLogin, string plgridPassword, string keyPassphrase)
		{
			var reqParams = DefaultQcgScheduleParams();

			if (plgridCe != null) {
				reqParams ["plgrid_host"] = plgridCe;
			}

			if (plgridLogin == null || plgridPassword == null || keyPassphrase == null) {
				new ArgumentNullException ("PL-Grid login, password and keyPassphrase should be provided");
			} else {
				reqParams ["plgrid_login"] = plgridLogin;
				reqParams ["plgrid_password"] = plgridPassword;
				reqParams ["key_passphrase"] = keyPassphrase;
			}

			return ScheduleSimulationManagers("qcg", count, reqParams);
		}

		/// <summary>
		///  Schedule jobs on PL-Grid for this experiment using externally loaded PL-Grid Proxy Certificate string.
		/// NOTICE: if using ProxyCertClient, please use SchedulePlGridJobs(string plgridCe, int count) method!
		/// </summary>
		/// <returns>The pl grid jobs.</returns>
		/// <param name="plgridCe">Target Computing Engine (cluster). Allowed values are stored in PLGridCE class. If null, "zeus.cyfronet.pl" is used.</param>
		/// <param name="count">How many jobs should be created (parallel computations).</param>
		public virtual IList<SimulationManager> SchedulePlGridJobs(string plgridCe, int count, string plgridProxy)
		{
			var reqParams = DefaultQcgScheduleParams();

			if (plgridCe != null) {
				reqParams ["plgrid_host"] = plgridCe;
			}

			reqParams ["proxy"] = plgridProxy;

			return ScheduleSimulationManagers("qcg", count, reqParams);
		}

		/// <summary>
		///  Schedule jobs on PL-Grid for this experiment using proxy certificate held by associated Client.
		///  Notice that this method can be used only with ProxyCertClient!
		/// </summary>
		/// <returns>The pl grid jobs.</returns>
		/// <param name="plgridCe">Target Computing Engine (cluster). Allowed values are stored in PLGridCE class. If null, "zeus.cyfronet.pl" is used.</param>
		/// <param name="count">How many jobs should be created (parallel computations).</param>
		public virtual IList<SimulationManager> SchedulePlGridJobs(string plgridCe, int count)
		{
			if (!(Client is ProxyCertClient)) {
				throw new Exception ("If not using ProxyCertClient, login and password or explicit proxy should be used.");
			}

			var reqParams = DefaultQcgScheduleParams();

			if (plgridCe != null) {
				reqParams ["plgrid_host"] = plgridCe;
			}

			return ScheduleSimulationManagers("qcg", count, reqParams);
		}

		protected IDictionary<string, object> DefaultQcgScheduleParams()
		{
			return new Dictionary<string, object> {
				{"time_limit", "60"},
				{"onsite_monitoring", true},
				{"plgrid_host", PLGridCE.ZEUS}
			};
		}

		public virtual ExperimentStatistics GetStatistics()
        {
            return Client.GetExperimentStatistics(Id);
        }

		public virtual bool IsDone()
        {
            var stats = GetStatistics();
			Console.WriteLine("DEBUG: exp stats: " + stats.ToString());
            return stats.All == stats.Done;
        }

        // TODO: check if there are workers running - if not - throw exception!
        /// <summary>
        ///  Actively waits for experiment for completion. 
        /// </summary>
		public virtual void WaitForDone(int timeoutSecs=-1, int pollingIntervalSeconds=5)
        {
            var startTime = DateTime.UtcNow;

            while (timeoutSecs <= 0 || (DateTime.UtcNow - startTime).TotalSeconds < timeoutSecs) {
				if (IsDone()) {
					return;
				} else if (!GetActiveSimulationManagers().Any()) {
					throw new NoActiveSimulationManagersException();
				}
                Thread.Sleep(pollingIntervalSeconds*1000);
            }
            throw new TimeoutException();
    	}

		private BackgroundWorker _worker;

		public virtual void StartWatching()
		{
			if (_worker == null) {
				_worker = new BackgroundWorker();
				_worker.WorkerSupportsCancellation = true;
				_worker.WorkerReportsProgress = false;
				_worker.DoWork += _watchCompletion;
				_worker.RunWorkerCompleted += _workerCompleted;
			}

			if (!_worker.IsBusy) {
				_worker.RunWorkerAsync();
			}
		}

		public virtual void StopWatching()
		{
			if (_worker != null) {
				_worker.CancelAsync();
			}
		}

		private void _watchCompletion(object sender, DoWorkEventArgs e)
		{
			BackgroundWorker worker = sender as BackgroundWorker;

			Console.WriteLine("Starting experiment watching thread");
			while (!worker.CancellationPending && !IsDone()) {
				if (!GetActiveSimulationManagers().Any()) {
					throw new NoActiveSimulationManagersException();
				}
				Thread.Sleep(_watchingIntervalMillis);
			}

			if (worker.CancellationPending) {
				e.Cancel = true;
			}
		}

		private void _workerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			if (!e.Cancelled) {
				if (e.Error == null) {
					OnExperimentCompleted(EventArgs.Empty);
				} else if (e.Error is NoActiveSimulationManagersException) {
					OnNoResources(EventArgs.Empty);
				} else {
					throw e.Error;
				}
			}
		}

		public virtual IList<Scalarm.SimulationParams> GetResults(Boolean fetchFailed = false)
		{
			var options = new GetResultsOptions() { WithStatus = fetchFailed };
			return GetResults(options);
		}

		// TODO: parse json to resolve types?
		// <summary>
		//  Gets results in form od Dictionary: input parameters -> MoEs
		//  Input parameters and MoEs are in form of dictionaries: id -> value; both keys and values are string!
		// </summary>
		public virtual IList<SimulationParams> GetResults(GetResultsOptions options)
		{
			// TODO: iterate all this experiment's SimulationParams and fill results to outputs

			IList<ValuesMap> results = Client.GetExperimentResults(this.Id, options);
			IList<string> parametersIds = InputDefinition.ParametersIdsForCategories(InputSpecification);

			IList<SimulationParams> finalResults = MakeResults(ConvertTypes(results), parametersIds);

			return finalResults;
		}

		// TODO: can modify results, use with caution
		public static IList<ValuesMap> ConvertTypes(IList<ValuesMap> results)
		{
			var convertedResults = new List<ValuesMap>();
			foreach(var item in results) {
				convertedResults.Add(item);
			}

			foreach (var record in convertedResults) {
                // http://stackoverflow.com/a/18288740/1367361
                List<string> keys = new List<string>(record.Keys);
				foreach(string key in keys) {

					// TODO: check with string values - probably there will bo problem with deserializing because lack of ""
					try {

						record[key] = JsonConvert.DeserializeObject(record[key].ToString());
						// exception happen when evaluate toString method on String object -> wrong Deserialize

					} catch (Newtonsoft.Json.JsonReaderException e) {
						//do nothing - it is already a String 
					}
				}

                
			}

			return convertedResults;
		}

		/// <summary>
		/// Using CSV-like results fetched from Scalarm (list of ValuesMap) and knowning input parameter ids,
		/// create a list of SimulationParams, which have Input and Output fields that contain values maps
		/// splitted into input parameters and output (results).
		/// </summary>
		/// <returns>List of SimulationParams objects. Each is a result from single simulation run.</returns>
		/// <param name="results">Results fetched from Scalarm in form of list of ValuesMap (see GetResults HTTP method).</param>
		/// <param name="parametersIds">Ids of input parameters for disinguishing input parameters from outputs.</param>
		public virtual IList<SimulationParams> MakeResults(IList<ValuesMap> results, IList<string> parametersIds)
		{
			List<SimulationParams> convertedResults = new List<SimulationParams>();

			foreach (var result in results) {
				ValuesMap input = new ValuesMap();
				ValuesMap output = new ValuesMap();

				SimulationParams singleResult = new SimulationParams(input, output);

				foreach (string id in result.Keys) {
					if (parametersIds.Contains(id)) {
						singleResult.Input.Add(id, result[id]);
					} else {
						singleResult.Output.Add(id, result[id]);
					}
				}

				convertedResults.Add(singleResult);
			}

			return convertedResults;
		}

		/// <summary>
		/// Gets simulation managers for this Experiment.
		/// </summary>
		/// <returns>All simulation managers associated with this Experiment.</returns>
		/// <param name="additionalParams">Additional query parameters.
		/// See additionalParams for Client.GetAllSimulationManagers for details (except for experiment_id).</param>
		public virtual IList<SimulationManager> GetSimulationManagers(IDictionary<string, object> additionalParams = null)
		{
			if (additionalParams == null) {
				additionalParams = new Dictionary<string, object>();
			}
			additionalParams.Add("experiment_id", this.Id);
			return Client.GetAllSimulationManagers(additionalParams);
		}

		public virtual IList<SimulationManager> GetActiveSimulationManagers()
		{
			return GetSimulationManagers(new Dictionary<string, object>() {
				{"states_not", new string[] {"error", "terminating"}}
			});
		}

//		// TODO: this should be method for "Results" object?
//		public ValuesMap GetSingleResult(ValuesMap point)
//		{
//			var results = GetResults();
//			return results[point];
//		}
	}

}

