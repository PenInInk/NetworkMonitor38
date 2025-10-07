using System;
using System.Net.NetworkInformation;
using log4net;
using System.Collections.Generic;

namespace NetworkAdapters
{
    public class dotNetNetInfo
    {
        /*
         * Purpose:     Get the connection status of the Ethernet port. using System.Net.NetworkInformation.NetworkInterface
         * Not working correct. The Port status is only correct at initialisation.
         * Stopped using this class.
         * */
        static private readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static NetworkInterface NetworkInterfaceCard = null;
        public static System.Net.IPAddress IpAddress = null;
        //private event NetworkAvailabilityChangedEventHandler networkChanged;

        /// <summary>
        /// Creates a static object<System.Net.NetworkInformation.NetworkInterface>NetworkInterfaceCard
        /// Properties OperationalStatus are not always correct!!
        /// </summary>
        /// <param name="IpAddress"></param>
        /// <returns></returns>
        public static bool Initialise(string IpAddressFromAnySwitch)
        {
            // we suppose a subnet /24 mask: 255.255.255.0
            string[] strIpAddress = IpAddressFromAnySwitch.Split('.');
            if (strIpAddress.Length != 4)
            {
                log.Error("Verify config file for ConnectionIpAddress " + IpAddressFromAnySwitch);
            }
            else
            {
                byte[] bIpAddress = new byte[4];
                try
                {
                    bIpAddress[0] = Convert.ToByte(strIpAddress[0]);
                    bIpAddress[1] = Convert.ToByte(strIpAddress[1]);
                    bIpAddress[2] = Convert.ToByte(strIpAddress[2]);
                    bIpAddress[3] = Convert.ToByte(strIpAddress[3]);
                }
                catch 
                {
                    log.Error("Verify config file for ConnectionIpAddress " + IpAddressFromAnySwitch);
                    return false;
                }
                foreach (NetworkInterface NI in NetworkInterface.GetAllNetworkInterfaces())
                {
                    switch (NI.NetworkInterfaceType)
                    {
                        case NetworkInterfaceType.Loopback:
                        case NetworkInterfaceType.Tunnel:
                            continue;
                        case NetworkInterfaceType.Ethernet:
                        case NetworkInterfaceType.FastEthernetFx: // 100Base-FX
                        case NetworkInterfaceType.FastEthernetT: // 100Base-T
                        case NetworkInterfaceType.GigabitEthernet:
                            IPInterfaceProperties iip = NI.GetIPProperties();
                            foreach (UnicastIPAddressInformation IpAdInfo in iip.UnicastAddresses)
                            {
                                if (IpAdInfo.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                {
                                    //System.Diagnostics.Trace.WriteLine("cheking " + IpAd.Address);
                                    byte[] configuredAddress = IpAdInfo.Address.GetAddressBytes();
                                    if (configuredAddress[0] != bIpAddress[0])
                                    {
                                        continue;
                                    }
                                    if (configuredAddress[1] != bIpAddress[1])
                                    {
                                        continue;
                                    }
                                    if (configuredAddress[2] != bIpAddress[2])
                                    {
                                        continue;
                                    }
                                    // same subnet as IpAddress
                                    NetworkInterfaceCard = NI;
                                    IpAddress = IpAdInfo.Address;
                                    break;
                                }
                            }
                            break;
                        default:
                            continue;
                    }
                }
            }
            if (NetworkInterfaceCard != null)
            {
                log.Info("Found network adapter [" + NetworkInterfaceCard.Name + "] " + IpAddress);
                return true;
            }
            else
            {
                log.Error("Cannot find any network adapter who's subnet allows " + IpAddressFromAnySwitch);
                return false;
            }
        }

        public static bool Initialise()
        {
            List<NetworkInterface> AdaptersFound = new List<NetworkInterface>();
            bool found;
            // Will not detect disabled adapters!
            foreach (NetworkInterface NI in NetworkInterface.GetAllNetworkInterfaces())
            {
                found = false;
                switch (NI.NetworkInterfaceType)
                {
                    case NetworkInterfaceType.Loopback:
                    case NetworkInterfaceType.Tunnel:
                        continue;
                    case NetworkInterfaceType.Ethernet:
                    case NetworkInterfaceType.FastEthernetFx: // 100Base-FX
                    case NetworkInterfaceType.FastEthernetT: // 100Base-T
                    case NetworkInterfaceType.GigabitEthernet:
                        IPInterfaceProperties iip = NI.GetIPProperties();
                        foreach (UnicastIPAddressInformation IpAd in iip.UnicastAddresses)
                        {
                            if (IpAd.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                //System.Diagnostics.Trace.WriteLine("cheking " + IpAd.Address);
                                found = true;
                                break;
                            }
                        }
                        break;
                    default:
                        continue;
                }
                if (found)
                {
                    AdaptersFound.Add(NI);
                }
            }
            if (AdaptersFound.Count == 1)
            {
                NetworkInterfaceCard = AdaptersFound[0];
                return true;
            }
            return false;
        }

        public static string Name
        {
            get 
            {
                if (NetworkInterfaceCard != null)
                {
                    return NetworkInterfaceCard.Name;
                }
                return "";
            }
        }

        public static bool StartObserving()
        {
            if (NetworkInterfaceCard != null)
            {
                NetworkChange.NetworkAvailabilityChanged += new NetworkAvailabilityChangedEventHandler(networkAvailabilityChanged);
                return true;
            }
            else
            {
                return false;
            }
        }
        public static void StopObserving()
        {
            try
            {
                NetworkChange.NetworkAvailabilityChanged -= new NetworkAvailabilityChangedEventHandler(networkAvailabilityChanged);
            }
            catch { }
        }

        private static void networkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs NetworkAvailability)
        {
            if (log.IsInfoEnabled) log.Info("networkAvailabilityChanged " + NetworkAvailability.IsAvailable);
        }

        public static bool IsNetworkAvailable()
        {
            bool Connected = false;
            if (NetworkInterfaceCard == null)
            {
                if (log.IsErrorEnabled) log.Error("Ethernet PortConnected not available. Verify IpAddress");
            }
            else
            {
                switch (NetworkInterfaceCard.OperationalStatus)
                {
                    case OperationalStatus.Down:
                    case OperationalStatus.NotPresent:
                    case OperationalStatus.Dormant:
                    case OperationalStatus.LowerLayerDown:
                    case OperationalStatus.Unknown:
                    case OperationalStatus.Testing:
                        if (log.IsErrorEnabled) log.Debug("Network interface DOWN");
                        break;
                    case OperationalStatus.Up:
                        if (log.IsErrorEnabled) log.Error("Network interface UP");
                        Connected = true;
                        break;
                    default:
                        break;
                }

            }
            return Connected;
        }
    }
}
