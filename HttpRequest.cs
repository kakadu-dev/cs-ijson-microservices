using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace cs_ijson_microservice
{
    class HttpRequest
    {
        public JObject JObject;
        public HttpResponseMessage httpResponseMessage;
        public Exception exception;
        public HttpRequest(HttpResponseMessage httpResponseMessage)
        {
            try
            {
                this.httpResponseMessage = httpResponseMessage;
                this.JObject = JObject.Parse(this.httpResponseMessage.Content.ReadAsStringAsync().Result);
            }
            catch (Exception e)
            {
                this.exception = e;
            }
        }
    }
}
