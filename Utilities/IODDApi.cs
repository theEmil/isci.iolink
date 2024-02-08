using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using openDCOSIoLink.Models.IODDApiData;
using openDCOSIoLink.Models;
using openDCOSIoLink.Utilities;
using System.Xml.Linq;
using Newtonsoft.Json;


namespace openDCOSIoLink.Utilities.IODDApi
{
    public class API
    {


        public Dictionary<string, string> manufacturerName {get; set;}

        public void getVendorIDTable(string URL = "https://io-link.com/share/Downloads/Vendor_ID_Table.xml")
        {
            // XML von Website herunterladen
            string XML = "";
            using (WebClient wc = new WebClient())
            {
                XML = wc.DownloadString(URL);
            }

            // XML parsen
            XDocument doc = XDocument.Parse(XML);

            var manufacturers = doc.Descendants("{http://www.profibus.com/IM/2003/11/Man_ID}Manufacturer")
                .Select(manufacturer => new
                {
                    ID = (string)manufacturer.Attribute("ID"),
                    Name = (string)manufacturer.Element("{http://www.profibus.com/IM/2003/11/Man_ID}ManufacturerInfo")
                                            .Element("{http://www.profibus.com/IM/2003/11/Man_ID}ManufacturerName")
                });

            // Namen aus XML in Dictionary speichern
            manufacturerName = new Dictionary<string, string>();
            foreach (var manufacturer in manufacturers)
            {
                manufacturerName[manufacturer.ID] = manufacturer.Name;
            }
        }
        
        // get JSON from URL "examle"
        public string getJSON(string url)
        {
            string json = "";
            using (WebClient wc = new WebClient())
            {
                json = wc.DownloadString(url);
            }
            return json;
        }
        public Content searchForDevice(string vendorName = null, string deviceID = null, string productName = null, string productId = null, string ioLinkRev = null){
            // Example https://ioddfinder.io-link.com/productvariants/search?page=0&vendorName=ifm%20electronic%20gmbh&deviceId=722&productName=KI5304&productId=KI5304&ioLinkRev=1.1
            // Build the search URL
            string URL = "https://ioddfinder.io-link.com/api/drivers?";
            if(vendorName != null){
                URL += "&vendorName=" + vendorName ;
            }
            if(deviceID != null){
                URL += "&deviceId=" + deviceID;
            }
            if(productName != null){
                URL += "&productName=" + productName;
            }
            if(productId != null){
                URL += "&productId=" + productId;
            }
            if(ioLinkRev != null){
                URL += "&ioLinkRev=" + ioLinkRev;
            }
            
            //get the JSON from the URL
            string json = getJSON(URL);

            //Parse the JSON into IODDApiData.Root
            searchResult data = JsonConvert.DeserializeObject<searchResult>(json);

            //Return the first result
            return data.content[0];
        }
        public detailedResult getDetails(int productVariantID){
            string json = getJSON("https://ioddfinder.io-link.com/api/productvariants/" + productVariantID);
            detailedResult data = JsonConvert.DeserializeObject<detailedResult>(json);

            return data;
        }
    }
}