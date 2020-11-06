using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Text;
using DnsClient;
using DnsClient.Protocol;
using Newtonsoft.Json.Linq;

namespace cs_ijson_microservice
{
    public class Options
    {
        public Options() { }
        public Options(string version, string env, string ijson, int requestTimeout)
        {
            this.version = version;
            this.env = env;
            this.ijson = ijson;
        }
        public string version { get; set; } = "1.0.0";
        public string env { get; set; } = "development";
        public string ijson { get; set; } = "http://localhost:8001";
        public int requestTimeout { get; set; } = 1000 * 60 * 5;
    }


    public class helpersExeption : Exception
    {
        public helpersExeption(string message) : base(message) { }
    }

    public class Helpers
    {
        public static string ExpandSrv(string host)
        {
            if (!host.EndsWith(".srv"))
            {
                return host;
            }
            string[] hostSplits = host.Split("://");
            string protocol = hostSplits.First();
            string domain = hostSplits.Last();

            var query = new LookupClient().Query(domain.Replace(".srv", ""), QueryType.SRV);
            if (!query.HasError)
            {
                SrvRecord record = query.Answers.SrvRecords()
                    .OrderBy(record => record.Priority)
                    .FirstOrDefault();
                string target = record.Target.Original.EndsWith(".") ? 
                    record.Target.Original.Substring(0, record.Target.Original.Length - 1) : 
                    record.Target.Original;
                return string.Format("{0}://{1}:{2}/", protocol, target, record.Port);
            }
            throw new helpersExeption(query.ErrorMessage);
        }

        public static JObject HttpRequest(HttpResponseMessage httpResponseMessage)
        {
            JObject result = new JObject();
            try
            {
                result  = JObject.Parse(httpResponseMessage.Content.ReadAsStringAsync().Result);
            }
            catch (Exception e)
            {
                throw new helpersExeption(e.Message);
            }
            return result;
        }

        public class MySql
        {
            public static string host;
            public static string port;
            public static string database;
            public static string user;
            public static string password;
        }

        public class MicroserviceConfig
        {
            public MicroserviceConfig()
            {

            }
            public MySql mysql;
        }

    }
}
