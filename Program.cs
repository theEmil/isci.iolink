using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using openDCOSIoLink.Models;
using openDCOSIoLink.Utilities;
using openDCOSIoLink.Models.IOLinkDevice;
using openDCOSIoLink.deviceHandling;
using openDCOSIoLink.Models.ProfiNetData;
using openDCOSIoLink.Models.RESTData;
using openDCOSIoLink.packetCapture;
using openDCOSIoLink.Utilities.IODDApi;

using System.Text.RegularExpressions;
using SharpPcap;
using openDCOSIoLink.Models.IODDApiData;
using openDCOSIoLink.Models.settings;
using Newtonsoft.Json;
using System.IO;

using isci.Allgemein;
using isci.Daten;
using isci.Beschreibung;

namespace openDCOSIoLink
{
    public class Konfiguration : Parameter
    {
        
        public Konfiguration(string[] args) : base(args)
        {

        }
    }
    public class Program
    {
        static Logger logger = new Logger();
        static deviceHandler devHandler = new deviceHandler(logger);
        static packageUtils packUtils = new packageUtils();
        static CaptureDeviceList networkInterfaces = CaptureDeviceList.Instance;
        static Settings settings { get; set; }
        static Konfiguration aktuelleKonfiguration;
        public static async Task Main(string[] args)
        {
            
            // read settings from settings.json, path to settings.json is given in args
            string json = File.ReadAllText("settings.json");
            settings = JsonConvert.DeserializeObject<Settings>(json);

            // Konfiguration initialisieren
            aktuelleKonfiguration = new Konfiguration(args);


            IOLinkSetup();


            //Erstellung des Zugriffs auf die dateibasierte Datenstruktur unter Nutzung der Parametrierung.
            Datenstruktur structure = new Datenstruktur(aktuelleKonfiguration);
            
            //Beispiel für die Erstellung eines Datenmodells. Wenn die Parametrierung genutzt wird, wird damit das Kerndatenmodell einer Modulinstanz erstellt mit der Modulinstanzidentifikation.
            //Die Modulinstanz kann auch weitere Datenmodelle erstellen unter Nutzung anderer Konstruktoren, sodass eine andere Identifikation genutzt wird.
            Datenmodell dm = new Datenmodell(aktuelleKonfiguration);


            foreach (IOLinkBaseStation item in devHandler.deviceList)
            {
                foreach (dataLink data in settings.dataMap)
                {
                    String address = data.dataPattern.Replace("\\[","[").Replace("\\]","]");
                    String variableName = data.variableName;
                    //Anlegen eines Dateneintrags und  zum Datenmodell

                    Format datenFormat = item.getFormatFromAddress(address);
                    Dateneintrag datenEintrag = null;
                    if (datenFormat.type == "string")
                    {
                        datenEintrag = new dtString("", variableName);
                    }
                    else if (datenFormat.type == "number")
                    {
                        if (datenFormat.encoding == "integer")
                        {
                            datenEintrag = new dtInt32(0, address);
                        }
                        else if (datenFormat.encoding == "double")
                        {
                            datenEintrag = new dtDouble(0.0, address);
                        }
                    }
                    else{
                        throw new NotImplementedException("der vom Gerät gelieferte Datentyp für die Variable " + address + " ist noch nicht implementiert");
                    }
                    
                    basicResponse antwort = await item.getValue(address);
                    
                    datenEintrag.WertAusString(antwort.data.value);

                    dm.Dateneinträge.Add(datenEintrag);
                }
            }



            //Speichern des Datenmodells im Standardordner als Datei.
            dm.Speichern(aktuelleKonfiguration);



            //Hinzufügen des Datenmodells zur Datenstruktur.
            structure.DatenmodellEinhängen(dm);
            //Hinzufügen aller als Dateien gespeicherte Datenmodelle im Standardordner.
            structure.DatenmodelleEinhängenAusOrdner(aktuelleKonfiguration.OrdnerDatenmodelle);
            //Logischer Start der Datenstruktur.
            structure.Start();


            //Arbeitsschleife
            while(true)
            {
                structure.Lesen();

                foreach (var Wert in dm.Dateneinträge)
                {
                    
                }
                

                structure.Schreiben();
            }


            // TODO: Restlichen Code auskommentieren
            // TODO: Loggen überall gleich machen
            // TODO: verteilte TODOS abarbeiten

        }
        public async static void IOLinkSetup(){
            // ################# IO-Link Setup ############################

            // Pcap Interfaces initialisieren und abhören TODO: Interfacewhitelist implementierne
            startCapturing();

            // Identifizierungsanfrage senden und Geräte in Liste speichern
            searchAndGetDevices();
        }
        public async static void subscribeWithSettingsMap(){
            // TODO: Subscribtions auch wieder beenden/löschen können

            // Alle Geräte in der Liste durchgehen
            foreach (IOLinkBaseStation device in devHandler.deviceList)
            {
                // die beiden Timer auf die gewünschten Intervalle setzen
                //await device.setTimer(1, 1000);
                //await device.setTimer(2, 10000);

                // Alle Services und Events des Gerätes auslesen
                List<string> ServiceList = devHandler.deviceList[0].getServiceList();
                List<string> EventList = devHandler.deviceList[0].getEventList();
                List<string> portDevices = devHandler.deviceList[0].getIoLinkDeviceList();

                // Wildcards für Type deviceData:
                //      $mqttIP = IP Adresse des MQTT Brokers (oben festgelegt)
                //      $NameOfStation = NameOfStation des IO-Link Moduls
                //      $deviceSerial = Seriennummer des IO-Link Moduls
                //      $deviceID = DeviceID des IO-Link Moduls
                //
                // Zusätzliche Wildcards für Type Sensor:
                //      $sensorSerial = Seriennummer des Sensors
                //      $sensorDeviceID = DeviceID des Sensors
                //      $sensorName = Name des Sensors
                //      $sensorProductName = Produktname des Sensors

                // Alle gewünschten Werte vom Typ deviceData per MQTT abonnieren
                foreach (var dataLink in settings.dataMap)
                {
                    if (dataLink.type == dataLinkType.deviceData)
                    {
                        // Wildcards ersetzen
                        string sendEvent = null;
                        string eventPattern = dataLink.eventPattern;
                        eventPattern = eventPattern.Replace("$NameOfStation", device.NameOfStation);
                        eventPattern = eventPattern.Replace("$deviceSerial", device.serialNumber);
                        eventPattern = eventPattern.Replace("$deviceID", device.DeviceID);

                        // das gewünschte Event suchen
                        // die Daten werden bei auslösen des Events gesendet
                        foreach (string messageEvent in EventList)
                        {
                            // Check if messageEvent matches the Regex String dataLink.eventPattern
                            if (Regex.IsMatch(messageEvent, eventPattern))
                            {
                                // ersten gefundenen Eintrag verwenden
                                sendEvent = messageEvent + "/subscribe";
                                break;
                            }
                        }

                        // Wildcards ersetzen
                        string dataPattern = dataLink.dataPattern;
                        dataPattern = dataPattern.Replace("$NameOfStation", device.NameOfStation);
                        dataPattern = dataPattern.Replace("$deviceSerial", device.serialNumber);
                        dataPattern = dataPattern.Replace("$deviceID", device.DeviceID);


                        // die zu sendenden Daten suchen, können mehrere sein
                        List<string> sendData = new List<string>();
                        foreach (string messageData in ServiceList)
                        {
                            // Check if messageData matches the Regex String dataLink.dataPattern
                            if (Regex.IsMatch(messageData, dataPattern))
                            {
                                sendData.Add(messageData);
                            }
                        }

                        // Wildcards ersetzen
                        string MQTTTopic = dataLink.MQTTTopic;
                        MQTTTopic = MQTTTopic.Replace("$NameOfStation", device.NameOfStation);
                        MQTTTopic = MQTTTopic.Replace("$deviceSerial", device.serialNumber);
                        MQTTTopic = MQTTTopic.Replace("$deviceID", device.DeviceID);

                        string mqttIP = dataLink.MQttBrokerIP;
                        mqttIP = mqttIP.Replace("$mqttIP", settings.mqttIP);
                        IPAddress MQTTBrokerIP = IPAddress.Parse(mqttIP);

                        // Daten mit entsprechendne Topics und Events abonnieren
                        await device.subscribeMQTT(MQTTBrokerIP, MQTTTopic, sendData, sendEvent, Port: dataLink.Port, duration: dataLink.duration);

                    }
                }

                //TODO: Irgendwas mit den PortDevices machen
                

                //foreach (var portDevice in portDevices)
                //{
                //    string vendorID = null;
                //    string portDeviceID = null;
                //    string deviceSerialNumber = null;
                //    string portDeviceProductName = null;
                //    basicResponse response = new basicResponse();
//
                //    try
                //    {
                //        response = await device.getValue(portDevice + "/vendorid/getdata");
                //        vendorID = response.data.value;
                //    }
                //    catch (Exception ex)
                //    {
                //        Console.WriteLine(ex.Message);
                //        Console.WriteLine("Could not get vendorID");
                //    }
                //    if (vendorID != null)
                //    {
                //        response = await device.getValue(portDevice + "/deviceid/getdata");
                //        portDeviceID = response.data.value;
                //        response = await device.getValue(portDevice + "/serial/getdata");
                //        deviceSerialNumber = response.data.value;
                //        response = await device.getValue(portDevice + "/productname/getdata");
                //        portDeviceProductName = response.data.value;
//
                //        string vendorName = InfoApi.manufacturerName[vendorID];
                //        int productVariantID = InfoApi.searchForDevice(vendorName, portDeviceID, portDeviceProductName).productVariantId;
                //        detailedResult details = InfoApi.getDetails(productVariantID);
                //    }
                //}
                
            }
        }
        public static void startCapturing()
        {
            // SharpPcap Verison ausgeben
            var ver = Pcap.SharpPcapVersion;
            logger.log("SharpPcapVersion: " + ver);

            // Wenn keine Interfaces gefunden wurden, abbrechen
            if (networkInterfaces.Count < 1)
            {
                logger.log("No devices were found on this machine", 3);
                return;
            }

            
            // Alle Interfaces ausgeben
            logger.log("");
            logger.log("The following devices are available on this machine:");
            logger.log("----------------------------------------------------");
            logger.log("");

            int i = 0;


            foreach (var dev in networkInterfaces)
            {
                logger.log("{" + i + ", " + dev.Name + ", " + dev.Description + "}");
                i++;
            }
            logger.log("");


            // Den Netzwerktraffic auf den gewünschten Interfaces abhören und auf Profinet Pakete prüfen
            Listener listener = new Listener(logger, devHandler);

            foreach (var @interface in networkInterfaces)
            {
                // Register our handler function to the 'packet arrival' event
                @interface.OnPacketArrival +=
                   new PacketArrivalEventHandler(listener.onPacketArrival);

                // Open the device for capturing
                int readTimeoutMilliseconds = 1000;
                @interface.Open(mode: DeviceModes.Promiscuous | DeviceModes.DataTransferUdp | DeviceModes.NoCaptureLocal, read_timeout: readTimeoutMilliseconds);

                logger.log("");
                logger.log("-- Listening on " + @interface.Name + ", " + @interface.Description + ", hit 'Enter' to stop...");

                // Start the capturing process
                @interface.StartCapture();
            }
        }

        public static void searchAndGetDevices()
        {
            // Indentifizierungsanfrage über alle Netzwerkinterfaces senden
            foreach (var device in networkInterfaces)
            {
                if (device.MacAddress != null)
                {
                    // Send the identify request
                    byte[] identifyRequest = packUtils.getRequestIdentity(device.MacAddress.GetAddressBytes());
                    device.SendPacket(identifyRequest);
                }

            }

            // Warten bis die gewünschte Menge an Geräten sich angemeldet hat
            bool connected = false;
            int counter = 0;
            while (!connected && counter < 100)
            {
                System.Threading.Thread.Sleep(100);
                if (devHandler.deviceList.Count > 0)
                {
                    connected = true;
                }
                counter++;
            }

            // Warten bis Geräteinformationen verfügbar sind
            foreach (var device in devHandler.deviceList)
            {
                while (!devHandler.deviceList[0].hasTree())
                {
                    System.Threading.Thread.Sleep(100);
                }
            }

            // Bei mehreren Netzwerkinterfaces kann es dazu kommen, dass sich ein Gerät mehrfach anmeldet bzw. die Anmeldung zwei mal empfangen wird
            // In diesem Fall werden die doppelten Geräte aus der Liste entfernt
            devHandler.removeDuplicates();
        }

    }
}