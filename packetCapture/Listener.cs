using System;
using openDCOSIoLink.deviceHandling;
using openDCOSIoLink.Models.ProfiNetData;
using openDCOSIoLink.Utilities;
using SharpPcap;
using SharpPcap.LibPcap;

namespace openDCOSIoLink.packetCapture
{
    public class Listener
    {
        private Logger logger {get; set;}
        private deviceHandler handler {get; set;}
        public void onPacketArrival(object sender, PacketCapture e)
        {
            //var time = e.Header.Timeval.Date;
            //var len = e.Data.Length;
            var rawPacket = e.GetPacket();

            var packet = PacketDotNet.Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);

            if (packet is PacketDotNet.EthernetPacket eth)
            {
                if (eth.Type == PacketDotNet.EthernetType.Profinet)
                {
                    logger.log(Convert.ToString(eth.Type));
                    logger.log("Zieladresse: " + eth.DestinationHardwareAddress);
                    logger.log("Absender:" + eth.SourceHardwareAddress);
                    logger.log("FrameID: " + eth.PayloadData[0].ToString("X2") + eth.PayloadData[1].ToString("X2"));

                    // get the Enum name of the ServiceID, the ServiceID is given by PayloadData[2] and PayloadData[3]

                    ushort serviceId = (ushort)((eth.PayloadData[2] << 8) | eth.PayloadData[3]);
                    string serviceName = Enum.GetName(typeof(ServiceIds), serviceId);
                    logger.log("ServiceName: " + serviceName);

                    // get the xID, the xID is given by PayloadData[4] - PayloadData[7]
                    uint xID = (uint)((eth.PayloadData[4] << 24) | (eth.PayloadData[5] << 16) | (eth.PayloadData[6] << 8) | eth.PayloadData[7]);


                    if (serviceId == (ushort)ServiceIds.Identify_Response)
                    {
                        logger.log("Ein neues Gerät mit der xID " + xID.ToString() + " hat sich angemeldet!");

                        // PayloadData[8] and PayloadData[9] are reserved

                        // the dataLength is given by PayloadData[10] and PayloadData[11]
                        ushort dataLength = (ushort)((eth.PayloadData[10] << 8) | eth.PayloadData[11]);

                        logger.log("\t DataLength: " + dataLength.ToString());
                        byte[] deviceInfo = new byte[dataLength];
                        Array.Copy(eth.PayloadData, 12, deviceInfo, 0, dataLength);
                        handler.registerNewDevice(xID, deviceInfo);
                    }
                }
            }
        }
        public Listener(Logger log, deviceHandler handler)
        {
            logger = log;
            this.handler = handler;
        }
    }
}