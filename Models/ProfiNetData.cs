using System;
using SharpPcap;
using System.Dynamic;

namespace openDCOSIoLink.Models.ProfiNetData
{
    public class packageUtils
    {
        private MACAddressHandler multicastAddress = new MACAddressHandler();
        public byte[] getRequestIdentity(byte[] senderMacAddress)
        {

            byte[] destination = multicastAddress.MulticastBytes();

            byte[] source = senderMacAddress;

            byte[] type = new byte[] { 0x88, 0x92 };

            byte[] frameID = new byte[] { 0xfe, 0xfe };

            byte[] serviceID = new byte[] { 0x05, 0x00 };

            byte[] xID = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            byte[] responseDelay = new byte[] { 0x00, 0xff };

            byte[] dataLength = new byte[] { 0x00, 0x04 };

            byte[] dataBlock = new byte[] { 0xff, 0xff, 0x00, 0x00 }; //all, all, 0, 0


            byte[] identifyRequest = new byte[30];

            Array.Copy(destination, 0, identifyRequest, 0, 6);
            Array.Copy(source, 0, identifyRequest, 6, 6);
            Array.Copy(type, 0, identifyRequest, 12, 2);
            Array.Copy(frameID, 0, identifyRequest, 14, 2);
            Array.Copy(serviceID, 0, identifyRequest, 16, 2);
            Array.Copy(xID, 0, identifyRequest, 18, 4);
            Array.Copy(responseDelay, 0, identifyRequest, 22, 2);
            Array.Copy(dataLength, 0, identifyRequest, 24, 2);
            Array.Copy(dataBlock, 0, identifyRequest, 26, 4);

            return identifyRequest;
        }
    }
    public class MACAddressHandler
    {
        private static string MulticastMACAdd_Identify_Address = "01-0E-CF-00-00-00";

        public string MulticastAddress()
        {
            return MulticastMACAdd_Identify_Address;
        }

        public byte[] MulticastBytes()
        {
            byte[] MACAddr = new byte[6];
            string[] destinationString = MulticastMACAdd_Identify_Address.Split('-');
            for (int j = 0; j < 6; j++)
            {
                MACAddr[j] = Convert.ToByte(destinationString[j], 16);
            }
            return MACAddr;
        }
    }


    public enum ServiceIds : ushort
    {
        Get_Request = 0x0300,
        Get_Response = 0x0301,
        Set_Request = 0x0400,
        Set_Response = 0x0401,
        Identify_Request = 0x0500,
        Identify_Response = 0x0501,
        Hello_Request = 0x0600,
        ServiceIDNotSupported = 0x0004,
    }
    public enum BlockOptions : ushort
    {
        //IP
        IP_MACAddress = 0x0101,
        IP_IPParameter = 0x0102,
        IP_FullIPSuite = 0x0103,

        //DeviceProperties
        DeviceProperties_DeviceVendor = 0x0201,
        DeviceProperties_NameOfStation = 0x0202,
        DeviceProperties_DeviceID = 0x0203,
        DeviceProperties_DeviceRole = 0x0204,
        DeviceProperties_DeviceOptions = 0x0205,
        DeviceProperties_AliasName = 0x0206,
        DeviceProperties_DeviceInstance = 0x0207,
        DeviceProperties_OEMDeviceID = 0x0208,

        //DHCP
        DHCP_HostName = 0x030C,
        DHCP_VendorSpecificInformation = 0x032B,
        DHCP_ServerIdentifier = 0x0336,
        DHCP_ParameterRequestList = 0x0337,
        DHCP_ClassIdentifier = 0x033C,
        DHCP_DHCPClientIdentifier = 0x033D,
        DHCP_FullyQualifiedDomainName = 0x0351,
        DHCP_UUIDClientIdentifier = 0x0361,
        DHCP_DHCP = 0x03FF,

        //Control
        Control_Start = 0x0501,
        Control_Stop = 0x0502,
        Control_Signal = 0x0503,
        Control_Response = 0x0504,
        Control_FactoryReset = 0x0505,
        Control_ResetToFactory = 0x0506,

        //DeviceInitiative
        DeviceInitiative_DeviceInitiative = 0x0601,

        //AllSelector
        AllSelector_AllSelector = 0xFFFF,
    }
    public enum BlockQualifiers : ushort
    {
        Temporary = 0,
        Permanent = 1,

        ResetApplicationData = 2,
        ResetCommunicationParameter = 4,
        ResetEngineeringParameter = 6,
        ResetAllStoredData = 8,
        ResetDevice = 16,
        ResetAndRestoreData = 18,
    }

    [Flags]
    public enum BlockInfo : ushort
    {
        IpSet = 1,
        IpSetViaDhcp = 2,
        IpConflict = 0x80,
    }

    [Flags]
    public enum DeviceRoles : byte
    {
        Device = 1,
        Controller = 2,
        Multidevice = 4,
        Supervisor = 8,
    }

    public enum BlockErrors : byte
    {
        NoError = 0,
        OptionNotSupported = 1,
        SuboptionNotSupported = 2,
        SuboptionNotSet = 3,
        ResourceError = 4,
        SetNotPossible = 5,
        Busy = 6,
    }
}