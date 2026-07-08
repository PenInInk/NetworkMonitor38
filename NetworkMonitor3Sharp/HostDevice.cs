using Lextm.SharpSnmpLib;
using log4net;
using NetworkMonitor.Properties;
using NetworkMonitor.snmp;
using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;

namespace NetworkMonitor;

public class HostDevice
{
    public enum RingStatusValues
    {
        Disabled = 1,
        NormalOk = 2,
        AbnormalOpen = 3,
        DontKnow = 0
    }

    public struct SnmpPort
    {
        public string Description;

        public long Speed;

        public long OperStatus;

        public long ifAdminStatus;

        public long InErrors;

        public long InDiscard;

        public long OutErrors;

        public long OutDiscard;
    }

    public class igmp
    {
        public bool PingAlive;

        public bool ConnectionBrokenAlarm;

        public long PingReceived;

        public long PingFailed;

        public long PingLastDuration;

        public bool ReReadSystem;

        public bool InteralApiError;

        public bool SlowDevice;

        public long SlowPingCounter;

        public DateTime lastConnectionTime = DateTime.Now;

        public double NrMinutesOffline
        {
            get
            {
                if (PingAlive)
                {
                    return 0.0;
                }
                return (DateTime.Now - lastConnectionTime).TotalMinutes;
            }
        }
    }

    public class TagValues
    {
        public string TagName;

        public uint tagUpValue;

        public uint tagDownValue;

        public uint tagStatusConfirmed;

        public bool tagDisabledExist = true;

        public uint tagDisabledValue;

        public bool UpdateDisabledTagRequested;

        public bool UpdateStatusTagRequested;

        public int TagDisabledHasChangedDelayCounter;

        public bool UpdateSystemTagRequested;

        public bool UpdatePortsTagsRequested;

        public bool UpdateDisabledTag = Settings.Default.UpdateDisabledTag;

        #region tag: ALARMS
        public bool AlarmTagExist = true;

        public uint AlarmTagValue = 0;

        public bool AlarmTagUpdateRequested = false;
        #endregion

        public bool ChangedDetected()
        {
            if (UpdateDisabledTagRequested || UpdateStatusTagRequested || UpdateSystemTagRequested || UpdatePortsTagsRequested)
            {
                return true;
            }
            if (AlarmTagUpdateRequested)
            {
                return true;
            }
            return false;
        }
    }

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

        #region tag: ALARMS

        public bool LogInFailed = false;

        public DateTime LogInFailedAlarmTime = DateTime.MinValue;

        #endregion

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

    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    public configuration.HostsDevice host;

    public IPAddress IP;

    public HostDevice mySwitch;

    private RingStatusValues myRingStatus;

    public int Temperature;

    public bool Power1ok;

    public bool Power1Failed;

    public bool Power2ok;

    public bool Power2Failed;

    public bool LogInFailed;

    public SnmpPort[] Ports = new SnmpPort[32];

    public bool ups;

    public bool argus;

    public upsStatus upsStatus = new upsStatus();

    /// <summary>
    /// Moxa EDR-810
    /// </summary>
    public vrrpStatus vrrpStatus = new vrrpStatus();

    public igmp ping = new igmp();

    public PingReply pingReply;

    public TagValues tagvalues = new TagValues();
    public bool TagValuesChangeDetected
    {
        get
        {
            return tagvalues.ChangedDetected();
        }
    }

    public snmpValues snmpvalues = new snmpValues();

    public aDevice data;

    public DateTime NextPollTime = DateTime.MinValue;

    public RingStatusValues RingStatus
    {
        get
        {
            return myRingStatus;
        }
        set
        {
            if (myRingStatus != value)
            {
                log.Info("RingStatus changed from " + myRingStatus.ToString() + " to " + value);
                myRingStatus = value;
            }
        }
    }

    public void Poll()
    {
        if (snmpvalues.sysObjectID.Length == 0)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug(" ScanDeviceNow get SystemOid first ");
            }
            data.getSystem();
            if (snmpvalues.sysObjectID.Length == 0)
            {
                return;
            }
        }
        if (data.Poll())
        {
            snmpvalues.lastSnmpRequest = DateTime.Now;
        }
        if (ping.ReReadSystem)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug(" ScanDeviceNow ReReadSystem ");
            }
            data.getSystem();
        }
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder(host.HostName);
        sb.Append(" ");
        sb.Append(IP.ToString());
        sb.Append(" ");
        if (data.enterprise == aDevice.Enterprise.Unknown)
        {
            sb.Append(snmpvalues.sysObjectID);
        }
        else
        {
            sb.Append(Enum.GetName(typeof(aDevice.Enterprise), data.enterprise));
        }
        if (mySwitch != null)
        {
            sb.Append("\t[");
            sb.Append(mySwitch.IP);
            sb.Append("]");
        }
        if (ping.PingFailed > 0)
        {
            sb.Append("\t (pf=");
            sb.Append(ping.PingFailed);
            sb.Append(")");
        }
        return sb.ToString();
    }
}
