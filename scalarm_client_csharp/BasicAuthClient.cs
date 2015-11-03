using System;
using RestSharp.Authenticators;

namespace Scalarm
{
	public class BasicAuthClient : Client
	{
		public BasicAuthClient (string baseUrl, string login, string password) : base(baseUrl)
		{
			this.Authenticator = new HttpBasicAuthenticator(login, password);
		}
	}
}

