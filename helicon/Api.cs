
using System;
using System.Net;
using System.Web;
using System.Xml;
using IronRockUtils.Json;
using System.Collections.Specialized;
using System.Collections.Generic;

namespace helicon
{
	/// <summary>
	/// The consumer api is a static class to provide access globally to any other class without the need
	/// of unnecessary instantiations.
	/// </summary>
	public static class Api
	{
		public static int responseCode;

		/// <summary>
		/// Query string built using the addRequestField() method.
		/// </summary>
		public static string query;
		public static string multipartQuery;
		public static string multipartBoundary;
		public static bool addedFiles;

		/// <summary>
		/// Cookie fields obtained from last request.
		/// </summary>
		public static NameValueCollection cookies = new NameValueCollection();
		
		/// <summary>
		/// Output headers.
		/// </summary>
		public static Dictionary<string, object> outHeaders = new Dictionary<string, object>();

		/// <summary>
		/// XML response obtained from the server.
		/// </summary>
		public static XmlDocument response;

		/// <summary>
		/// JSON response obtained from the server.
		/// </summary>
		public static JsonElement jsonResponse;

		/// <summary>
		/// Error string from connection exceptions.
		/// </summary>
		public static string errstr;

		/// <summary>
		/// Clears the request query string.
		/// </summary>
		public static void clearRequest()
		{
			query = "";
			addedFiles = false;
			multipartBoundary = "MK8SHF618234JXKXABVHS76SL5L6NSAJS8D6VVA8283AOSD87CKA";
			multipartQuery = "";
		}

		/// <summary>
		/// Clears the cookie list.
		/// </summary>
		public static void clearCookies()
		{
			cookies.Clear();
		}

		/// <summary>
		/// Adds a request field to the current request query.
		/// </summary>
		/// <param name="name">Name of the field to add.</param>
		/// <param name="value">Value of the field.</param>
		public static void addRequestField (String name, String value)
		{
			query += "&" + HttpUtility.UrlEncode(name) + "=" + HttpUtility.UrlEncode(value);

			multipartQuery += "--" + multipartBoundary + "\r\n";
			multipartQuery += "Content-Disposition: form-data; name='" + name + "'\r\n";
			multipartQuery += "\r\n";
			multipartQuery += value + "\r\n";
		}

		public static void addRequestFile (String name, String filename, byte[] data)
		{
			addedFiles = true;

			multipartQuery += "--" + multipartBoundary + "\r\n";
			multipartQuery += "Content-Disposition: form-data; name='" + name + "'; filename='" + filename + "'\r\n";
			multipartQuery += "Content-Type: application/octet-stream\r\n";
			multipartQuery += "Content-Transfer-Encoding: binary\r\n";

			multipartQuery += "\r\n";
			multipartQuery += System.Text.Encoding.GetEncoding(1252).GetString(data);

			multipartQuery += "\r\n";
		}

		/// <summary>
		/// Adds a request field to the current request query.
		/// </summary>
		/// <param name="name">Name of the field to add.</param>
		/// <param name="value">Value of the field.</param>
		public static void addRequestField (String name, String value, bool noEncode)
		{
			if (noEncode)
				query += "&" + name + "=" + value;
			else
				query += "&" + HttpUtility.UrlEncode(name) + "=" + HttpUtility.UrlEncode(value);
		}

		/// <summary>
		/// Executes an API call with the parameters specified in the current request query and returns
		/// the response string.
		/// </summary>
		public static string executeRequest(string url, string method, bool decodeXml)
		{
			string result = "";
			errstr = "";

			//System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
			multipartQuery += "--" + multipartBoundary + "--\r\n\r\n";

			try {
				WebClient netClient = new WebClient();

				netClient.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:103.0) Gecko/20100101 Firefox/103.0");

				string cookieHeader = "";

				foreach (string cookieName in cookies.AllKeys) {
					cookieHeader += cookieName + "=" + cookies[cookieName] + "; ";
				}

				if (cookieHeader.Length > 0)
					netClient.Headers["Cookie"] = cookieHeader;

				if (method == "get")
				{
					string tmp = url + (url.IndexOf("?") != -1 ? "&" : "?") + (query != "" ? query.Substring(1) : "");
					if (tmp.EndsWith("&")) tmp = tmp.Substring(0, tmp.Length-1);
					result = netClient.DownloadString(tmp);
				}
				else
				{
					netClient.Headers["Content-Type"] = "multipart/form-data; boundary=" + multipartBoundary;
					result = netClient.UploadString(url, multipartQuery);
				}

				WebHeaderCollection h = netClient.ResponseHeaders;

				for (int i = 0; i < h.Count; i++)
				{
					string hdr = h.GetKey(i);
					if (hdr.ToLower() != "set-cookie") continue;

					foreach (string value in h.GetValues(hdr))
					{
						string[] t0 = value.Split(';');
						string[] t1 = t0[0].Split('=');

						cookies.Set(t1[0].Trim(), t1[1].Trim());
					}
				}
			}
			catch (Exception e) {
				errstr = e.Message + "\n" + result;
			}

			if (decodeXml)
			{
				try {
					response = new XmlDocument();
					response.LoadXml(result);
				}
				catch (Exception e2) {
					return "";
				}
			}

			return result;
		}

		/// <summary>
		/// Executes an API call with the parameters specified in the current request query and returns
		/// the response string.
		/// </summary>
		public static string executeRequestJson(string url, string method, string auth, bool decodeJson)
		{
			string result = "";
			errstr = "";

			//System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
			multipartQuery += "--" + multipartBoundary + "--\r\n\r\n";

			try {
				WebClient netClient = new WebClient();

				netClient.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:103.0) Gecko/20100101 Firefox/103.0");

				string cookieHeader = "";

				foreach (string cookieName in cookies.AllKeys) {
					cookieHeader += cookieName + "=" + cookies[cookieName] + "; ";
				}

				if (cookieHeader.Length > 0)
					netClient.Headers["Cookie"] = cookieHeader;

				if (auth != null)
				{
					if (auth.StartsWith("!!*"))
						netClient.Headers["Authorization"] = auth.Substring(3);
					else
						netClient.Headers["Authorization"] = "Basic " + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(auth));
				}

				if (method == "get")
				{
					string tmp = url + (url.IndexOf("?") != -1 ? "&" : "?") + (query != "" ? query.Substring(1) : "");
					if (tmp.EndsWith("&")) tmp = tmp.Substring(0, tmp.Length-1);
					result = netClient.DownloadString(tmp);
				}
				else
				{
					if (addedFiles)
					{
						netClient.Headers["Content-Type"] = "multipart/form-data; boundary=" + multipartBoundary;
						result = netClient.UploadString(url, multipartQuery);
					}
					else
					{
						netClient.Headers["Content-Type"] = "application/x-www-form-urlencoded";
						if (query.StartsWith("&")) query = query.Substring(1);
						result = netClient.UploadString(url, query);
					}
				}

				WebHeaderCollection h = netClient.ResponseHeaders;

				for (int i = 0; i < h.Count; i++)
				{
					string hdr = h.GetKey(i);
					if (hdr.ToLower() != "set-cookie") continue;

					foreach (string value in h.GetValues(hdr))
					{
						string[] t0 = value.Split(';');
						string[] t1 = t0[0].Split('=');

						cookies.Set(t1[0].Trim(), t1[1].Trim());
					}
				}
			}
			catch (Exception e) {
				errstr = e.Message + "\n" + result;
				result = "";
			}

			if (decodeJson)
			{
				try {
					jsonResponse = JsonElement.fromString(result);
				}
				catch (Exception e) {
					jsonResponse = JsonElement.fromString("{\"error\":\"Unable to parse JSON data.\"}");
				}
			}

			return result;
		}

		/// <summary>
		/// Executes a body post and interprets the JSON response.
		/// </summary>
		public static string postData (string url, string contentType, byte[] data, string auth, bool decodeJson)
		{
			string result = "";
			errstr = "";

			//System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

			try {
				WebClient netClient = new WebClient();

				netClient.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:103.0) Gecko/20100101 Firefox/103.0");
				netClient.Headers.Add("Accept", "application/json,application/xhtml+xml,application/xml,*/*");

				string cookieHeader = "";

				foreach (string cookieName in cookies.AllKeys) {
					cookieHeader += cookieName + "=" + cookies[cookieName] + "; ";
				}

				if (cookieHeader.Length > 0)
					netClient.Headers["Cookie"] = cookieHeader;

				netClient.Headers["Content-Type"] = contentType;

				if (auth != null)
				{
					if (auth.StartsWith("!!*"))
						netClient.Headers["Authorization"] = auth.Substring(3);
					else
						netClient.Headers["Authorization"] = "Basic " + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(auth));
				}

				result = System.Text.Encoding.UTF8.GetString(netClient.UploadData(url, "POST", data));

				WebHeaderCollection h = netClient.ResponseHeaders;

				for (int i = 0; i < h.Count; i++)
				{
					string hdr = h.GetKey(i);
					if (hdr.ToLower() != "set-cookie") continue;

					foreach (string value in h.GetValues(hdr))
					{
						string[] t0 = value.Split(';');
						string[] t1 = t0[0].Split('=');

						cookies.Set(t1[0].Trim(), t1[1].Trim());
					}
				}
			}
			catch (Exception e) {
				errstr = e.Message + "\n" + result;
				result = "";
			}

			if (decodeJson)
			{
				try {
					jsonResponse = JsonElement.fromString(result);
				}
				catch (Exception e) {
					jsonResponse = JsonElement.fromString("{\"error\":\"Unable to parse JSON data.\"}");
				}
			}

			return result;
		}

		/// <summary>
		/// Executes an HTTP request.
		/// </summary>
		public static string runRequest (string url, string method, string contentType, byte[] data, string auth, string[] headers)
		{
			string result = "";
			errstr = "";

			//System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
			multipartQuery += "--" + multipartBoundary + "--\r\n\r\n";
			responseCode = 200;

			try
			{
				WebClient netClient = new WebClient();

				netClient.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:103.0) Gecko/20100101 Firefox/103.0");
				netClient.Headers.Add("Accept", "application/json,application/xhtml+xml,application/xml,*/*");

				string cookieHeader = "";

				foreach (string cookieName in cookies.AllKeys) {
					cookieHeader += cookieName + "=" + cookies[cookieName] + "; ";
				}

				if (cookieHeader.Length > 0)
					netClient.Headers["Cookie"] = cookieHeader;

				if (auth != null)
				{
					if (auth.ToUpper().StartsWith("RAW:"))
						netClient.Headers["Authorization"] = auth.Substring(4);
					else
						netClient.Headers["Authorization"] = "Basic " + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(auth));
				}

				foreach (string tmp in headers)
				{
					int i = tmp.IndexOf(':');
					if (i == -1 || i == 0 || i == tmp.Length-1) continue;

					netClient.Headers[tmp.Substring(0,i).Trim()] = tmp.Substring(i+1).Trim();
				}

				if (method == "GET")
				{
					string tmp = url + (url.IndexOf("?") != -1 ? "&" : "?") + (query != "" ? query.Substring(1) : "");
					if (tmp.EndsWith("&")) tmp = tmp.Substring(0, tmp.Length-1);
					result = netClient.DownloadString(tmp);
				}
				else
				{
					if (contentType == "")
					{
						if (addedFiles) {
							netClient.Headers["Content-Type"] = "multipart/form-data; boundary=" + multipartBoundary;
							result = netClient.UploadString(url, method, multipartQuery);
						}
						else {
							netClient.Headers["Content-Type"] = "application/x-www-form-urlencoded";
							if (query.StartsWith("&")) query = query.Substring(1);
							result = netClient.UploadString(url, method, query);
						}
					}
					else
					{
						netClient.Headers["Content-Type"] = contentType;
						result = System.Text.Encoding.UTF8.GetString(netClient.UploadData(url, method, data));
					}
				}

				WebHeaderCollection h = netClient.ResponseHeaders;
				outHeaders.Clear();

				for (int i = 0; i < h.Count; i++)
				{
					string hdr = h.GetKey(i).ToLower();

					//Console.WriteLine("HDR " + hdr + " => " + value);
					foreach (string value in h.GetValues(hdr))
						outHeaders[hdr] = value;

					if (hdr == "set-cookie")
					{
						foreach (string value in h.GetValues(hdr))
						{
							string[] t0 = value.Split(';');
							string[] t1 = t0[0].Split('=');

							cookies.Set(t1[0].Trim(), t1[1].Trim());
						}
					}
				}
			}
			catch (WebException we)
			{
				HttpWebResponse response = (System.Net.HttpWebResponse)we.Response;
				responseCode = (int)response.StatusCode;
				errstr = "Error: " + response.StatusDescription;
				result = "";
			}
			catch (Exception e) {
				errstr = e.Message + "\n" + result;
				result = "";
			}

			return result;
		}

	}

};
