using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static cs_ijson_microservice.Helpers;

namespace cs_ijson_microservice
{
    public class Endpoints : Dictionary<string, Callback> { }

    public sealed class Microservice
    {
        static Microservice() { }

        private Microservice()
        {
            Endpoints = new Endpoints();
            Options = new Options();
        }

        private static readonly Microservice myInstance = new Microservice();

        public static Microservice GetInstance => myInstance;

        private string Name { get; set; }

        private Options Options { get; set; }

        private bool SrvExpand { get; set; }

        public Callback Worker { get; set; }

        private readonly Endpoints Endpoints;

        private HttpClient httpClient;

        private HttpClient httpClientMicroservice;

        private LogsDriver logsDriver;

        public void Create(string name, Options options)
        {
            Name = name;
            Options = options;
            logsDriver = new LogsDriver();
        }

        public void AddEndpoint(string path, Callback handler)
        {
            Endpoints.Add(path, handler);
        }

        private string GetIjsonHost()
        {
            if (!SrvExpand)
            {
                Options.Ijson = ExpandSrv(Options.Ijson);
                SrvExpand = true;
            }
            return Options.Ijson;
        }

        public JObject SendToService(string method, JObject data)
        {
            JObject response;
            try
            {
                response = GetInstance.SendServiceRequest(method, data);
            }
            catch (AggregateException)
            {
                throw new Exception("Request timeout");
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }

            JObject param = (JObject)response.SelectToken("params");
            if (JsonConvert.SerializeObject(data) == JsonConvert.SerializeObject(param))
            {
                throw new Exception("Service unavailable");
            }
            return response;
        }

        public JObject SendServiceRequest(string method, JObject data)
        {
            string[] methods = method.Split('.');
            string service = methods.First();
            string other = string.Join('.', methods.Skip(1));

            data.Merge(new JProperty("payload",
                new JObject(
                    new JProperty("sender", string.Format("{0} (srv)", Name))
            )));

            string guid = Guid.NewGuid().ToString();

            JObject request = new JObject(new[]{
                new JProperty("id", guid),
                new JProperty("method", other),
                new JProperty("params", data),
            });

            string jsonHost = GetIjsonHost();

            Console.WriteLine("    --> Request ({0} - {1}): {2}", service, guid, JsonConvert.SerializeObject(request));
            MjRequest mjRequest = HttpRequest(HandleClientRequest(jsonHost + service, request).Result);
            if (!mjRequest.IsError)
            {
                request = mjRequest.Request;
            }
            else
            {
                logsDriver.Write(LogsDriver.Type.Error, mjRequest.ErrorMessages);
            }
            Console.WriteLine("    <-- Response ({0} - {1}): {2}", service, guid, JsonConvert.SerializeObject(request));
            httpClientMicroservice.Dispose();

            return request;
        }

        private async Task<HttpResponseMessage> HandleClientRequest(string path, JObject json)
        {
            httpClientMicroservice = new HttpClient();
            HttpRequestMessage requestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(path)
            };
            httpClientMicroservice.Timeout = Options.RequestTimeout;
            requestMessage.Content = new StringContent((json != null) ?
                new JObject(json).ToString() :
                "{}", System.Text.Encoding.UTF8, "application/json");
            return await httpClientMicroservice.SendAsync(requestMessage);
        }

        private async Task<HttpResponseMessage> HandleClientRequest(JObject json = default, bool isFirstTask = true)
        {
            string url = string.Format("{0}{1}", Options.Ijson, isFirstTask ? Name : string.Empty);
            HttpRequestMessage requestMessage = new HttpRequestMessage();
            requestMessage.Headers.Add("type", "worker");
            requestMessage.Method = HttpMethod.Post;
            requestMessage.RequestUri = new Uri(url);
            if (json != null)
            {
                requestMessage.Content = new StringContent(json.ToString(), System.Text.Encoding.UTF8, "application/json");
                logsDriver.Write(LogsDriver.Type.Response, json);
            }
            return await httpClient.SendAsync(requestMessage);
        }

        public void Start()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("    Microservices: {0} start", Name);
            Console.ResetColor();
            httpClient = new HttpClient
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            MjRequest mjRequest = new MjRequest();
            try
            {
                mjRequest = HttpRequest(HandleClientRequest().Result);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            while (true)
            {
                try
                {
                    if (!mjRequest.IsError)
                    {
                        logsDriver.Write(LogsDriver.Type.Request, mjRequest.Request);
                        JObject responseJObj = new JObject();
                        if (mjRequest.Request.ContainsKey("id"))
                        {
                            responseJObj.Add("id", mjRequest.Request.SelectToken("id"));

                            string method = "undefined";
                            JObject param = new JObject();
                            if (mjRequest.Request.ContainsKey("method"))
                            {
                                method = (string)mjRequest.Request.SelectToken("method");
                            }
                            if (mjRequest.Request.ContainsKey("params"))
                            {
                                param = (JObject)mjRequest.Request.SelectToken("params");
                            }
                            responseJObj.Add(Worker(method, param));
                        }
                        mjRequest = HttpRequest(HandleClientRequest(responseJObj, false).Result);
                    }
                    else
                    {
                        logsDriver.Write(LogsDriver.Type.Response, mjRequest.InvalidJson);
                        ResponseError responseError = new ResponseError("0", new ResponseError.Error(Name, mjRequest.ErrorMessages));
                        mjRequest = HttpRequest(HandleClientRequest(responseError.ToJObject(), false).Result);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("    ERROR: " + ex);
                    if (!mjRequest.IsError)
                    {
                        string id = (string)mjRequest.Request["id"];
                        ResponseError responseError = new ResponseError(id, new ResponseError.Error(Name, ex.Message));
                        mjRequest = HttpRequest(HandleClientRequest(responseError.ToJObject(), false).Result);
                    }
                }

            }
        }
    }
}
