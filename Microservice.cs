using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace cs_ijson_microservice
{
    public delegate void Callback(string action, JToken param);
    public class ENVIRONMENT : Dictionary<string, string> { }
    public class ENDPOINTS : Dictionary<string, Callback> { }

    public sealed class Microservice
    {
        static Microservice() { }
        private Microservice() { }
        private static Microservice myInstance = new Microservice();
        public static Microservice getInstance { get { return myInstance; } }

        private string name = "";
        private ENVIRONMENT env;
        private ENDPOINTS endpoints;

        private HttpClient httpClient = new HttpClient();

        public void create(string serviceName, ENVIRONMENT env, ENDPOINTS endpoints)
        {
            this.name = serviceName;
            this.env = new ENVIRONMENT();
            foreach (string key in env.Keys)
            {
                string value = Environment.GetEnvironmentVariable(key);
                this.env.Add(key, (value != null) ? value : env[key]);
            }

            this.endpoints = endpoints;
        }

        public string getDbCredential()
        {
            return string.Format("Server={0};Port={1};UserId={2};Password={3};Database={4};", env["MYSQL_HOST"], env["MYSQL_PORT"], env["MYSQL_USER"], env["MYSQL_PASSWORD"], env["MYSQL_DATABASE"]);
        }

        private async Task<HttpResponseMessage> handleClientRequest(JObject json = default(JObject), bool isFirstTask = true)
        {
            string url = isFirstTask ? ("http://localhost:8001/" + this.name) : "http://localhost:8001/";

            HttpRequestMessage requestMessage = new HttpRequestMessage();
            requestMessage.Headers.Add("type", "worker");
            requestMessage.Method = HttpMethod.Post;
            requestMessage.RequestUri = new Uri(url);
            requestMessage.Content = new StringContent((json != null) ?
                new JObject(json).ToString() :
                "{}", System.Text.Encoding.UTF8, "application/json");
            return await httpClient.SendAsync(requestMessage);
        }

        public void start()
        {
            Console.WriteLine("Microservices: {0} start", this.name);
            httpClient.Timeout = Timeout.InfiniteTimeSpan;
            HttpRequest request = new HttpRequest(handleClientRequest().Result);
            JObject responseJObj = new JObject();

            while (true)
            {
                if (request.exception != null)
                {
                    Console.WriteLine(request.exception.Message);
                    responseJObj = new JObject();
                    JProperty error = new JProperty("error", request.exception.Message);
                    responseJObj.Add(error);
                }
                else
                {
                    JObject requestJObj = request.JObject;
                    responseJObj = new JObject();
                    if (requestJObj.ContainsKey("id"))
                    {
                        responseJObj.Add("id", requestJObj.SelectToken("id"));
                        JProperty result = new JProperty("result", "this is result");
                        responseJObj.Add(result);
                        string method = (string)requestJObj.SelectToken("method");
                        var posHandlerEnd = method.IndexOf('.');
                        string handler = method.Substring(0, posHandlerEnd);
                        string action = method.Substring(posHandlerEnd + 1);
                        JToken param = requestJObj.SelectToken("params");

                        this.endpoints[handler](action, param);
                    }
                }

                request = new HttpRequest(handleClientRequest(responseJObj, false).Result);
            }
        }

    }
}
