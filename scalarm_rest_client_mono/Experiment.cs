using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Newtonsoft.Json;
using Scalarm.ExperimentInput;
using System.Threading;
using System.Linq;
using System.Collections;

namespace Scalarm
{	
	public class Experiment : ScalarmObject
	{
        // TODO: make full experiment model

        public string ExperimentId {get; private set;}

		// TODO: it should be retrieved with experiment data from controller
		public List<Category> InputSpecification { get; set; }

        public Experiment(string experimentId, Client client) : base(client)
        {
            ExperimentId = experimentId;
        }

        public List<SimulationManager> ScheduleSimulationManagers(string infrastructure, int count, Dictionary<string, string> parameters) {
            return Client.ScheduleSimulationManagers(ExperimentId, infrastructure, count, parameters);
        }

        public List<SimulationManager> ScheduleZeusJobs(int count)
        {
            return ScheduleSimulationManagers("qsub", count, new Dictionary<string, string> {
                {"time_limit", "60"}
            });
        }

        public ExperimentStatistics GetStatistics()
        {
            return Client.GetExperimentStatistics(ExperimentId);
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
                }
                Thread.Sleep(pollingIntervalSeconds*1000);
            }
            throw new TimeoutException();
    	}

		// TODO: parse json to resolve types?
		// <summary>
		//  Gets results in form od Dictionary: input parameters -> MoEs
		//  Input parameters and MoEs are in form of dictionaries: id -> value; both keys and values are string!
		// </summary>
		public IDictionary<ValuesMap, ValuesMap> GetResults()
		{
			var results = Client.GetExperimentResults(ExperimentId);
			var parametersIds = InputDefinition.ParametersIdsForCategories(InputSpecification);

			return SplitParametersAndResults(ConvertTypes(results), parametersIds);
		}

		// TODO: can modify results, use with caution
		public static IList<ValuesMap> ConvertTypes(IList<ValuesMap> results)
		{
			var convertedResults = new List<ValuesMap>();
			foreach (var item in results) {
				convertedResults.Add(item);
			}

			foreach (var record in convertedResults) {
				foreach (var singleResult in record) {
					// TODO: check with string values - probably there will bo problem with deserializing because lack of ""
					record [singleResult.Key] = JsonConvert.DeserializeObject(singleResult.Value.ToString());
				}
			}

			return convertedResults;
		}

		public static IDictionary<ValuesMap, ValuesMap> SplitParametersAndResults(IList<ValuesMap> results, IList<string> parametersIds)
		{
			var finalDict = new Dictionary<ValuesMap, ValuesMap>();

			foreach (var result in results) {
				var resultDict = result.ShallowCopy();

				var paramsDict = new ValuesMap();

				foreach (string id in parametersIds) {
					if (resultDict.ContainsKey(id)) {
						paramsDict.Add(id, resultDict[id]);
						resultDict.Remove(id);
					}
				}
				finalDict.Add(paramsDict, resultDict);
			}

			return finalDict;
		}

		// TODO: this should be method for "Results" object?
		public ValuesMap GetSingleResult(ValuesMap point)
		{
			var results = GetResults();
			return results[point];
		}
	}

}

