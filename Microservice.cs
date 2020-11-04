using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace cs_ijson_microservice
{
    public delegate void Callback(string action, JToken param);
    public class ENDPOINTS : Dictionary<string, Callback> { }

    public sealed class Microservice
    {
        static Microservice() { }
        private Microservice() { 
            this.endpoints = new ENDPOINTS();
        }
        private static Microservice myInstance = new Microservice();
        public static Microservice getInstance { get { return myInstance; } }

        /* microservice endpoints */
        private ENDPOINTS endpoints;

        /* microservice name */
        private string name { get; set; }

        private string ijsonHost { get; set; }

        /* srv ijson expanded */
        private bool srvExpand { get; set; }

        private HttpClient httpClient = new HttpClient();

        public void create(string name, string ijsonHost)
        {
            this.name = name;
            this.ijsonHost = ijsonHost;
        }
        public void addEndpoint(string path, Callback handler)
        {
            this.endpoints.Add(path, handler);
        }

        public void sendServiceRequest(string method, JObject data)
        {
            string[] methods = method.Split('.');
            string service = methods.First();
            string other = string.Join('.', methods.Skip(1));

            data.Add(new JProperty("payload", 
                new JObject(
                        new JProperty("sender", string.Format("{0} (srv)", this.name))
            )));

            JObject request = new JObject(new []{
                new JProperty("id", Guid.NewGuid().ToString()),
                new JProperty("method", other),
                new JProperty("params", data),
            });
        }

        private async Task<HttpResponseMessage> handleClientRequest(JObject json = default(JObject), bool isFirstTask = true)
        {
            string url = string.Format("{0}{1}", ijsonHost, isFirstTask ? this.name : string.Empty);

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
