using Lextm.SharpSnmpLib;

namespace NetworkMonitor.snmp
{
    public class vrrpStatus
    {
        /// <summary>
        /// This confirms VRRP MIB is not available on this device. SNMP GET returned noSuchObject or noSuchInstance
        /// </summary>
        public bool NotAvailable = false;
        /// <summary>
        /// This confirms VRRP MIB is available on this device
        /// </summary>
        public bool Available = false;

        #region Data from VRRP MIB
        public bool Enabled = false;
        public bool Disabled = false;
        public bool Master = false;
        public bool Backup = false;
        #endregion

        public void Clear()
        {
            NotAvailable = false;
            Available = false;
            Enabled = false;
            Disabled = false;
            Master = false;
            Backup = false;
        }
        public bool IsInitialized()
        {
            return !NotAvailable && !Available;
        }

        public static ObjectIdentifier moxaSwitchModel = new ObjectIdentifier("1.3.6.1.4.1.8691.6.100.1.22.2.0"); // String
        public static ObjectIdentifier moxaFirmwareVersion = new ObjectIdentifier("1.3.6.1.4.1.8691.6.100.1.22.4.0"); // String
        public static ObjectIdentifier moxaVrrpStatus = new ObjectIdentifier("1.3.6.1.4.1.8691.6.100.1.16.1.1.1.8.1"); // ASN_INTEGER
        public static ObjectIdentifier moxaVrrpEnable = new ObjectIdentifier("1.3.6.1.4.1.8691.6.100.1.16.1.1.1.3.1"); // ASN_INTEGER
        public static ObjectIdentifier moxaVrrpTree = new ObjectIdentifier("1.3.6.1.4.1.8691.6.100.1.16.1.1.1");
    }
}
