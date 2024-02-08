using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using openDCOSIoLink.Models;
using openDCOSIoLink.Models.ProfiNetData;
using openDCOSIoLink.Models.RESTData;
using openDCOSIoLink.Utilities;
using openDCOSIoLink.Models.IOLinkDevice;
using openDCOSIoLink.Utilities.IODDApi;

namespace openDCOSIoLink.deviceHandling
{
    public class deviceHandler
    {
        private Logger logger {get; set;}
        public List<IOLinkBaseStation> deviceList = new List<IOLinkBaseStation>();


        // bei Initialisierung Logger übergeben lassen
        public deviceHandler(Logger logger)
        {
            this.logger = logger;
        }


        public async void registerNewDevice(uint xID, byte[] payloadData, bool autoTree = true, bool registerSubDevices = true)
        {


            IOLinkBaseStation device = new IOLinkBaseStation(logger);

            int i = 0;

            // die Selbstauskunft des Gerätes parsen
            // der Aufbau kann mit Hilfe von Wireshark nachvollzogen werden
            // viel Byteschubserei
            // die gewonnen Daten sollten anhand der Log-Befehle ersichtlich sein
            while (i < payloadData.Length)
            {
                // get the next ServiceID, the ServiceID is given by PayloadData[i] and PayloadData[i+1]
                ushort nextServiceId = (ushort)((payloadData[i] << 8) | payloadData[i + 1]);
                string BlockName = Enum.GetName(typeof(BlockOptions), nextServiceId);
                logger.log("\t BlockName: " + BlockName);

                //get the BlockLength, the BlockLength is given by PayloadData[i+2] and PayloadData[i+3]
                ushort blockLength = (ushort)((payloadData[i + 2] << 8) | payloadData[i + 3]);
                logger.log("\t BlockLength: " + blockLength.ToString());

                //get the BlockInfo, the BlockInfo is given by PayloadData[i+4] and PayloadData[i+5]
                ushort blockInfoValue = (ushort)((payloadData[i + 4] << 8) | payloadData[i + 5]);
                logger.log("\t BlockInfoValue: " + blockInfoValue.ToString());
                string blockInfo = Enum.GetName(typeof(BlockInfo), blockInfoValue);


                if (nextServiceId == (ushort)BlockOptions.DeviceProperties_NameOfStation)
                {
                    // Convert payloadData[i + 4 + 2] - payloadData[i + 4 + blockLength] to ASCII
                    byte[] asciiBytes = new byte[blockLength - 2];
                    Array.Copy(payloadData, i + 6, asciiBytes, 0, blockLength - 2);
                    string asciiString = System.Text.Encoding.ASCII.GetString(asciiBytes);
                    logger.log("Parsed NameOfStation: " + asciiString);
                    device.NameOfStation = asciiString;
                }
                else if (nextServiceId == (ushort)BlockOptions.IP_IPParameter)
                {

                    device.IP = new IPAddress(new byte[] { payloadData[i + 6], payloadData[i + 7], payloadData[i + 8], payloadData[i + 9] });
                    logger.log("Parsed IP: " + device.IP.ToString());
                    device.Subnet = new IPAddress(new byte[] { payloadData[i + 10], payloadData[i + 11], payloadData[i + 12], payloadData[i + 13] });
                    logger.log("Parsed Subnet: " + device.Subnet.ToString());
                    device.Gateway = new IPAddress(new byte[] { payloadData[i + 14], payloadData[i + 15], payloadData[i + 16], payloadData[i + 17] });
                    logger.log("Parsed Gateway: " + device.Gateway.ToString());
                }
                else if (nextServiceId == (ushort)BlockOptions.DeviceProperties_DeviceID)
                {

                    device.VendorID = payloadData[i + 6].ToString("X2") + payloadData[i + 7].ToString("X2");
                    logger.log("Parsed VendorID: " + device.VendorID);
                    device.DeviceID = payloadData[i + 8].ToString("X2") + payloadData[i + 9].ToString("X2");
                    logger.log("Parsed DeviceID: " + device.DeviceID);

                }
                else if (nextServiceId == (ushort)BlockOptions.DeviceProperties_DeviceOptions)
                {
                    //TBD
                }
                else if (nextServiceId == (ushort)BlockOptions.DeviceProperties_DeviceRole)
                {
                    device.DeviceRole = (DeviceRoles)payloadData[i + 6];
                    logger.log("Parsed DeviceRole: " + device.DeviceRole.ToString());
                }
                else if (nextServiceId == (ushort)BlockOptions.DeviceInitiative_DeviceInitiative)
                {
                    //TBD
                }
                else if (nextServiceId == (ushort)BlockOptions.DeviceProperties_DeviceVendor)
                {
                    // Convert payloadData[i + 4 + 2] - payloadData[i + 4 + blockLength] to ASCII
                    byte[] asciiBytes = new byte[blockLength - 2];
                    Array.Copy(payloadData, i + 4 + 2, asciiBytes, 0, blockLength - 2);
                    string asciiString = System.Text.Encoding.ASCII.GetString(asciiBytes);
                    device.DeviceType = asciiString;
                    logger.log("Parsed DeviceType: " + device.DeviceType);

                }
                else
                {
                    logger.log("\t BlockInfo: " + blockInfo);
                    if (logger.logLevel == 0)
                    {
                        Console.Write("\t ");
                        for (int j = 2; j < blockLength; j++)
                        {
                            Console.Write(payloadData[i + 4 + j].ToString("X2") + "|");
                        }
                        Console.WriteLine();
                    }
                }

                if (blockLength % 2 == 1)
                {
                    logger.log("\t Padding");
                    i++; // wenn die BlockLength ungerade ist, muss ein Byte übersprungen werden
                }
                i = i + blockLength + 4;
            }
            

            //Seriennummer herausfinden
            basicResponse serialResponse = await device.getValue("/deviceinfo/serialnumber/getdata");
            device.serialNumber = serialResponse.data.value;

            
            logger.log("--------------------", 1);
            logger.log("Ein neues Gerät mit der Seriennummer " + device.serialNumber + " hat sich angemeldet", 1);
            logger.log("Infos: ", 1);
            logger.log("\t NameOfStation: " + device.NameOfStation, 1);
            logger.log("\t DeviceType: " + device.DeviceType, 1);
            logger.log("\t VendorID: " + device.VendorID, 1);

            //Convert the Hexadecimal number device.DeviceID to a decimal number
            int dec = Convert.ToInt32(device.DeviceID, 16);
            logger.log("\t DeviceID: " + device.DeviceID + "/" + dec.ToString(), 1);

            logger.log("\t Seriennummer: " + device.serialNumber, 1);
            logger.log("\t IP-Config: <" + device.IP.ToString() + " | " + device.Subnet.ToString() + " | " + device.Gateway.ToString() + ">", 1);
            logger.log("--------------------", 1);

            // wenn autoTree true ist, werden die Informationen über das Gerät direkt beim Anmelden abgefragt
            if (autoTree){
                device.getTree();
                while (device.tree == null)
                {
                    System.Threading.Thread.Sleep(1);
                }
            }
            
            if (registerSubDevices){
                List<string> deviceList = device.getIoLinkDeviceList(onlyConnected: true);
                API infoAPI = new API();
                infoAPI.getVendorIDTable();

                foreach (string linkDevice in deviceList){
                    device.addIOlinkDevice(linkDevice, infoAPI);
                }
            }
            // Gerät der Liste hinzufügen
            deviceList.Add(device);
        }

        public void removeDuplicates(){
            // doppelt eingetragene Geräte entfernen
            List<IOLinkBaseStation> uniqueList = deviceList.Distinct(new IOLinkDeviceComparer()).ToList();
            deviceList = uniqueList;
        }
    }
    public class IOLinkDeviceComparer : IEqualityComparer<IOLinkBaseStation>
    {   
        // Prüft ob zwei IOLinkDevices gleich sind
        // Prüft dies anhand der Seriennummer des Geräts
        public bool Equals(IOLinkBaseStation x, IOLinkBaseStation y)
        {
            if (x == null || y == null)
                return false;

            return x.serialNumber == y.serialNumber;
        }
        public int GetHashCode(IOLinkBaseStation obj)
        {
            return obj.IP.GetHashCode() ^ obj.DeviceID.GetHashCode() ^ obj.Subnet.GetHashCode();
        }
    }
    

}