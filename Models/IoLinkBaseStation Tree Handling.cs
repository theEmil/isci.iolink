using openDCOSIoLink.Models.RESTData;
using System;
using System.Collections.Generic;
using System.Linq;

namespace openDCOSIoLink.Models.BaseStation
{

    public partial class IoLinkBaseStation
    {
        
        
        public bool hasTree()
        {
            if (tree == null)
            {
                return false;
            }
            return true;
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
    }
    

}

