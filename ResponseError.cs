using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace cs_ijson_microservice
{
    internal class MjResponse
    {
        [JsonProperty(Order = -1)]
        public string jsonrpc = "2.0";
    }

    internal class ResponseError : MjResponse
    {
        public class Error
        {
            public string service;
            public string message;

            public Error(string service, string message)
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

        public JObject ToJObject()
        {
            return JObject.FromObject(this);
        }
    }
}
