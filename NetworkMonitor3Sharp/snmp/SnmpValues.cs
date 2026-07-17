
using System;
using Lextm.SharpSnmpLib;

namespace NetworkMonitor
{
    public class snmpValues
    {
        public string Description = "";

        public string sysObjectID = "";

        public long nrInterfaces;

        public string SystemName = "";

        public OctetString CommunityString;

        public int PortSNMP = 161;

        public bool PortAdminStatusInitialised;

        public bool PortOperStatusInitialized;

        public int CountSnmpGetTimeOut;

        public int snmpGetNoResponse;

        public DateTime lastSnmpRequest;

        public TimeSpan WaitTimeSnmpGets;


        public snmpValues()
        {
            int waitTime = configuration.PollInterval;
            if (configuration.PollInterval > 1)
            {
                WaitTimeSnmpGets = new TimeSpan(0, 0, waitTime);
            }
            else
            {
                WaitTimeSnmpGets = new TimeSpan(0, 0, 30);
            }
            if (configuration.SnmpCommunity.Length > 0)
            {
                CommunityString = new OctetString(configuration.SnmpCommunity);
            }
            else
            {
                CommunityString = new OctetString("public");
            }
            lastSnmpRequest = DateTime.MinValue;
        }
    }

}
