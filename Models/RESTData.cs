using System.Collections.Generic;
using System.Net;

namespace openDCOSIoLink.Models.RESTData{
        public class basicData
        {
            public string value { get; set; }
        }

        public class basicResponse
        {
            public int cid { get; set; }
            public basicData data { get; set; }
            public HttpStatusCode code { get; set; }
        }

        // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
        public class Data
        {
            public string identifier { get; set; }
            public string type { get; set; }
            public List<Sub> subs { get; set; }
        }

        public class Format
        {
            public string type { get; set; }
            public string @namespace { get; set; }
            public string encoding { get; set; }
            public Valuation valuation { get; set; }
        }

        public class Tree
        {
            public int cid { get; set; }
            public Data data { get; set; }
            public HttpStatusCode code { get; set; }
            public string? jsonText {get; set;}
        }

        public class Sub
        {
            public string identifier { get; set; }
            public string type { get; set; }
            public List<string> profiles { get; set; }
            public List<Sub> subs { get; set; }
            public Format format { get; set; }
        }

        public class Valuation
        {
            public int min { get; set; }
            public object max { get; set; }
            public Dictionary<string, string> valuelist { get; set; }
            public int? off { get; set; }
            public int? minlength { get; set; }
            public int? maxlength { get; set; }
        }
       
}