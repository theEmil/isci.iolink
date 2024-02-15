using openDCOSIoLink.Models.IODDApiData;


namespace openDCOSIoLink.Models.Device
{
    public class IoLinkDevice
    {
        public string address { get; set; }
        public string vendorID { get; set; }
        public string deviceID { get; set; }
        public string serialNumber { get; set; }
        public string productName { get; set; }
        public string vendorName { get; set; }
        public int productVariantID { get; set; }
        public detailedResult details { get; set; }
    }
}