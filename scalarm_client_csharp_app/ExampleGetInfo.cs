﻿using Scalarm.ExperimentInput;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Dynamic.Utils;

namespace Scalarm
{
	/// <summary>
	/// Example of getting info/results of exising experiment
	/// </summary>
	public class ExampleGetInfo
	{
		public string ExperimentId { get; set; }
		public int SimulationRunId { get; set; }
		public string ResultsSavePath { get; set; }

		public ExampleGetInfo() 
		{
			ResultsSavePath = "/tmp/scalarm_experiment_results.zip";
		}

		public void Run()
		{
			if (ExperimentId == null) {
				throw new Exception("ExperimentId not provided to ExampleGetInfo instance");
			}

			var config = Application.ReadConfig ("config.json");
			var client = Application.CreateClient(config);

			try
			{
				Experiment experiment = client.GetExperimentById<Experiment>(ExperimentId);

				Console.WriteLine("Got experiment with id: {0}, name: {1}, started at: {2}", experiment.Id, experiment.Name, experiment.StartAt);

				GetResultsOptions resultsOptions = new GetResultsOptions() {
					WithIndex = true,
					WithParams = true,
					WithMoes = true,
					WithStatus = true,
					MinIndex = 1,
					MaxIndex = 1000
				};

				IList<SimulationParams> results = experiment.GetResults(resultsOptions);


				experiment.GetBinaryResults(ResultsSavePath);
				foreach (SimulationParams r in results) {
					int index = int.Parse(r.Output["simulation_index"].ToString());
					string path = String.Format("/tmp/result-{0}.tar.gz", index);
					Console.WriteLine("Get results for simulation run {0} to: {1}", index, path);
					experiment.GetSimulationRunBinaryResult(index, path);
				}

				Console.WriteLine("Binary experiment results saved to: {0}", ResultsSavePath);
			}
			catch (RegisterSimulationScenarioException e)
			{
				Console.WriteLine("Registering simulation scenario failed: " + e);
			}
			catch (CreateScenarioException e)
			{
				Console.WriteLine("Creating experiment failed: " + e);
			}
			catch (InvalidResponseException e)
			{
				Console.WriteLine("Invalid response: {0};\n\n{1};\n\n{2}", e.Response.Content, e.Response.ErrorMessage, e.Response.ErrorException);
			}
			catch (ScalarmResourceException<SimulationScenario> e)
			{
				Console.WriteLine("Error getting Scalarm SimulationScenario resource: {0}", e.Resource.ErrorCode);
			}
		}
	}
}
