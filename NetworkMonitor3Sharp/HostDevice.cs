
using log4net;
using NetworkMonitor.Properties;
using NetworkMonitor.snmp;
using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Web.Configuration;

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

    public bool WANConnected;
    public bool WANDisconnected;

    public bool VPNConnected;
    public bool VPNDisconnected;

    #region need latch for 1 minute
    
    public DateTime ResetTime = DateTime.MinValue;
    private object ResetTimeLock;
    public bool ResetOldLatchedStates()
    {
        if (ResetTime == DateTime.MinValue) return false;
        if (ResetTime > DateTime.Now) return false;

        LogInFailed = false;
        ConfigurationChanged = false;
        SecurityNotification = false;
        FireWallConfigChanged = false;
        FireWallPolicy = false;

        lock (ResetTimeLock)
        {
            ResetTime = DateTime.MinValue;
        }
        return true;
    }
    public bool LogInFailed
    {
        get { return mLogInFailed; }
        set 
        {   if (mLogInFailed == value) return;
            mLogInFailed = value;
            if (value)
                lock(ResetTimeLock)
                    ResetTime = DateTime.Now.AddMinutes(1);
        }
    }
    public bool ConfigurationChanged
    {
        get { return mConfigurationChanged; }
        set
        {
            if (mConfigurationChanged == value) return;
            mConfigurationChanged = value;
            if (value)
                lock (ResetTimeLock) 
                    ResetTime = DateTime.Now.AddMinutes(1);
        }
    }
    public bool SecurityNotification
    {
        get { return mSecurityNotification; }
        set
        {
            if (mSecurityNotification == value) return;
            mSecurityNotification = value;
            if (value)
                lock (ResetTimeLock)
                    ResetTime = DateTime.Now.AddMinutes(1);
        }
    }
    public bool FireWallConfigChanged
    {
        get { return mFireWallConfigChanged; }
        set
        {
            if (mFireWallConfigChanged == value) return;
            mFireWallConfigChanged = value;
            if (value)
                lock (ResetTimeLock)
                    ResetTime = DateTime.Now.AddMinutes(1);
        }
    }
    public bool FireWallPolicy
    {
        get { return mFireWallPolicy; }
        set
        {
            if (mFireWallPolicy == value) return;
            mFireWallPolicy = value;
            if (value)
                lock (ResetTimeLock)
                    ResetTime = DateTime.Now.AddMinutes(1);
        }
    }

    private bool mLogInFailed = false;
    private bool mConfigurationChanged = false;
    private bool mSecurityNotification = false;
    private bool mFireWallConfigChanged = false;
    private bool mFireWallPolicy = false;
    #endregion

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
