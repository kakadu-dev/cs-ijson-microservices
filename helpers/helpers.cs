using System;
using System.Collections.Generic;
using System.Text;

namespace cs_ijson_microservice.helpers
{
    public class ExpandSrvExeption : Exception
    {
        public ExpandSrvExeption(string message) : base(message)
        { }
    }

    public class helpers
    {
        public void ExpandSrv(string host)
        {
            throw new ExpandSrvExeption("qwa");
        }
    }
}
