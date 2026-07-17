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
        public static ObjectIdentifier moxaVrrpStatus = new ObjectIdentifier("1.3.6.1.4.1.8691.6.100.1.16.1.1.1.8"); // ASN_INTEGER
        public static ObjectIdentifier moxaVrrpEnable = new ObjectIdentifier("1.3.6.1.4.1.8691.6.100.1.16.1.1.1.3"); // ASN_INTEGER
        public static ObjectIdentifier moxaVrrpTree = new ObjectIdentifier("1.3.6.1.4.1.8691.6.100.1.16.1.1.1");
        public static ObjectIdentifier moxaLoginFailStatus = new ObjectIdentifier("1.3.6.1.4.1.8691.6.100.1.23.7");

        #region RFC-2787-VRRP
        /* vrrp oper state values:
         * initialize(1) backup(2) master(3)
         * */
        public static ObjectIdentifier genericVrrpOperState = new ObjectIdentifier("1.3.6.1.2.1.68.1.3.1.3"); // Gauge32

        /* vrrp admin state values:
         * up(1) down(2)
         * */
        public static ObjectIdentifier genericVrrpAdminState = new ObjectIdentifier("1.3.6.1.2.1.68.1.3.1.4"); // Gauge32

        #endregion

        #region RFC-6527-VRRPV3
        /* VrrpV3 Operations Status
         * initialize(1) backup(2) master(3)
         * */
        public static ObjectIdentifier VrrpV3OperationsStatus = new ObjectIdentifier("1.3.6.1.2.1.207.1.1.1.1.6"); // Gauge32
        #endregion

    }
}
