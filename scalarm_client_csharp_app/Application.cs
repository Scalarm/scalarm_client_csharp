using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Scalarm.ExperimentInput;
using RestSharp.Deserializers;
using System.Threading;

namespace Scalarm
{
	public class Application
	{	
		public static bool ShouldWait = true;

		public static void ShowResults(object sender, IList<SimulationParams> results)
		{
            Experiment experiment = sender as Experiment;
			Console.WriteLine(string.Format("Experiment with id {0} done! Results:", experiment.Id));
			foreach (var r in results) {
				Console.WriteLine(string.Format("{0} -> {1}", r.Input, r.Output));
			}

			ShouldWait = false;
		}

		public static void HandleNoResources(object sender)
		{
			Experiment experiment = sender as Experiment;
			Console.WriteLine(string.Format("Experiment with id {0} has no resources!", experiment.Id));

			ShouldWait = false;
		}

		// private static List<ValuesMap> points = new List<ValuesMap>();

		public static string ReadString(string prompt)
		{
			Console.Write(prompt + " ");
			return Console.ReadLine();
		}

		public static string ReadPassword(string prompt)
		{
			string pass = "";
			Console.Write(prompt + " ");
			ConsoleKeyInfo key;

			do
			{
				key = Console.ReadKey(true);

				// Backspace Should Not Work
				if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
				{
					pass += key.KeyChar;
					Console.Write("*");
				}
				else
				{
					if (key.Key == ConsoleKey.Backspace && pass.Length > 0)
					{
						pass = pass.Substring(0, (pass.Length - 1));
						Console.Write("\b \b");
					}
				}
			}
			// Stops Receving Keys Once Enter is Pressed
			while (key.Key != ConsoleKey.Enter);

			Console.WriteLine("\n");

			return pass;
		}

		static private Random RAND = new Random((Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds);

		static public int GetRandomNumber(int n)
		{
			return RAND.Next(n);
		}

		public class ScalarmAppConfig
		{
			public string base_url = null;
			public string login = null;
			public string password = null;
			public string plgrid_login = null;
			public string proxy_path = null;
			public string experiment_id = null;
		}

		public static ScalarmAppConfig ReadConfig(string path)
		{
			string configText = System.IO.File.ReadAllText(path);
			return JsonConvert.DeserializeObject<ScalarmAppConfig> (configText);
		}

        public static Client CreateClient(ScalarmAppConfig config)
        {
			if (String.IsNullOrEmpty (config.proxy_path)) {
				return new BasicAuthClient (config.base_url, config.login, config.password);
			} else {
				return new ProxyCertClient (config.base_url, new FileStream (config.proxy_path, FileMode.Open));
			}					
        }

		static void Main()
		{
//			new ExampleFullRegister().Run();

			var exampleGetInfo = new ExampleGetInfo();
			exampleGetInfo.ExperimentId = "57aadf5a4269a818208da4c2";
			exampleGetInfo.SimulationRunId = 288;
			exampleGetInfo.Run();
		}

	}
}

