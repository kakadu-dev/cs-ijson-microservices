using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using DnsClient;
using DnsClient.Protocol;

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


    public class ExpandSrvExeption : Exception
    {
        public ExpandSrvExeption(string message) : base(message)
        { }
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
                    .ThenBy(record => record.Weight)
                    .FirstOrDefault();
                return string.Format("{0}://{1}:{2}", protocol, record.Target.Original, record.Port);
            }
            throw new ExpandSrvExeption(query.ErrorMessage);
        }
    }
}
