using System;
using System.Net;
using System.Collections.Generic;
using Newtonsoft.Json;
using RestSharp;
using System.Linq;
using RestSharp.Deserializers;

namespace Scalarm
{


	public class SupervisedExperiment : Experiment
	{
		public SupervisedExperiment() : base()
		{}

		public SupervisedExperiment(string experimentId, Client client) : base(experimentId, client)
		{}

		// TODO
//		public void SchedulePoints(List<ValuesMap> points)
//		{
//		}

//		public static implicit operator SupervisedExperiment(Experiment experiment)
//		{
//			// TODO
//			return new SupervisedExperiment();
//		}

		#region model

		// inherit model from Experiment

		[DeserializeAs(Name = "completed")]
		public bool IsCompleted {get; private set;}

		[DeserializeAs(Name = "result")]
		public string Result { get; private set;}

		#endregion

		/// <summary>
		/// Schedules the point.
		/// </summary>
		/// <param name="point">Point.</param>
		/// <returns>Scheduled point index in Scalarm</returns>
		public virtual int SchedulePoint(ValuesMap point)
		{
			var request = new RestRequest(String.Format("experiments/{0}/schedule_point", this.Id), Method.POST);
			request.AddParameter("point", point.ToJson());
			var result = Client.Execute<SchedulePointResult> (request);
			return HandleSchedulePointResponse(result);
		}

		/// <summary>
		/// Schedules the points.
		/// </summary>
		/// <returns>Array of point indexes in order of point in "point" array</returns>
		/// <param name="points">Array with points</param>
		public virtual List<int> SchedulePoints(IEnumerable<ValuesMap> points)
		{
			// TODO: implement multi-point scheduling in Scalarm
			List<int> indexes = new List<int>();
			foreach (ValuesMap point in points) {
				indexes.Add(SchedulePoint(point));
			}
			return indexes;
		}

		private int HandleSchedulePointResponse(IRestResponse<SchedulePointResult> response)
		{
			Client.ValidateResponseStatus(response);

			var dataResult = response.Data;

			if (dataResult.status == "ok")
			{
				return dataResult.index;
			} else if (dataResult.status == "error") {
				throw new SchedulePointException("");
			} else {
				throw new InvalidResponseException(response);
			}
		}

		// TODO: values parameter (point) support
		public virtual void MarkAsComplete(string results, bool success = true, string errorReason = null)
		{
			var request = new RestRequest(String.Format("experiments/{0}/mark_as_complete", this.Id), Method.POST);
			request.AddParameter("status", success ? "ok" : "error");
			request.AddParameter("results", results);
			if (errorReason != null) {
				request.AddParameter("reason", errorReason);
			}

			var result = Client.Execute<ScalarmStatus>(request);

			Client.ValidateResponseStatus(result);
			if (result.Data.status == "ok") {
				return;
			} else {
				// TODO: use ScalarmException
				throw new Exception("Invalid experiment mark as complete result");
			}
		}


	}
}

