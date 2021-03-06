using System;
using System.Net;
using System.Collections.Generic;
using Newtonsoft.Json;
using RestSharp;

namespace Scalarm
{	
    // Resource envelopes are currently experimental in Scalarm REST API
    // and are not used everywhere.
	public class ResourceEnvelope<T>
	{
        [JsonProperty(PropertyName = "status")]
        public string Status {get; set;}

        [JsonProperty(PropertyName = "data")]
        public T Data {get; set;}

        [JsonProperty(PropertyName = "error_code", NullValueHandling = NullValueHandling.Ignore)]
        public string ErrorCode {get; set;}
	}


}

