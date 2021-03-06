﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using DnsClient;
using DnsClient.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace cs_ijson_microservice
{
    public delegate JProperty Callback(string action, JObject param);
    public class Options
    {
        public Options() { }
        public Options(string version, string env, string ijson, int requestTimeout)
        {
            this.version = version;
            this.env = env;
            this.ijson = ijson.EndsWith("/") ? ijson : (ijson + "/");
        }
        public string version { get; set; } = "1.0.0";
        public string env { get; set; } = "development";
        public string ijson { get; set; } = "http://localhost:8001";
        public TimeSpan requestTimeout { get; set; } = TimeSpan.FromSeconds(60 * 5);
    }


    public class helpersExeption : Exception
    {
        public helpersExeption(string message) : base(message) { }
    }

    public class Helpers
    {

        public class MjRequest
        {
            public bool isError { get; set; } = false;
            public JObject request { get; set; }
            public string invalidJson { get; set; }
            public string errorMessages { get; set; }

            public MjRequest()
            {

            }
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

        public static MjRequest HttpRequest(HttpResponseMessage httpResponseMessage)
        {
            MjRequest result = new MjRequest();
            string content = httpResponseMessage.Content.ReadAsStringAsync().Result;
            try
            {
                result.request = JObject.Parse(content);
            }
            catch (Exception e)
            {
                result.isError = true;
                result.errorMessages = e.Message;
                result.invalidJson = content.Replace("\n", "").Replace("\r", "");
            }
            return result;
        }

        public class MySqlConfig
        {
            public MySqlConfig(string host, string port, string database, string user, string password)
            {
                Host = host;
                Port = port;
                Database = database;
                User = user;
                Password = password;
            }
            public MySqlConfig(JObject jObject)
            {
                if (jObject.ContainsKey("MysqlCredentials"))
                {
                    JObject mysqlCredentials = (JObject)jObject.SelectToken("MysqlCredentials[0]");
                    Host = (string)mysqlCredentials["host"];
                    Port = (string)mysqlCredentials["port"];
                    Database = (string)mysqlCredentials["database"];
                    User = (string)mysqlCredentials["user"];
                    Password = (string)mysqlCredentials["password"];
                }
            }
            public string Host;
            public string Port;
            public string Database;
            public string User;
            public string Password;
            public string getStringConnection { 
                get {
                    return string.Format("Server={0};Port={1};UserId={2};Password={3};Database={4};", Host, Port, User, Password, Database);
                } 
            }
        }

        public class MicroserviceConfig
        {
            public MicroserviceConfig() { }
            public MicroserviceConfig(MySqlConfig defaultMySql, string authAlias)
            {
                this.mysql = defaultMySql;
                this.authAlias = authAlias;
            }

            public void addObject(JObject jObject)
            {
                if (jObject.ContainsKey("result"))
                {
                    JObject result = (JObject)jObject.SelectToken("result.model");
                    this.mysql = new MySqlConfig(result);
                    JObject services = (JObject)result["Services"][0];
                    if (services.ContainsKey("alias"))
                    {
                        this.hasAuthorization = ((string)services["alias"] == authAlias) ? true : this.hasAuthorization;
                    }
                }
            }
            public MySqlConfig mysql;
            public bool hasAuthorization;
            private string authAlias;
        }

    }

    public class LogsDriver
    {
        public class TYPE
        {
            public const string Response    = "    <-- Response";
            public const string Request     = "    --> Request";
            public const string Error       = "    ERROR";
        }
        private string serviceName;

        public LogsDriver(string serviceName)
        {
            this.serviceName = serviceName;
        }
        public void Write(string type, JObject jObject)
        {
            string id = "0";
            if(jObject.ContainsKey("id"))
                id = (string)jObject["id"];

            Console.WriteLine("{0} ({1}) : {2}", type, id, JsonConvert.SerializeObject(jObject));
        }
        public void Write(string type, string errorMessages)
        {
            if(type == TYPE.Error)
                Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine("{0} : {1}", type, errorMessages);
            Console.ResetColor();
        }
    }
}
