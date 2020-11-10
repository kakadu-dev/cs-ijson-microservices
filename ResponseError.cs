using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace cs_ijson_microservice
{
    class MjResponse
    {
        [JsonProperty(Order = -1)]
        public string jsonrpc = "2.0";
    }

    class ResponseError : MjResponse
    {
        public class Error
        {
            public string service;
            public string message;
            public Error (string service, string message)
            {
                this.service = service;
                this.message = message;
            }
        }
        [JsonProperty(Order = 1)]
        public string id;
        [JsonProperty(Order = 2)]
        public Error error;
        public ResponseError(string id, Error error)
        {
            this.id = id;
            this.error = error;
        }
        public JObject toJObject()
        {
            return JObject.FromObject(this);
        }
    }
}
