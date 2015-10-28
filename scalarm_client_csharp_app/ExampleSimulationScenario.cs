using Scalarm.ExperimentInput;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Dynamic.Utils;

namespace Scalarm
{
	public class ExampleSimulationScenario
	{

		public void Run()
		{
			var config = Application.ReadConfig ("config.json");
			var client = Application.CreateClient (config);

			try
			{

				//List<string> simulation_scenarios = client.GetSimulationScenarioIds();
				//Console.WriteLine("Got simulation scenarios ids: {0}",string.Join(",",simulation_scenarios));

				//List<string>  experiments_ids =  client.GetSimulationScenarioExperiments("<simulation_scenario_id>);

				//Console.WriteLine("Got simulation scenario experiment: {0}", string.Join(",",experiments_ids));

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

