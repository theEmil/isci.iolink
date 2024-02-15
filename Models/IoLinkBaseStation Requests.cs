using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using openDCOSIoLink.Models.RESTData;
using openDCOSIoLink.Models.ProfiNetData;
using System.Text;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace openDCOSIoLink.Models.BaseStation
{

    public partial class IoLinkBaseStation
    {
        // fragt einen Wert per getRequest vom Gerät ab
        // Value ist die API-Adresse des Wertes
        // Beispiel: /timer[1]/counter/data
        public async Task<basicResponse> getValue(string value)
        {
            logger.log("Requesting value " + value);
            await using Stream stream =
            await requestClient.GetStreamAsync("http://" + IP + value);
            string json = new StreamReader(stream).ReadToEnd();

            var response = JsonConvert.DeserializeObject<basicResponse>(json);
            httpErrorHandling(response.code);
            logger.log("Value succesfully retrieved");
            return response;
        }
        public async Task<dataMulti> getDataMulti(List<string> dataToSend)
        {
            var request = new
            {
                code = "request",
                cid = consumerID,
                adr = "/getdatamulti",
                data = new
                {
                    datatosend = dataToSend
                }
            };
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            // send a post request to the device
            var postResponse = requestClient.PostAsync("http://" + IP, content);
            postResponse.Wait();
            var response_content = postResponse.Result.Content.ReadAsStringAsync();
            response_content.Wait();

            var responseString = response_content.Result;
            dataMulti response = JsonConvert.DeserializeObject<dataMulti>(responseString);
            httpErrorHandling(response.code);
            logger.log("multiple Values succesfully retrieved");
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
    }
    

}

