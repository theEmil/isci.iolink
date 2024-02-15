using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using openDCOSIoLink.Models.RESTData;
using openDCOSIoLink.Models.ProfiNetData;
using openDCOSIoLink.Utilities;
using System.Collections.Generic;
using openDCOSIoLink.Utilities.IODDApi;
using openDCOSIoLink.Models.Device;

namespace openDCOSIoLink.Models.BaseStation
{

    public partial class IoLinkBaseStation
    {
        // Allgemeine Informationen
        public string NameOfStation { get; set; }
        public string serialNumber { get; set; }

        // IP Informationen
        public IPAddress IP { get; set; }
        public IPAddress Subnet { get; set; }
        public IPAddress Gateway { get; set; }

        // Gerätedaten
        public string VendorID { get; set; }
        public string DeviceID { get; set; }
        public DeviceRoles DeviceRole { get; set; }
        public string DeviceType { get; set; }

        // Variablen zum Verwalten des Gerätes
        private HttpClient requestClient { get; set; }
        public Tree tree { get; set; }
        private Logger logger { get; set; }
        public int consumerID { get; set; }

        // Verbundene IO-Link Devices
        public List<IoLinkDevice> connectedDevices { get; set; }

        // beim Initialisierne requestClient und Logger anlegen
        public IoLinkBaseStation(Logger log)
        {
            requestClient = new HttpClient();
            requestClient.DefaultRequestHeaders.Accept.Clear();
            requestClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/openDCOS"));
            requestClient.DefaultRequestHeaders.Add("User-Agent", "openDCOS IO-Link Modul");
            logger = log;
            logger.log("New IOLinkDevice initialized");
            consumerID = 2;
        }

        // fragt einen Wert per getRequest vom Gerät ab
        // Value ist die API-Adresse des Wertes
        // Beispiel: /timer[1]/counter/data
        public string replaceWildCards(string input, IoLinkDevice portDevice = null)
        {
            string returnString = input;
            returnString = returnString.Replace("$NameOfStation", NameOfStation);
            returnString = returnString.Replace("$deviceSerial", serialNumber);
            returnString = returnString.Replace("$deviceID", DeviceID);
            if (portDevice != null)
            {
                returnString = returnString.Replace("$sensorSerial", portDevice.serialNumber);
                returnString = returnString.Replace("$sensorDeviceID", portDevice.deviceID);
                returnString = returnString.Replace("$sensorName", portDevice.productName);
                returnString = returnString.Replace("$sensorProductName", portDevice.productName);

            }
            return returnString;
        }
        public void addIOlinkDevice(string address, API InfoApi)
        {
            if (connectedDevices == null)
            {
                connectedDevices = new List<IoLinkDevice>();
            }

            IoLinkDevice newDevice = new IoLinkDevice();
            newDevice.address = address;

            basicResponse response = getValue(address + "/vendorid/getdata").Result;
            newDevice.vendorID = response.data.value;
            response = getValue(address + "/deviceid/getdata").Result;
            newDevice.deviceID = response.data.value;
            response = getValue(address + "/serial/getdata").Result;
            newDevice.serialNumber = response.data.value;
            response = getValue(address + "/productname/getdata").Result;
            newDevice.productName = response.data.value;
            string vendorName = InfoApi.manufacturerName[newDevice.vendorID];
            newDevice.vendorName = vendorName;
            int productVariantID = InfoApi.searchForDevice(vendorName, newDevice.deviceID, newDevice.productName).productVariantId;
            newDevice.productVariantID = productVariantID;
            newDevice.details = InfoApi.getDetails(productVariantID);
            connectedDevices.Add(newDevice);
        }
    }
    

}

