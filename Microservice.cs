using static cs_ijson_microservice.Helpers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace cs_ijson_microservice
{
    public class ENDPOINTS : Dictionary<string, Callback> { }

    public sealed class Microservice
    {
        static Microservice() { }
        private Microservice() { 
            this.endpoints = new ENDPOINTS();
            this.options = new Options();
        }
        private static Microservice myInstance = new Microservice();
        public static Microservice getInstance { get { return myInstance; } }

        /* microservice endpoints */
        private ENDPOINTS endpoints;

        /* microservice name */
        private string name { get; set; }

        /* microservice options */
        private Options options { get; set; }

        /* srv ijson expanded */
        private bool srvExpand { get; set; }

        private HttpClient httpClient;

        public Callback worker { get; set; }

        private LogsDriver logsDriver;

        public void create(string name, Options options)
        {
            this.name = name;
            this.options = options;
            logsDriver = new LogsDriver(this.name);
        }

        public void addEndpoint(string path, Callback handler)
        {
            this.endpoints.Add(path, handler);
        }

        private string getIjsonHost()
        {
            if(!this.srvExpand)
            {
                this.options.ijson = ExpandSrv(this.options.ijson);
                this.srvExpand = true;
            }
            return this.options.ijson;
        }

        public JObject sendServiceRequest(string method, JObject data)
        {
            string[] methods = method.Split('.');
            string service = methods.First();
            string other = string.Join('.', methods.Skip(1));

            data.Add(new JProperty("payload", 
                new JObject(
                        new JProperty("sender", string.Format("{0} (srv)", this.name))
            )));

            string guid = Guid.NewGuid().ToString();

            JObject request = new JObject(new []{
                new JProperty("id", guid),
                new JProperty("method", other),
                new JProperty("params", data),
            });

            string jsonHost = this.getIjsonHost();

            Console.WriteLine("    --> Request ({0} - {1}): {2}", service, guid, JsonConvert.SerializeObject(request));
            request = HttpRequest(handleClientRequest(jsonHost + service, request).Result);
            Console.WriteLine("    <-- Response ({0} - {1}): {2}", service, guid, JsonConvert.SerializeObject(request));
            httpClient.Dispose();

            return request;
        }


        private async Task<HttpResponseMessage> handleClientRequest(string path, JObject json)
        {
            httpClient = new HttpClient();
            HttpRequestMessage requestMessage = new HttpRequestMessage();
            requestMessage.Method = HttpMethod.Post;
            requestMessage.RequestUri = new Uri(path);
            httpClient.Timeout = this.options.requestTimeout;
            requestMessage.Content = new StringContent((json != null) ?
                new JObject(json).ToString() :
                "{}", System.Text.Encoding.UTF8, "application/json");
            return await httpClient.SendAsync(requestMessage);
        }

        private async Task<HttpResponseMessage> handleClientRequest(JObject json = default(JObject), bool isFirstTask = true)
        {
            string url = string.Format("{0}{1}", options.ijson, isFirstTask ? this.name : string.Empty);
            HttpRequestMessage requestMessage = new HttpRequestMessage();
            requestMessage.Headers.Add("type", "worker");
            requestMessage.Method = HttpMethod.Post;
            requestMessage.RequestUri = new Uri(url);
            if (json != null)
            {
                requestMessage.Content = new StringContent(json.ToString(), System.Text.Encoding.UTF8, "application/json");
                logsDriver.Write(LogsDriver.TYPE.Request, json);
            }
            return await httpClient.SendAsync(requestMessage);
        }

        public void start()
        {
            Console.WriteLine("Microservices: {0} start", this.name);
            httpClient = new HttpClient();
            httpClient.Timeout = Timeout.InfiniteTimeSpan;
            JObject requestJObj = new JObject();
            JObject responseJObj = new JObject();
            try
            {
                requestJObj = HttpRequest(handleClientRequest().Result);
                logsDriver.Write(LogsDriver.TYPE.Response, requestJObj);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            while (true)
            {
                try
                {
                    responseJObj = new JObject();
                    if (requestJObj.ContainsKey("id"))
                    {
                        responseJObj.Add("id", requestJObj.SelectToken("id"));
                        JProperty result = new JProperty("result", "this is result");
                        responseJObj.Add(result);
                        string method = (string)requestJObj.SelectToken("method");
                        JObject param = (JObject)requestJObj.SelectToken("params");
                        this.worker(method, param);
                    }
                    requestJObj = HttpRequest(handleClientRequest(responseJObj, false).Result);
                    logsDriver.Write(LogsDriver.TYPE.Response, requestJObj);
                }
                catch(Exception e)
                {
                    string id = (string)requestJObj["id"];
                    ResponseError responseError = new ResponseError(id, new ResponseError.Error(this.name, e.Message));
                    requestJObj = HttpRequest(handleClientRequest(responseError.toJObject(), false).Result);
                }
                
            }
        }
    }
}
