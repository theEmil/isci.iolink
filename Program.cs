using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using openDCOSIoLink.Models;
using openDCOSIoLink.Utilities;
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
using System.Net.Http;
using openDCOSIoLink.Models.BaseStation;
using openDCOSIoLink.Models.Device;
using System.Linq;

namespace openDCOSIoLink
{
    public class Konfiguration : Parameter
    {
        [fromEnv, fromArgs]
        public int warteZeit;
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
        static List<ILiveDevice> whiteListedInterfaces { get; set; }
        static Settings settings { get; set; }
        static Konfiguration aktuelleKonfiguration;
        static Datenmodell dm { get; set; }
        static Dictionary<string, Dateneintrag> dateinEintragLinkMap = new Dictionary<string, Dateneintrag>();

        public static async Task Main(string[] args)
        {
            // Settings einlesen
            string json = File.ReadAllText("settings.json");
            settings = JsonConvert.DeserializeObject<Settings>(json);

            // Konfiguration initialisieren
            aktuelleKonfiguration = new Konfiguration(args);

            // Geräte suchen, finden und initialisieren
            IOLinkSetup();


            //Erstellung des Zugriffs auf die dateibasierte Datenstruktur unter Nutzung der Parametrierung.
            Datenstruktur datenstruktur = new Datenstruktur(aktuelleKonfiguration);

            //Beispiel für die Erstellung eines Datenmodells. Wenn die Parametrierung genutzt wird, wird damit das Kerndatenmodell einer Modulinstanz erstellt mit der Modulinstanzidentifikation.
            //Die Modulinstanz kann auch weitere Datenmodelle erstellen unter Nutzung anderer Konstruktoren, sodass eine andere Identifikation genutzt wird.
            dm = new Datenmodell(aktuelleKonfiguration);


            foreach (IoLinkBaseStation baseStation in devHandler.deviceList)
            {
                // Alle Datenwerte die in den Einstellung als deviceData definiert sind, werden in das Datenmodell eingefügt
                List<dataLink> nonSensorValues = settings.dataMap.FindAll(x => x.type == dataLinkType.deviceData);
                foreach (dataLink data in nonSensorValues)
                {
                    String address = data.dataPattern.Replace("\\[", "[").Replace("\\]", "]");
                    String variableName = data.variableName;
                    variableName = baseStation.replaceWildCards(variableName);

                    var tmp = await getDateneintragFromDataLink(baseStation, address, variableName);

                    dateinEintragLinkMap.Add(tmp.Key, tmp.Value);
                    dm.Dateneinträge.Add(tmp.Value);
                }

                // Die Werte aller Sensoren werden in das Datenmodell eingefügt
                foreach (IoLinkDevice subDevice in baseStation.connectedDevices)
                {
                    String address = subDevice.address + "/pdin/getdata";
                    String variableName = "$deviceSerial_SensorValue_$sensorName_$sensorSerial";
                    variableName = baseStation.replaceWildCards(variableName, subDevice);


                    var tmp = await getDateneintragFromDataLink(baseStation, address, variableName);

                    dateinEintragLinkMap.Add(tmp.Key, tmp.Value);
                    dm.Dateneinträge.Add(tmp.Value);
                }
            }



            //Speichern des Datenmodells im Standardordner als Datei.
            dm.Speichern(aktuelleKonfiguration);
            //Hinzufügen des Datenmodells zur Datenstruktur.
            datenstruktur.DatenmodellEinhängen(dm);
            //Hinzufügen aller als Dateien gespeicherte Datenmodelle im Standardordner.
            datenstruktur.DatenmodelleEinhängenAusOrdner(aktuelleKonfiguration.OrdnerDatenmodelle);
            //Logischer Start der Datenstruktur.
            datenstruktur.Start();

            var ausfuehrungsmodell = new Ausführungsmodell(aktuelleKonfiguration, datenstruktur.Zustand);

            // im Hintergrund Werte aktualisieren
            updateSchleife();

            //Arbeitsschleife
            while (true)
            {
                datenstruktur.Zustand.WertAusSpeicherLesen();

                if (ausfuehrungsmodell.AktuellerZustandModulAktivieren()) //Abprüfen, ob das Ausführungsmodell für den Zustandswert die Modulinstanz vorsieht.
                {
                    var zustandParameter = (string)ausfuehrungsmodell.ParameterAktuellerZustand(); //Abruf der Parameter für die Ausführung. Ist ein Object und kann in eigene Typen gewandelt werden.

                    switch (zustandParameter)
                    {
                        case "E":
                            {
                                foreach (var dt in dateinEintragLinkMap)
                                {
                                    dt.Value.WertInSpeicherSchreiben();
                                }
                                break;
                            }
                        case "A":
                            {
                                break;
                            }
                    }

                    ausfuehrungsmodell.Folgezustand();
                    datenstruktur.Zustand.WertInSpeicherSchreiben(); //Zustandswert in Datenstruktur übernehmen.
                }

                System.Threading.Thread.Sleep(aktuelleKonfiguration.warteZeit);

            }
        }

        // Läuft im Hintergrund und aktualisiert alle pollRate ms die Werte
        public static async void updateSchleife()
        {
            while (true)
            {
                await singleDatenAktualisieren();
                System.Threading.Thread.Sleep(settings.pollRate);
            }
        }

        // bevorzugte Methode um Daten zu aktualisieren
        public async static Task<bool> singleDatenAktualisieren()
        {
            foreach (IoLinkBaseStation device in devHandler.deviceList)
            {
                List<string> dataToGet = new List<string>();
                foreach (var dt in dateinEintragLinkMap)
                {
                    if (dt.Key.Contains(device.serialNumber))
                    {
                        string tmp = dt.Key.Replace(device.serialNumber + ":/", "");

                        basicResponse dataResponse = await device.getValue(tmp);
                        if (dt.Value.type == Datentypen.String)
                        {
                            dt.Value.Wert = Convert.ToString(dataResponse.data.value);
                        }
                        else if (dt.Value.type == Datentypen.Int64)
                        {
                            dt.Value.Wert = Convert.ToInt64(dataResponse.data.value);
                        }
                        else if (dt.Value.type == Datentypen.Double)
                        {
                            dt.Value.Wert = Convert.ToDouble(dataResponse.data.value);
                        }
                        //dt.Wert = dataResponse.data.value;
                    }
                }
            }
            return true;
        }


        // langsamer als singleDatenAktualisieren
        public async static Task<bool> multiDatenAktualisieren()
        {
            foreach (IoLinkBaseStation device in devHandler.deviceList)
            {
                List<string> dataToGet = new List<string>();
                foreach (var dt in dateinEintragLinkMap)
                {
                    if (dt.Key.Contains(device.serialNumber))
                    {
                        string tmp = dt.Key.Replace(device.serialNumber + ":/", "");
                        tmp = tmp.Replace("/getdata", "");
                        dataToGet.Add(tmp);
                    }
                }

                dataMulti newValues = await device.getDataMulti(dataToGet);

                foreach (var dt in dateinEintragLinkMap)
                {
                    String address = dt.Key.Replace(device.serialNumber + ":/", "");
                    address = address.Replace("/getdata", "");

                    if (newValues.data.ContainsKey(address))
                    {
                        if (newValues.data[address].code == HttpStatusCode.OK)
                        {
                            if (dt.Value.type == Datentypen.String && newValues.data[address].data.GetType() == typeof(string))
                            {
                                dt.Value.Wert = newValues.data[address].data;
                            }
                            else if (dt.Value.type == Datentypen.Int64 && newValues.data[address].data.GetType() == typeof(long))
                            {
                                dt.Value.Wert = newValues.data[address].data;
                            }
                            else if (dt.Value.type == Datentypen.Double && newValues.data[address].data.GetType() == typeof(double))
                            {
                                dt.Value.Wert = newValues.data[address].data;
                            }
                            else
                            {
                                throw new FormatException("Falsche Datentype");
                            }
                        }
                        else
                        {
                            throw new HttpRequestException("Fehler beim Abrufen der Variable:" + dt.Key);
                        }
                    }
                }
            }
            return true;
        }
        public async static Task<KeyValuePair<string, Dateneintrag>> getDateneintragFromDataLink(IoLinkBaseStation baseStation, String address, String variableName)
        {

            Dateneintrag datenEintrag = null;

            //Anlegen eines Dateneintrags und initialisieren des Datentyps
            Format datenFormat = baseStation.getFormatFromAddress(address);
            if (datenFormat.type == "string")
            {
                datenEintrag = new dtString("", variableName);
            }
            else if (datenFormat.type == "number")
            {
                if (datenFormat.encoding == "integer")
                {
                    datenEintrag = new dtInt64(0, variableName);
                }
                else if (datenFormat.encoding == "double")
                {
                    datenEintrag = new dtDouble(0.0, variableName);
                }
            }
            else
            {
                throw new NotImplementedException("der vom Gerät gelieferte Datentyp (" + datenFormat.type + ") für die Variable " + address + " ist noch nicht implementiert");
            }

            // Addresse und baseStation der Variable speichern

            // Anfangswert festlegen
            basicResponse antwort = await baseStation.getValue(address);
            datenEintrag.WertAusString(antwort.data.value);

            return new KeyValuePair<string, Dateneintrag>(baseStation.serialNumber + ":/" + address, datenEintrag);
        }


        public static void IOLinkSetup()
        {
            // ################# IO-Link Setup ############################

            // Pcap Interfaces initialisieren und abhören TODO: Interfacewhitelist implementierne
            startCapturing();

            // Identifizierungsanfrage senden und Geräte in Liste speichern
            searchAndGetDevices();

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
                throw new NullReferenceException("Es konnten keine Netzwerkinterfaces gefunden werden");
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


            // Nur Interfaces behalten, die in den Settings angegeben sind
            whiteListedInterfaces = networkInterfaces.Where(x => settings.InterfaceWhiteList.Contains(x.Name)).ToList();
            logger.log("");


            // Den Netzwerktraffic auf den gewünschten Interfaces abhören und auf Profinet Pakete prüfen
            Listener listener = new Listener(logger, devHandler);

            foreach (var @interface in whiteListedInterfaces)
            {
                // Register our handler function to the 'packet arrival' event
                @interface.OnPacketArrival +=
                   new PacketArrivalEventHandler(listener.onPacketArrival);

                // Open the device for capturing
                int readTimeoutMilliseconds = 1000;
                @interface.Open(read_timeout: readTimeoutMilliseconds);
                //@interface.Open(mode: DeviceModes.Promiscuous | DeviceModes.DataTransferUdp | DeviceModes.NoCaptureLocal, read_timeout: readTimeoutMilliseconds);

                logger.log("");
                logger.log("-- Listening on " + @interface.Name + ", " + @interface.Description + ", hit 'Enter' to stop...");

                // Start the capturing process
                @interface.StartCapture();
            }
        }

        // sendet Identifizierungsanfrage über alle Netzwerkinterfaces und wartet auf Antworten
        // 
        public static void searchAndGetDevices()
        {
            // Indentifizierungsanfrage über alle Netzwerkinterfaces senden
            foreach (var device in whiteListedInterfaces)
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
            if (counter >= 100)
            {
                logger.log("Es konnten keine Geräte gefunden werden", 3);
                throw new TimeoutException("Es konnten nach 10 Sekunden Wartezeit keine Io-Link Geräte gefunden werden");
            }

            // Warten bis Geräteinformationen verfügbar sind
            foreach (var device in devHandler.deviceList)
            {
                while (!device.hasTree())
                {
                    System.Threading.Thread.Sleep(100);
                }
            }

            // Bei mehreren Netzwerkinterfaces kann es dazu kommen, dass sich ein Gerät mehrfach anmeldet bzw. die Anmeldung zwei mal empfangen wird
            // In diesem Fall werden die doppelten Geräte aus der Liste entfernt
            devHandler.removeDuplicates();
        }

        // erstmal veraltet
        public async static void subscribeWithSettingsMap()
        {
            // TODO: Subscribtions auch wieder beenden/löschen können

            // Alle Geräte in der Liste durchgehen
            foreach (IoLinkBaseStation device in devHandler.deviceList)
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
    }
}