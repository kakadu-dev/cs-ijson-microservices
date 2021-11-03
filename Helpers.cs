using DnsClient;
using DnsClient.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net.Http;

namespace cs_ijson_microservice
{
    public delegate JProperty Callback(string action, JObject param);

    public class Options
    {
        public Options() { }

        public Options(string version, string env, string ijson, int requestTimeout)
        {
            Version = version;
            Env = env;
            Ijson = ijson.EndsWith("/") ? ijson : (ijson + "/");
            RequestTimeout = TimeSpan.FromMilliseconds(requestTimeout);
        }

        public string Version { get; set; } = "1.0.0";

        public string Env { get; set; } = "development";

        public string Ijson { get; set; } = "http://localhost:8001";

        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMilliseconds(1000 * 15);
    }


    public class HelpersExeption : Exception
    {
        public HelpersExeption(string message) : base(message) { }
    }

    public class Helpers
    {
        public class MjRequest
        {
            public bool IsError { get; set; } = false;

            public JObject Request { get; set; }

            public string InvalidJson { get; set; }

            public string ErrorMessages { get; set; }

            public MjRequest() { }
        }

        public static string ExpandSrv(string host)
        {
            if (!host.EndsWith(".srv"))
            {
                return host;
            }
            string[] hostSplits = host.Split("://");
            string protocol = hostSplits.First();
            string domain = hostSplits.Last();

            IDnsQueryResponse query = new LookupClient().Query(domain.Replace(".srv", ""), QueryType.SRV);
            if (!query.HasError)
            {
                SrvRecord record = query.Answers.SrvRecords()
                    .OrderBy(record => record.Priority)
                    .FirstOrDefault();
                string target = record.Target.Original.EndsWith(".") ?
                    record.Target.Original[0..^1] :
                    record.Target.Original;
                return string.Format("{0}://{1}:{2}/", protocol, target, record.Port);
            }
            throw new HelpersExeption(query.ErrorMessage);
        }

        public static MjRequest HttpRequest(HttpResponseMessage httpResponseMessage)
        {
            MjRequest result = new MjRequest();
            string content = httpResponseMessage.Content.ReadAsStringAsync().Result;
            try
            {
                result.Request = JObject.Parse(content);
            }
            catch (Exception e)
            {
                result.IsError = true;
                result.ErrorMessages = e.Message;
                result.InvalidJson = content.Replace("\n", "").Replace("\r", "");
            }
            return result;
        }
    }

    public class LogsDriver
    {
        public class Type
        {
            public const string Response = "    <-- Response";
            public const string Request = "    --> Request";
            public const string Error = "    ERROR";
        }

        public LogsDriver() { }

        public void Write(string type, JObject jObject)
        {
            string id = "0";
            if (jObject.ContainsKey("id"))
            {
                id = (string)jObject["id"];
            }

            Console.WriteLine("{0} ({1}): {2}", type, id, JsonConvert.SerializeObject(jObject));
        }

        public void Write(string type, string errorMessages)
        {
            if (type == Type.Error)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }

            Console.WriteLine("{0}: {1}", type, errorMessages);
            Console.ResetColor();
        }
    }
}
