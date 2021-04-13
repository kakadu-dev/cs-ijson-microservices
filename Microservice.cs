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

        private HttpClient httpClientMicroservice;

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
            MjRequest mjRequest = HttpRequest(handleClientRequest(jsonHost + service, request).Result);
            if (!mjRequest.isError)
            {
                request = mjRequest.request;
            }
            else
            {
                logsDriver.Write(LogsDriver.TYPE.Error, mjRequest.errorMessages);
            }
            Console.WriteLine("    <-- Response ({0} - {1}): {2}", service, guid, JsonConvert.SerializeObject(request));
            httpClientMicroservice.Dispose();

            return request;
        }


        private async Task<HttpResponseMessage> handleClientRequest(string path, JObject json)
        {
            httpClientMicroservice = new HttpClient();
            HttpRequestMessage requestMessage = new HttpRequestMessage();
            requestMessage.Method = HttpMethod.Post;
            requestMessage.RequestUri = new Uri(path);
            httpClientMicroservice.Timeout = this.options.requestTimeout;
            requestMessage.Content = new StringContent((json != null) ?
                new JObject(json).ToString() :
                "{}", System.Text.Encoding.UTF8, "application/json");
            return await httpClientMicroservice.SendAsync(requestMessage);
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
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Microservices: {0} start", this.name);
            Console.ResetColor();
            httpClient = new HttpClient();
            httpClient.Timeout = Timeout.InfiniteTimeSpan;
            MjRequest mjRequest = new MjRequest();
            JObject responseJObj = new JObject();
            try
            {
                mjRequest = HttpRequest(handleClientRequest().Result);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            while (true)
            {
                try
                {
                    if (!mjRequest.isError)
                    {
                        logsDriver.Write(LogsDriver.TYPE.Response, mjRequest.request);
                        responseJObj = new JObject();
                        if (mjRequest.request.ContainsKey("id"))
                        {
                            responseJObj.Add("id", mjRequest.request.SelectToken("id"));

                            string method = "undefined";
                            JObject param = new JObject();
                            if (mjRequest.request.ContainsKey("method"))
                            {
                                method = (string)mjRequest.request.SelectToken("method");
                            }
                            if (mjRequest.request.ContainsKey("params"))
                            {
                                param = (JObject)mjRequest.request.SelectToken("params");
                            }
                            responseJObj.Add(this.worker(method, param));
                        }
                        mjRequest = HttpRequest(handleClientRequest(responseJObj, false).Result);
                    }
                    else
                    {
                        logsDriver.Write(LogsDriver.TYPE.Response, mjRequest.invalidJson);
                        ResponseError responseError = new ResponseError("0", new ResponseError.Error(this.name, mjRequest.errorMessages));
                        mjRequest = HttpRequest(handleClientRequest(responseError.toJObject(), false).Result);
                    }
                }
                catch(Exception e)
                {
                    if (!mjRequest.isError)
                    {
                        string id = (string)mjRequest.request["id"];
                        ResponseError responseError = new ResponseError(id, new ResponseError.Error(this.name, e.Message));
                        mjRequest = HttpRequest(handleClientRequest(responseError.toJObject(), false).Result);
                    }
                }
                
            }
        }
    }
}
