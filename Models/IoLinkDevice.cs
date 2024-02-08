using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using openDCOSIoLink.Models.RESTData;
using openDCOSIoLink.Models.ProfiNetData;
using System.Text;
using openDCOSIoLink.Utilities;
using System.Security.Cryptography;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices;
using openDCOSIoLink.Models.IODDApiData;
using openDCOSIoLink.Utilities.IODDApi;


namespace openDCOSIoLink.Models.IOLinkDevice
{

    public class IOLinkBaseStation
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
        public IOLinkBaseStation(Logger log)
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
        public async Task<basicResponse> getValue(string value)
        {
            logger.log("Requesting value " + value);
            await using Stream stream =
               await requestClient.GetStreamAsync("http://" + IP + value);

            //string json  = "[" + new StreamReader(stream).ReadToEnd() + "]"; // so eine Mimose der Deserializer
            string json = new StreamReader(stream).ReadToEnd(); // so eine Mimose der Deserializer

            var response = JsonConvert.DeserializeObject<basicResponse>(json);
            httpErrorHandling(response.code);
            logger.log("Value succesfully retrieved");
            return response;
        }
        public async Task<basicResponse> setValue(string valueName, string newValue)
        {
            // Muster:
            // {
            //   "code": "request",
            //   "cid": 6,
            //   "adr": "/timer[1]/counter/setdata",
            //   "data": {
            //     "newvalue": 234234
            //   }
            // }
            var data = new
            {
                code = "request",
                cid = consumerID,
                adr = valueName,
                data = new
                {
                    newvalue = newValue
                }
            };
            //Formatieren und senden
            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            //var response = new HttpResponseMessage();
            var response = requestClient.PostAsync("http://" + IP, content);
            response.Wait();
            var response_content = response.Result.Content.ReadAsStringAsync();
            response_content.Wait();
            var responseString = response_content.Result;

            try
            {
            }
            catch (Exception ex)
            {
                // Handle exception from PostAsync
                Console.WriteLine("test");
            }
            // await requestClient.PostAsync("http://" + IP, content);
            //var responseString = await response.Content.ReadAsStringAsync();


            ////check for Transport Errors
            //httpErrorHandling(response.StatusCode);

            //check IO-Link Errors
            basicResponse answer = JsonConvert.DeserializeObject<basicResponse>(responseString);
            return answer;
        }
        public async Task<basicResponse> setTimer(int timerID, int Intervall)
        {
            return await setValue("/timer[" + timerID + "]/interval/setdata", Convert.ToString(Intervall));
        }
        public async void getTree()
        {
            logger.log("Requesting Tree");
            await using Stream stream =
               await requestClient.GetStreamAsync("http://" + IP + "/getTree");

            //string json  = "[" + new StreamReader(stream).ReadToEnd() + "]"; // so eine Mimose der Deserializer
            string json = new StreamReader(stream).ReadToEnd(); // so eine Mimose der Deserializer

            tree = JsonConvert.DeserializeObject<Tree>(json);
            httpErrorHandling(tree.code);
            logger.log("Tree retrieved succesfully");
            tree.jsonText = json;
        }
        public Format getFormatFromAddress(string address)
        {
            String cleanAddress = address.Replace("\\", "");
            cleanAddress = cleanAddress.Replace("/getdata", "");
            Format returnFormat = searchForType(tree.data.subs, "", cleanAddress);
            if (returnFormat == null)
            {
                throw new KeyNotFoundException("Das Format der Adresse " + address + " konnte nicht gefunden werden (gefundes Format ist null)");
            }
            return searchForType(tree.data.subs, "", cleanAddress);
        }
        public Format searchForType(List<Sub> subs, string adress, string searchAdress)
        {
            foreach (Sub item in subs)
            {
                if (adress + "/" + item.identifier == searchAdress)
                {
                    return item.format;
                }
                else if (item.subs != null && searchAdress.Contains(adress + "/" + item.identifier))
                {
                    return searchForType(item.subs, adress + "/" + item.identifier, searchAdress);
                }
            }
            throw new KeyNotFoundException("Das Format der Adresse " + searchAdress + " konnte nicht gefunden werden (Ende des Trees erreicht)");
        }
        public async Task<basicResponse> subscribeMQTT(IPAddress MQTTAddress, string MQTTTopic, List<string> publishData, string publishEvent = "/timer[1]/counter/datachanged/subscribe", int Port = 1883, string duration = "lifetime")
        {
            //Muster
            //{"adr": "00-02-01-6e-9d-e2/timer[1]/counter/datachanged/subscribe", 
            //"data" : 
            //    {"callback" : "mqtt://:0", 
            //    "datatosend" : ["00-02-01-6e-9d-e2/iolinkmaster/port[2]/iolinkdevice/pdin"]}, 
            //"duration" : "lifetime", 
            //"cid" : 456, 
            //"code" : 10}
            //Initialize request
            var data = new
            {
                code = "request", //request?
                cid = consumerID,
                adr = publishEvent,
                data = new
                {
                    callback = "mqtt://" + MQTTAddress.ToString() + ":" + Port + MQTTTopic,
                    datatosend = publishData
                },
                duration = duration

            };

            //Format and send
            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = requestClient.PostAsync("http://" + IP, content);
            response.Wait();
            var response_content = response.Result.Content.ReadAsStringAsync();
            response_content.Wait();
            var responseString = response_content.Result;


            //check for Transport Errors
            httpErrorHandling(response.Result.StatusCode);

            //check IO-Link Errors
            basicResponse answer = JsonConvert.DeserializeObject<basicResponse>(responseString);
            httpErrorHandling(answer.code);
            return answer;

        }
        private void httpErrorHandling(HttpStatusCode responseCode)
        {

            switch (responseCode)
            {
                case HttpStatusCode.OK:
                    return;
                case HttpStatusCode.BadRequest:
                    throw new HttpRequestException("Bad Request - please check Inputparameters");
                case HttpStatusCode.NotFound:
                    throw new KeyNotFoundException("The Request returned Status Code 404 - Not Found");
                default:
                    throw new NotImplementedException("Request returned unexpected status code " + responseCode);
            }

        }
        private List<string> treeSearch(List<Sub> subs, string address, string Type)
        {
            List<string> services = new List<string>();
            foreach (Sub sub in subs)
            {
                if (sub.type == Type)
                {
                    // Speichern Sie den Namen des Subs
                    string name = address + "/" + sub.identifier;
                    services.Add(name);
                }

                if (sub.subs != null && sub.subs.Any())
                {
                    string newAddress = address + "/" + sub.identifier;
                    services.AddRange(treeSearch(sub.subs, newAddress, Type));
                }
            }
            return services;
        }
        private List<string> elementSearch(List<Sub> subs, string address, string identifier)
        {
            List<string> services = new List<string>();
            foreach (Sub sub in subs)
            {
                if (sub.identifier == identifier)
                {
                    // Speichern Sie den Namen des Subs
                    string name = address + "/" + sub.identifier;
                    services.Add(name);
                }

                if (sub.subs != null && sub.subs.Any())
                {
                    string newAddress = address + "/" + sub.identifier;
                    services.AddRange(elementSearch(sub.subs, newAddress, identifier));
                }
            }
            return services;
        }
        public bool hasTree()
        {
            if (tree == null)
            {
                return false;
            }
            return true;
        }
        public List<string> getServiceList()
        {
            return treeSearch(tree.data.subs, "", "service");
        }
        public List<string> getEventList()
        {
            return treeSearch(tree.data.subs, "", "event");
        }
        public List<string> getIoLinkDeviceList(bool onlyConnected = true)
        {

            // alle verfügbaren IO-Link Devices abrufen
            List<string> availableDevices = new List<string>();
            availableDevices = elementSearch(tree.data.subs, "", "iolinkdevice");


            List<string> connectedDevices = new List<string>();

            // rausfinden welche verbunden und bereit sind
            if (onlyConnected)
            {

                foreach (string device in availableDevices)
                {
                    // Status abfragen
                    basicResponse response = getValue(device + "/status/getdata").Result;

                    switch (response.data.value)
                    {
                        case "0":
                            // State not connected
                            break;
                        case "1":
                            // State preoperate
                            break;
                        case "2":
                            // State operate
                            connectedDevices.Add(device);
                            break;
                        case "3":
                            // State communication error
                            break;
                        default:
                            // ???
                            throw new NotImplementedException("The device returned an unexpected status code " + response.data.value);
                            break;
                    }
                }
                return connectedDevices;

            }
            else
            {
                return availableDevices;
            }
        }
        public string replaceWildCards(string input, string portDevice = null)
        {
            string returnString = input;
            returnString = returnString.Replace("$NameOfStation", NameOfStation);
            returnString = returnString.Replace("$deviceSerial", serialNumber);
            returnString = returnString.Replace("$deviceID", DeviceID);
            if (portDevice != null)
            {
                returnString = returnString.Replace("$portDevice", portDevice);
            }
            List<string> portDevices = getIoLinkDeviceList();
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

