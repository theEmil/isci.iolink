using System.Collections.Generic;
using System.Net;

namespace openDCOSIoLink.Models.settings{
    public class Settings
    {
        public string mqttIP { get; set; }
        public bool listenOnAllInterfaces { get; set; }
        public List<string> InterfaceWhiteList { get; set; }
        public int minimunDeviceCount { get; set; }
        public int logLevel { get; set; }
        public List<dataLink> dataMap { get; set; }
        public int slowTimerIntervall { get; set; }
        public int fastTimerIntervall { get; set; }

    }
    public class dataLink{
        // Wildcards für Type deviceData:
            // $NameOfStation = NameOfStation des IO-Link Moduls
            // $deviceSerial = Seriennummer des IO-Link Moduls
            // $deviceID = DeviceID des IO-Link Moduls
        // Zusätzliche Wildcards für Type Sensor:
            // $sensorSerial = Seriennummer des Sensors
            // $sensorDeviceID = DeviceID des Sensors
            // $sensorName = Name des Sensors
            // $sensorProductName = Produktname des Sensors
        public dataLinkType type { get; set; }
        public string eventPattern { get; set; }
        public string dataPattern { get; set; }
        public string MQttBrokerIP { get; set; }
        public string MQTTTopic { get; set; }
        public int Port { get; set; }
        public string duration { get; set; }
        public string variableName { get; set; }
    }
    public enum dataLinkType{
        sensorData,
        deviceData
    }
}