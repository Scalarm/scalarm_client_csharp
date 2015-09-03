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

		public Dictionary<ValuesMap, ValuesMap> SimulationParamsMap { get; private set; }

		// TODO support for parameter constraints

		#region model

		[DeserializeAs(Name = "_id")]
        public string Id {get; private set;}

		[DeserializeAs(Name = "name")]
		public string Name {get; private set;}

		[DeserializeAs(Name = "description")]
		public string Description {get; private set;}

		[DeserializeAs(Name = "simulation_id")]
		public string SimulationId {get; private set;}

		[DeserializeAs(Name = "is_running")]
		public bool IsRunning {get; private set;}

		[DeserializeAs(Name = "replication_level")]
		public int ReplicationLevel {get; private set;}

		[DeserializeAs(Name = "time_constraint_in_sec")]
		public int TimeConstraintSec {get; private set;}

		[DeserializeAs(Name = "start_at")]
		public DateTime StartAt {get; private set;}

		[DeserializeAs(Name = "user_id")]
		public string UserId {get; private set;}

		[DeserializeAs(Name = "scheduling_policy")]
		public string SchedulingPolicy {get; private set;}

		[DeserializeAs(Name = "experiment_input")]
		public List<Category> InputSpecification { get; set; }

		[DeserializeAs(Name = "size")]
		public int Size {get; private set;}

		[DeserializeAs(Name = "supervised")]
		public bool IsSupervised {get; private set;}

		#endregion

		public Experiment()
		{
			SimulationParamsMap = new Dictionary<ValuesMap, ValuesMap>();
		}

        public Experiment(string experimentId, Client client) : base(client)
        {
            Id = experimentId;
			SimulationParamsMap = new Dictionary<ValuesMap, ValuesMap>();
        }

		public void CreateParamsMap(List<ValuesMap> parameters)
		{
			foreach (var p in parameters) {
				SimulationParamsMap.Add(p, null);
			}
		}

		protected void OnExperimentCompleted(EventArgs e)
		{
			// TODO not this this
			if (ExperimentCompleted != null) ExperimentCompleted(this, GetResults());
		}

		protected void OnNoResources(EventArgs e)
		{
			if (NoResources != null) NoResources(this); // TODO: should send failed SiM list
		}

        public IList<SimulationManager> ScheduleSimulationManagers(string infrastructure, int count, IDictionary<string, object> parameters = null) {
            return Client.ScheduleSimulationManagers(Id, infrastructure, count, parameters);
        }

		public IList<SimulationManager> ScheduleZeusJobs(int count, string plgridLogin, string plgridPassword)
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

		public IList<SimulationManager> ScheduleZeusJobs(int count, IDictionary<string, object> parameters = null)
		{
			// default time limit
			var reqParams = new Dictionary<string, object> {
				{"time_limit", "60"}
			};

			// TODO: this is not true is user has credentials saved in Scalarm DB
			if (!(Client is ProxyCertClient)) {
				throw new Exception ("If not using ProxyCertClient, login and password should be used.");
			}

			foreach (var param in parameters) {
				reqParams[param.Key] = param.Value;
			}

			reqParams["onsite_monitoring"] = true;

			return ScheduleSimulationManagers("qsub", count, reqParams);
		}

		public IList<SimulationManager> SchedulePrivateMachineJobs(int count, PrivateMachineCredentials credentials)
		{
			var reqParams = new Dictionary<string, object> {
				{"time_limit", "60"},
				{"credentials_id", credentials.Id}
			};

			return ScheduleSimulationManagers("private_machine", count, reqParams);
		}

		public IList<SimulationManager> SchedulePrivateMachineJobs(int count, string credentialsId)
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
		public IList<SimulationManager> SchedulePlGridJobs(string plgridCe, int count, string plgridLogin, string plgridPassword, string keyPassphrase)
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
		public IList<SimulationManager> SchedulePlGridJobs(string plgridCe, int count, string plgridProxy)
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
		public IList<SimulationManager> SchedulePlGridJobs(string plgridCe, int count)
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

        public ExperimentStatistics GetStatistics()
        {
            return Client.GetExperimentStatistics(Id);
        }

        public bool IsDone()
        {
            var stats = GetStatistics();
			Console.WriteLine("DEBUG: exp stats: " + stats.ToString());
            return stats.All == stats.Done;
        }

        // TODO: check if there are workers running - if not - throw exception!
        /// <summary>
        ///  Actively waits for experiment for completion. 
        /// </summary>
        public void WaitForDone(int timeoutSecs=-1, int pollingIntervalSeconds=5)
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

		public void StartWatching()
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

		public void StopWatching()
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

		// TODO: parse json to resolve types?
		// <summary>
		//  Gets results in form od Dictionary: input parameters -> MoEs
		//  Input parameters and MoEs are in form of dictionaries: id -> value; both keys and values are string!
		// </summary>
		public IList<SimulationParams> GetResults()
		{
			// TODO: iterate all this experiment's SimulationParams and fill results to outputs

			IList<ValuesMap> results = Client.GetExperimentResults(Id);
			IList<string> parametersIds = InputDefinition.ParametersIdsForCategories(InputSpecification);

			FillSimulationParamsMap(ConvertTypes(results), parametersIds);

			// cast to SimulationParam (list)
			return SimulationParamsMap.ToList().Select(p => (SimulationParams)p).ToList();
		}

		// TODO: can modify results, use with caution
		public static IList<ValuesMap> ConvertTypes(IList<ValuesMap> results)
		{
			var convertedResults = new List<ValuesMap>();
			foreach (var item in results) {
				convertedResults.Add(item);
			}

			foreach (var record in convertedResults) {
                // http://stackoverflow.com/a/18288740/1367361
                List<string> keys = new List<string>(record.Keys);
                foreach (string key in keys)
                {
                    // TODO: check with string values - probably there will bo problem with deserializing because lack of ""
                    record[key] = JsonConvert.DeserializeObject(record[key].ToString());
                }
			}

			return convertedResults;
		}

		public void FillSimulationParamsMap(IList<ValuesMap> results, IList<string> parametersIds)
		{
			foreach (var result in results) {
				var resultDict = result.ShallowCopy();

				var paramsDict = new ValuesMap();

				foreach (string id in parametersIds) {
					if (resultDict.ContainsKey(id)) {
						paramsDict.Add(id, resultDict[id]);
						resultDict.Remove(id);
					}
				}

				if (SimulationParamsMap.ContainsKey(paramsDict)) {
					SimulationParamsMap [paramsDict] = resultDict;
				} else {
					SimulationParamsMap.Add(paramsDict, resultDict);
				}
			}
		}

		/// <summary>
		/// Gets simulation managers for this Experiment.
		/// </summary>
		/// <returns>All simulation managers associated with this Experiment.</returns>
		/// <param name="additionalParams">Additional query parameters.
		/// See additionalParams for Client.GetAllSimulationManagers for details (except for experiment_id).</param>
		public IList<SimulationManager> GetSimulationManagers(IDictionary<string, object> additionalParams = null)
		{
			if (additionalParams == null) {
				additionalParams = new Dictionary<string, object>();
			}
			additionalParams.Add("experiment_id", this.Id);
			return Client.GetAllSimulationManagers(additionalParams);
		}

		public IList<SimulationManager> GetActiveSimulationManagers()
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

