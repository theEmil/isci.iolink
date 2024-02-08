using System.Collections.Generic;
using System.Net;



// Datenstruktur zum deserialisieren der IODD-Api Daten
namespace openDCOSIoLink.Models.IODDApiData{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class Content
    {
        public bool hasMoreVersions { get; set; }
        public int deviceId { get; set; }
        public string ioLinkRev { get; set; }
        public string versionString { get; set; }
        public int ioddId { get; set; }
        public string productId { get; set; }
        public int productVariantId { get; set; }
        public string productName { get; set; }
        public string vendorName { get; set; }
        public object uploadDate { get; set; }
        public int vendorId { get; set; }
        public string ioddStatus { get; set; }
        public string indicationOfSource { get; set; }
        public Md md { get; set; }
        public bool hasMd { get; set; }
        public string driverName { get; set; }
    }

    public class Md
    {
        public int id { get; set; }
        public string fileName { get; set; }
        public string revision { get; set; }
        public long releasedAt { get; set; }
        public long updatedAt { get; set; }
    }

    public class searchResult
    {
        public List<Content> content { get; set; }
        public int number { get; set; }
        public int size { get; set; }
        public int numberOfElements { get; set; }
        public List<object> sort { get; set; }
        public bool first { get; set; }
        public bool last { get; set; }
        public int totalPages { get; set; }
        public int totalElements { get; set; }
    }
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class Iodd
    {
        public int id { get; set; }
        public int deviceId { get; set; }
        public string deviceFamily { get; set; }
        public long uploadDate { get; set; }
        public long releaseDate { get; set; }
        public string ioLinkRev { get; set; }
        public string version { get; set; }
        public string status { get; set; }
        public string indicationOfSource { get; set; }
        public string driverName { get; set; }
        public Vendor vendor { get; set; }
        public Md md { get; set; }
        public bool hasMd { get; set; }
    }
    public class Product
    {
        public string productId { get; set; }
    }

    public class detailedResult
    {
        public int id { get; set; }
        public string productName { get; set; }
        public string productDescription { get; set; }
        public Product product { get; set; }
        public Iodd iodd { get; set; }
        public Vendor vendor { get; set; }
    }

    public class Vendor
    {
        public string name { get; set; }
        public int vendorId { get; set; }
        public string url { get; set; }
    }



}