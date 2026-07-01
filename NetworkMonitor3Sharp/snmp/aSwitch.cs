using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using log4net;
using log4net.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NetworkMonitor.snmp;

public class aSwitch : aDevice
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    private const string strPortAdminStatus = "1.3.6.1.2.1.2.2.1.7";

    public ObjectIdentifier oidPortAdminStatus = new ObjectIdentifier("1.3.6.1.2.1.2.2.1.7");

    private const string strPortOperStatus = "1.3.6.1.2.1.2.2.1.8";

    public ObjectIdentifier oidPortOperStatus = new ObjectIdentifier("1.3.6.1.2.1.2.2.1.8");

    private const string strRingStatusMSR = "1.3.6.1.4.1.24062.2.2.1.4.4.2.1.6";

    private const string strRingStatusFRNT = "1.3.6.1.4.1.16177.2.1.3.3.1.1.1.7";

    private const string strFaultStateScalance = "1.3.6.1.4.1.4329.20.1.1.1.1.28.1";

    private const string strLEDStateScalance = "1.3.6.1.4.1.4329.20.1.1.1.1.28.3.0";

    public ObjectIdentifier oidRingStatus;

    private bool RingStatusObjectDoesNotExist;

    private bool SensorDoesNotExist;

    private const string strSensorValues = ".1.3.6.1.2.1.99.1.1.1.4";



    public ObjectIdentifier oidSensorValues;

    private List<Variable> myRingStatus = new List<Variable>();

    private List<Variable> mySensorValues = new List<Variable>();

    private byte _CounterScanPortsAdminstrativeStatus;

    private List<ifStatus> lstPortStatus = new List<ifStatus>();

    private StringBuilder reportString = new StringBuilder();

    public override void InitOids(Enterprise brand)
    {
        switch (brand)
        {
            case Enterprise.Korenix:
                oidRingStatus = new ObjectIdentifier("1.3.6.1.4.1.24062.2.2.1.4.4.2.1.6");
                myRingStatus.Add(new Variable(oidRingStatus));
                break;
            case Enterprise.Westermo:
                oidRingStatus = new ObjectIdentifier("1.3.6.1.4.1.16177.2.1.3.3.1.1.1.7");
                myRingStatus.Add(new Variable(oidRingStatus));
                oidSensorValues = new ObjectIdentifier(".1.3.6.1.2.1.99.1.1.1.4");
                mySensorValues.Add(new Variable(oidSensorValues));
                break;
            case Enterprise.ScalanceXR328:
                oidRingStatus = new ObjectIdentifier("1.3.6.1.4.1.4329.20.1.1.1.1.28.1");
                myRingStatus.Add(new Variable(oidRingStatus));
                oidSensorValues = new ObjectIdentifier(".1.3.6.1.2.1.99.1.1.1.4");
                mySensorValues.Add(new Variable(oidSensorValues));
                break;
            case Enterprise.Moxa:
                oidSensorValues = new ObjectIdentifier(".1.3.6.1.2.1.99.1.1.1.4");
                mySensorValues.Add(new Variable(oidSensorValues));

                break;
            default:
                if (log.IsDebugEnabled)
                {

                }
                break;
        }
    }

    public aSwitch(HostDevice me)
        : base(me)
    {
    }

    public override string ToString()
    {
        return base.myIpEndpoint.Address.ToString();
    }
    public override bool Poll()
    {
        ScanRingStatus();
        ScanSensorValues();
        if (this.enterprise == Enterprise.Moxa)
        {
            if (this.version == VersionCode.V3)
            {
                //ScanVRRPStatus();
                if (!hostdevice.vrrpStatus.NotAvailable)
                {
                    GetVrrpBulkStatus();
                }
            }
        }
        if (ScanPorts())
        {
            GUI.TagConnection.UpdatePortTags(ref hostdevice);
            return true;
        }
        return false;
    }

    private bool ScanRingStatus()
    {
        if (RingStatusObjectDoesNotExist)
        {
            return false;
        }
        if (myRingStatus.Count == 0)
        {
            RingStatusObjectDoesNotExist = true;
            if (log.IsInfoEnabled)
            {
                log.Info(hostdevice?.ToString() + " no ReadRingStatus at " + oidRingStatus);
            }
            return false;
        }
        if (hostdevice == null)
        {
            return false;
        }
        if (oidRingStatus == null)
        {
            if (log.IsInfoEnabled)
            {
                log.Info("no ring status not defined. Disabling scan ring status");
            }
            RingStatusObjectDoesNotExist = true;
            return false;
        }
        try
        {
            myRingStatus.Clear();
            ReportMessage response = snmpV3User.GetDiscoveryResponseMessage(SnmpType.GetBulkRequestPdu);
            Messenger.BulkWalk(configuration.SnmpVersion, myIpEndpoint, hostdevice.snmpvalues.CommunityString, snmpV3User.ContextName, oidRingStatus, myRingStatus, configuration.snmpTimeOutMs, configuration.snmpRetries, WalkMode.WithinSubtree, snmpV3User.privacy, response);
        }
        catch (Exception ex)
        {
            if (log.IsInfoEnabled)
            {
                log.Info(hostdevice?.ToString() + " ReadRingStatus " + ex.Message);
            }
            return false;
        }
        foreach (Variable v in myRingStatus)
        {
            ProcessRingStatus(v);
        }
        return true;
    }

    private bool ScanSensorValues()
    {
        if (SensorDoesNotExist)
        {
            return false;
        }
        if (hostdevice == null)
        {
            return false;
        }
        if (oidSensorValues == null)
        {
            return false;
        }
        if (mySensorValues.Count == 0)
        {
            SensorDoesNotExist = true;
            log.Info(hostdevice?.ToString() + " no need to scan SensorValues");
            return false;
        }
        try
        {
            mySensorValues.Clear();
            if (snmpV3User != null)
            {
                ReportMessage response = snmpV3User.GetDiscoveryResponseMessage(SnmpType.GetBulkRequestPdu);
                Messenger.BulkWalk(configuration.SnmpVersion, myIpEndpoint, hostdevice.snmpvalues.CommunityString, snmpV3User.ContextName, oidSensorValues, mySensorValues, configuration.snmpTimeOutMs, configuration.snmpRetries, WalkMode.WithinSubtree, snmpV3User.privacy, response);
            }
        }
        catch (Exception ex)
        {
            if (log.IsInfoEnabled)
            {
                log.Info(hostdevice?.ToString() + " ScanSensorValues " + ex.Message);
            }
            return false;
        }
        if (mySensorValues.Count == 0)
        {
            SensorDoesNotExist = true;
            log.Info(hostdevice?.ToString() + " no SensorValues found");
            return false;
        }
        foreach (Variable v in mySensorValues)
        {
            ProcessSensorValues(v);
        }
        return true;
    }

    private bool ScanPorts()
    {
        if ((!hostdevice.tagvalues.UpdateDisabledTag) || (!hostdevice.snmpvalues.PortOperStatusInitialized))
        {
            hostdevice.snmpvalues.PortOperStatusInitialized = true;
            return getPortOperStatus();
        }
        if (!hostdevice.snmpvalues.PortAdminStatusInitialised)
        {
            _CounterScanPortsAdminstrativeStatus = 0;
            hostdevice.snmpvalues.PortAdminStatusInitialised = true;
            getPortAdminStatus();
        }
        if (++_CounterScanPortsAdminstrativeStatus <= 5)
        {
            return getPortOperStatus();
        }
        _CounterScanPortsAdminstrativeStatus = 0;
        return getPortAdminStatus();
    }

    private List<Variable> myPortOperStatusses = new List<Variable>();
    private bool getPortOperStatus()
    {
        if (log.IsDebugEnabled)
        {
            log.Debug($"{this} {nameof(getPortOperStatus)}");
        }

        myPortOperStatusses.Clear();
        ReportMessage response = snmpV3User.GetDiscoveryResponseMessage(SnmpType.GetBulkRequestPdu);
        Messenger.BulkWalk(version, myIpEndpoint, hostdevice.snmpvalues.CommunityString, snmpV3User.ContextName, oidPortOperStatus, myPortOperStatusses, configuration.snmpTimeOutMs, configuration.snmpRetries, WalkMode.WithinSubtree, snmpV3User.privacy, response);
        return handlePortStatus(myPortOperStatusses, typeof(ifOperStatus));

        /*
        if (version == VersionCode.V3)
        {
            return sharpsnmplib.V3GetPortOperStatus(this);
        }
        if (configuration.snmpBulkWalk)
        {
            return sharpsnmplib.BulkWalkPortOperStatus(this);
        }
        return sharpsnmplib.WalkPortOperStatus(this);
        */

    }
    private List<Variable> myPortAdminStatusses = new List<Variable>();
    private bool getPortAdminStatus()
    {
        if (log.IsDebugEnabled)
        {
            log.Debug("getPortAdminStatus");
        }
        myPortAdminStatusses.Clear();
        ReportMessage response = snmpV3User.GetDiscoveryResponseMessage(SnmpType.GetBulkRequestPdu);
        Messenger.BulkWalk(version, myIpEndpoint, hostdevice.snmpvalues.CommunityString, snmpV3User.ContextName, oidPortAdminStatus, myPortAdminStatusses, configuration.snmpTimeOutMs, configuration.snmpRetries, WalkMode.WithinSubtree, snmpV3User.privacy, response);
        return handlePortStatus(myPortAdminStatusses, typeof(ifAdminStatus));

        /*
        if (version == VersionCode.V3)
        {
            return sharpsnmplib.V3GetPortAdminStatus(this);
        }
        if (configuration.snmpBulkWalk)
        {
            return sharpsnmplib.BulkWalkPortAdminStatus(this);
        }
        return sharpsnmplib.WalkPortAdminStatus(this);
        */
    }

    public bool handlePortStatus(IList<Variable> received, Type portStatusType)
    {
        lstPortStatus.Clear();
        try
        {
            foreach (Variable item in received)
            {
                //if (log.IsDebugEnabled) log.Debug($"{this}: {nameof(handlePortStatus)}: oid={item.Id}, data={item.Data}");
                ifStatus aPortStatus = null;
                aPortStatus = new ifStatus(item, enterprise);
                if (log.IsDebugEnabled) log.Debug($"{this}: {nameof(handlePortStatus)}: {aPortStatus}");
                if (aPortStatus.portNr <= 28 && aPortStatus.portNr > 0)
                {
                    lstPortStatus.Add(aPortStatus);
                }
            }
        }
        catch (Exception ex)
        {
            log.Error(hostdevice.host.IP + " handlePortStatus " + ex.Message);
            return false;
        }
        foreach (ifStatus physicalPort in lstPortStatus)
        {
            if (physicalPort.portStatus != 0)
            {
                //if (log.IsDebugEnabled) log.Debug($"{this}: {nameof(handlePortStatus)}: {physicalPort}");

                if (portStatusType == typeof(ifOperStatus))
                {
                    hostdevice.Ports[physicalPort.portNr - 1].OperStatus = (long)physicalPort.portStatus;
                }
                if (portStatusType == typeof(ifAdminStatus))
                {
                    hostdevice.Ports[physicalPort.portNr - 1].ifAdminStatus = (long)physicalPort.portStatus;
                }
            }
        }
        return true;
    }

    public void ProcessRingStatus(Variable v)
    {
        if (v.Data.TypeCode == SnmpType.NoSuchInstance)
        {
            return;
        }
        if (log.IsDebugEnabled)
        {
            log.Debug(hostdevice.host.HostName + " " + v.Id?.ToString() + " ReadRingStatus= " + (object)v.Data);
        }
        int ringId = 0;
        int superRingRingStatus;
        if (v.Id.ToString().StartsWith("1.3.6.1.4.1.16177.2.1.3.3.1.1.1.7"))
        {
            int iStatus = 0;
            if (int.TryParse(v.Data.ToString(), out iStatus))
            {
                switch (iStatus)
                {
                    case 1:
                        hostdevice.RingStatus = HostDevice.RingStatusValues.AbnormalOpen;
                        break;
                    case 2:
                        hostdevice.RingStatus = HostDevice.RingStatusValues.NormalOk;
                        break;
                    case 0:
                        hostdevice.RingStatus = HostDevice.RingStatusValues.Disabled;
                        break;
                }
            }
        }
        else if (v.Id.ToString().StartsWith("1.3.6.1.4.1.4329.20.1.1.1.1.28.1"))
        {
            int iStatus2 = 0;
            if (int.TryParse(v.Data.ToString(), out iStatus2))
            {
                switch (iStatus2)
                {
                    case 1:
                        hostdevice.RingStatus = HostDevice.RingStatusValues.NormalOk;
                        break;
                    case 2:
                        hostdevice.RingStatus = HostDevice.RingStatusValues.AbnormalOpen;
                        break;
                    case 0:
                        hostdevice.RingStatus = HostDevice.RingStatusValues.Disabled;
                        break;
                }
            }
        }
        else if (int.TryParse(v.Data.ToString(), out superRingRingStatus))
        {
            HostDevice.RingStatusValues RingStatus = (HostDevice.RingStatusValues)superRingRingStatus;
            string text = v.Id.ToString();
            if (int.TryParse(text.Substring(text.Length - 1, 1), out ringId))
            {
                hostdevice.RingStatus = RingStatus;
            }
        }
    }

    public void ProcessSensorValues(Variable v)
    {
        string strId = "";
        if (log.IsDebugEnabled)
        {
            log.Debug(hostdevice.host.HostName + " " + v.Id?.ToString() + " = " + (object)v.Data);
        }
        if (v.Data.TypeCode == SnmpType.NoSuchInstance)
        {
            return;
        }
        strId = v.Id.ToString();
        if (strId.EndsWith(".1000"))
        {
            int temperature = 0;
            if (int.TryParse(v.Data.ToString(), out temperature) && Math.Abs(hostdevice.Temperature - temperature) > 1)
            {
                hostdevice.Temperature = temperature;
                hostdevice.tagvalues.UpdateStatusTagRequested = true;
            }
        }
        else if (strId.EndsWith(".1001"))
        {
            int power1ok = 0;
            if (int.TryParse(v.Data.ToString(), out power1ok))
            {
                switch (power1ok)
                {
                    case 1:
                        hostdevice.Power1ok = true;
                        hostdevice.Power1Failed = false;
                        break;
                    case 2:
                        hostdevice.Power1Failed = true;
                        hostdevice.Power1ok = false;
                        break;
                    case 0:
                    case 3:
                        break;
                }
            }
        }
        else
        {
            if (!strId.EndsWith(".1002"))
            {
                return;
            }
            int power2ok = 0;
            if (int.TryParse(v.Data.ToString(), out power2ok))
            {
                switch (power2ok)
                {
                    case 1:
                        hostdevice.Power2ok = true;
                        hostdevice.Power2Failed = false;
                        break;
                    case 2:
                        hostdevice.Power2ok = false;
                        hostdevice.Power2Failed = true;
                        break;
                    case 0:
                    case 3:
                        break;
                }
            }
        }
    }

    public override bool ProcessTrap(Variable trap)
    {
        if (trap != null)
        {
            string oid = trap.Id.ToString();
            long PortNr = 0L;
            if (oid.StartsWith("interfaces.ifTable.ifEntry.ifOperStatus.") || oid.StartsWith("1.3.6.1.2.1.2.2.1.8"))
            {
                ifStatus aPortStatus = new ifStatus(trap, enterprise);
                if (aPortStatus.portNr <= hostdevice.Ports.Count())
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("+traped OperPortStatus " + oid + " = " + Enum.GetName(typeof(ifOperStatus), aPortStatus.portStatus));
                    }
                    hostdevice.Ports[aPortStatus.portNr - 1].OperStatus = (long)aPortStatus.portStatus;
                    return true;
                }
                if (log.IsDebugEnabled)
                {
                    log.Debug("-traped OperPortStatus " + oid + " = " + Enum.GetName(typeof(ifOperStatus), aPortStatus.portStatus));
                }
            }
            else if (oid.IndexOf("1.3.6.1.4.1.4329.20.1.1.1.1.28.3.0") >= 0)
            {
                if (trap.Data.TypeCode == SnmpType.OctetString)
                {
                    byte[] bLedState = trap.Data.ToBytes();
                    if (bLedState.Length >= 3)
                    {
                        if ((bLedState[3] & 1) > 0)
                        {
                            hostdevice.RingStatus = HostDevice.RingStatusValues.AbnormalOpen;
                            if (log.IsDebugEnabled)
                            {
                                log.Debug(ToString() + " ring abnormal = open");
                            }
                        }
                        else
                        {
                            hostdevice.RingStatus = HostDevice.RingStatusValues.NormalOk;
                            if (log.IsDebugEnabled)
                            {
                                log.Debug(ToString() + " ring ok = closed");
                            }
                        }
                        hostdevice.tagvalues.UpdateStatusTagRequested = true;
                    }
                }
            }
            else if (oid.IndexOf(".16177.4.4") >= 0 || oid.IndexOf(".24062.4.4") >= 0 || oid.IndexOf(".24062.4.3.1") >= 0)
            {
                string[] words = null;
                words = trap.Data.ToString().Split(' ');
                if (words.Length >= 3)
                {
                    if (words[2].ToLower().StartsWith("up"))
                    {
                        PortNr = Convert.ToInt32(words[1]);
                        if (PortNr > 0)
                        {
                            hostdevice.Ports[PortNr - 1].OperStatus = 1L;
                            if (log.IsDebugEnabled)
                            {
                                log.Debug("+traped ");
                            }
                            return true;
                        }
                    }
                    else if (words[2].ToLower().StartsWith("down"))
                    {
                        PortNr = Convert.ToInt32(words[1]);
                        if (long.TryParse(words[1], out PortNr) && PortNr > 0)
                        {
                            if (log.IsDebugEnabled)
                            {
                                log.Debug("+traped: port " + PortNr + " down ");
                            }
                            hostdevice.Ports[PortNr - 1].OperStatus = 2L;
                            return true;
                        }
                    }
                    else if (log.IsDebugEnabled)
                    {
                        log.Debug("port status not found");
                    }
                }
                else if (log.IsDebugEnabled)
                {
                    log.Debug("split[" + words.Length + "]");
                }
            }
            else if (oid.IndexOf(".4.7.1.") >= 0)
            {
                Logging.ReportMessage($"{hostdevice} = {trap.Data.ToString()}", Level.Warn);
                int ringId = int.MaxValue;
                if (int.TryParse(oid.Substring(oid.Length - 1, 1), out ringId) && log.IsDebugEnabled)
                {
                    log.Debug(" received trap ring status: RingID " + ringId + "(oid=" + oid);
                }
                if (trap.Data.ToString().IndexOf("Ring0", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ringId = 0;
                    if (log.IsDebugEnabled)
                    {
                        log.Debug(" received trap ring status: RingID " + ringId + "(data=" + trap.Data.ToString());
                    }
                }
                if (trap.Data.ToString().IndexOf("Ring1", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ringId = 1;
                    if (log.IsDebugEnabled)
                    {
                        log.Debug(" received trap ring status: RingID " + ringId + "(data=" + trap.Data.ToString());
                    }
                }
                if (trap.Data.ToString().IndexOf("abnormal", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hostdevice.RingStatus = HostDevice.RingStatusValues.AbnormalOpen;
                    hostdevice.tagvalues.UpdateStatusTagRequested = true;
                    if (log.IsInfoEnabled)
                    {
                        log.Info(hostdevice?.ToString() + " received trap. Ring id " + ringId + " = Abnormal (Open) ");
                    }
                    return true;
                }
                if (trap.Data.ToString().IndexOf(" normal", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hostdevice.RingStatus = HostDevice.RingStatusValues.NormalOk;
                    hostdevice.tagvalues.UpdateStatusTagRequested = true;
                    if (log.IsInfoEnabled)
                    {
                        log.Info(hostdevice?.ToString() + " received trap. Ring id " + ringId + " = Normal (closed) ");
                    }
                    return true;
                }
            }
            else if (oid.StartsWith("1.3.6.1.4.1.16177.2.1.3.3.1.1.1.7"))
            {
                int iStatus = 0;
                if (int.TryParse(trap.Data.ToString(), out iStatus))
                {
                    switch (iStatus)
                    {
                        case 1:
                            hostdevice.RingStatus = HostDevice.RingStatusValues.AbnormalOpen;
                            break;
                        case 2:
                            hostdevice.RingStatus = HostDevice.RingStatusValues.NormalOk;
                            break;
                        case 0:
                            hostdevice.RingStatus = HostDevice.RingStatusValues.DontKnow;
                            break;
                    }
                    if (log.IsInfoEnabled)
                    {
                        log.Info(hostdevice?.ToString() + " received trap. RingStatus = " + Enum.GetName(typeof(HostDevice.RingStatusValues), hostdevice.RingStatus));
                    }
                }
            }
            else
            {
                if (oid.StartsWith("1.3.6.1.4.1.9.9.46.1.6.1.1.14."))
                {
                    ifStatus aPortStatus2 = new ifStatus(trap, enterprise);
                    hostdevice.Ports[aPortStatus2.portNr - 1].OperStatus = (long)aPortStatus2.portStatus;
                    return true;
                }
                if (log.IsDebugEnabled)
                {
                    log.Debug("trap ignored.");
                }
            }
        }
        return false;
    }

    public bool ScanVRRPStatus()
    {
        if (hostdevice == null)
        {
            return false;
        }
        if ((enterprise != Enterprise.Moxa) && (enterprise != Enterprise.catalistL3Router))
        {
            return false;
        }

        List<Variable> vList = new List<Variable>();
        if (!hostdevice.vrrpStatus.IsInitialized())
        {
            if (enterprise == Enterprise.Moxa)
            {
                vList.Add(new Variable(vrrpStatus.moxaFirmwareVersion));
                vList.Add(new Variable(vrrpStatus.moxaSwitchModel));
                vList.Add(new Variable(vrrpStatus.moxaVrrpEnable));
                vList.Add(new Variable(vrrpStatus.moxaVrrpStatus));
            }
            else if (enterprise == Enterprise.catalistL3Router)
            {
                vList.Add(new Variable(vrrpStatus.genericVrrpAdminState));
                vList.Add(new Variable(vrrpStatus.genericVrrpOperState));
                vList.Add(new Variable(vrrpStatus.VrrpV3OperationsStatus));
            }
            else
            {
                if (enterprise == Enterprise.Moxa)
                {
                    vList.Add(new Variable(vrrpStatus.moxaVrrpEnable));
                    vList.Add(new Variable(vrrpStatus.moxaVrrpStatus));
                }
                else if (enterprise == Enterprise.catalistL3Router)
                {
                    vList.Add(new Variable(vrrpStatus.genericVrrpAdminState));
                    vList.Add(new Variable(vrrpStatus.genericVrrpOperState));
                    vList.Add(new Variable(vrrpStatus.VrrpV3OperationsStatus));
                }
            }
            ISnmpMessage response = null;
            try
            {
                GetBulkRequestMessage msgScanUps = null;
                if (version == VersionCode.V2)
                {
                    msgScanUps = new GetBulkRequestMessage(Messenger.NextMessageId, VersionCode.V2, hostdevice.snmpvalues.CommunityString, vList.Count, 1, vList);
                }
                else if (version == VersionCode.V3)
                {
                    ReportMessage report = null;
                    report = snmpV3User.GetDiscoveryResponseMessage(SnmpType.GetRequestPdu);
                    msgScanUps = new GetBulkRequestMessage(VersionCode.V3, Messenger.NextMessageId, Messenger.NextRequestId, snmpV3User.username, snmpV3User.ContextName, vList.Count, 1, vList, snmpV3User.privacy, Messenger.MaxMessageSize, report);
                }
                response = msgScanUps.GetResponse(configuration.snmpTimeOutMs, myIpEndpoint);
            }
            catch (Lextm.SharpSnmpLib.Messaging.TimeoutException)
            {
                return false;
            }
            catch (Exception ex)
            {
                if (log.IsInfoEnabled)
                {
                    log.Info("Error occurred while scanning VRRP status: " + ex.Message);
                }
            }
            int errorStatus = response.Pdu().ErrorStatus.ToInt32();
            if (errorStatus != 0)
            {
                log.Error($"{nameof(ScanVRRPStatus)} GetResponse {Enum.GetName(typeof(Lextm.SharpSnmpLib.ErrorCode), errorStatus)}");
                return false;
            }
            if (log.IsDebugEnabled) log.Debug($"{nameof(ScanVRRPStatus)} received {response.Pdu().Variables.Count} Variables");
            foreach (Variable v in response.Pdu().Variables)
            {
                ProcessVrrpStatus(v);
            }
            return true;
        }
        return false;
    }
    public bool GetVrrpBulkStatus()
    {

        IList<Variable> result = sharpsnmplib.V3GetBulkRequest(this, new List<Variable> { new Variable(vrrpStatus.moxaVrrpTree) });
        if (result == null)
        {
            if (log.IsDebugEnabled)
                log.Debug($"{ToString()} {nameof(GetVrrpBulkStatus)} no result returned");
            return false;
        }
        if (result.Count == 0)
        {
            if (log.IsDebugEnabled)
                log.Debug($"{ToString()} {nameof(GetVrrpBulkStatus)} no values available");
            return false;
        }
        if (log.IsDebugEnabled)
            log.Debug($"{ToString()} {nameof(GetVrrpBulkStatus)} returned {result.Count} values");
        foreach (Variable v in result)
        {
            ProcessVrrpStatus(v);
        }
        return true;
    }

    private void ProcessVrrpStatus(Variable v)
    {
        if (log.IsDebugEnabled)
        {
            log.Debug($"{ToString()} {v.Id?.ToString()} {(object)v.Data}");
        }
        if (!v.Id.ToString().StartsWith("1.3.6.1.4.1.8691.6.100.1.16.1.1.1"))
        {
            hostdevice.vrrpStatus.NotAvailable = true;
            //log.Info($"{hostdevice} VRRP Status not available");
            return;
        }

        if (v.Id.ToString().StartsWith(vrrpStatus.moxaFirmwareVersion.ToString()))
        {
            log.Info($"{ToString()} FirmwareVersion = {v.Data.ToString()}");
        }
        else if (v.Id == vrrpStatus.moxaSwitchModel)
        {
            log.Info($"Model = {v.Data.ToString()}");
        }
        else if (v.Id.ToString().StartsWith(vrrpStatus.moxaVrrpEnable.ToString()))
        {
            if (v.Data.TypeCode == SnmpType.NoSuchInstance || v.Data.TypeCode == SnmpType.NoSuchObject)
            {
                hostdevice.vrrpStatus.NotAvailable = true;
            }
            else
            {
                hostdevice.vrrpStatus.Available = true;

                if (v.Data.TypeCode == SnmpType.Gauge32)
                {
                    if (log.IsDebugEnabled) log.Warn($"{hostdevice} VRRP Enable has wrong type {v.Data.TypeCode}");
                }
                int iEnable = 0;
                if (int.TryParse(v.Data.ToString(), out iEnable))
                {
                    switch (iEnable)
                    {
                        case 1:
                            hostdevice.vrrpStatus.Enabled = true;
                            hostdevice.vrrpStatus.Disabled = false;
                            break;
                        case 2:
                            hostdevice.vrrpStatus.Enabled = false;
                            hostdevice.vrrpStatus.Disabled = true;
                            break;
                        default:
                            hostdevice.vrrpStatus.Enabled = false;
                            hostdevice.vrrpStatus.Disabled = false;
                            break;
                    }
                }
            }
        }
        else if ((v.Id == vrrpStatus.moxaVrrpStatus) ||
            (v.Id == vrrpStatus.VrrpV3OperationsStatus) ||
            (v.Id == vrrpStatus.genericVrrpOperState))
        {
            if (v.Data.TypeCode == SnmpType.NoSuchInstance || v.Data.TypeCode == SnmpType.NoSuchObject)
            {
                hostdevice.vrrpStatus.NotAvailable = true;
                if (log.IsDebugEnabled) log.Debug($"{hostdevice} VRRP Status not available");
                return;
            }
            else
            {
                hostdevice.vrrpStatus.Available = true;
                if (v.Data.TypeCode == SnmpType.Gauge32)
                {
                    log.Warn($"{hostdevice} VRRP Status has wrong type {v.Data.TypeCode}");
                }
                UInt32 iStatus = 0;
                if (!(UInt32.TryParse(v.Data.ToString(), out iStatus)))
                {
                    log.Error($"{ToString()} {nameof(ProcessVrrpStatus)}: {v.Id} = {v.Data.ToString()}");
                }
                else
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug($"{ToString()} {nameof(ProcessVrrpStatus)}: VRRP Status = {iStatus}");
                    }
                    // 0=init, 1=backup, 2=master
                    switch (iStatus)
                    {
                        case 0:
                            if (hostdevice.vrrpStatus.Master || hostdevice.vrrpStatus.Backup)
                            {
                                log.Info($"{ToString()} VRRP status changed to init or unknown");
                            }
                            hostdevice.vrrpStatus.Master = false;
                            hostdevice.vrrpStatus.Backup = false;
                            break;
                        case 1:
                            if (hostdevice.vrrpStatus.Backup == false)
                            {
                                log.Info($"{ToString()} VRRP status changed to Backup");
                            }
                            hostdevice.vrrpStatus.Master = false;
                            hostdevice.vrrpStatus.Backup = true;
                            break;
                        case 2:
                            if (hostdevice.vrrpStatus.Master == false)
                            {
                                log.Info($"{ToString()} VRRP status changed to Master");
                            }
                            hostdevice.vrrpStatus.Master = true;
                            hostdevice.vrrpStatus.Backup = false;
                            break;
                        default:
                            if (hostdevice.vrrpStatus.Master || hostdevice.vrrpStatus.Backup)
                            {
                                log.Info($"{ToString()} VRRP status changed to unknown value {iStatus}");
                            }
                            hostdevice.vrrpStatus.Master = false;
                            hostdevice.vrrpStatus.Backup = false;
                            break;
                    }
                }
            }
        }
        else
        {
            //log.Error($"{ToString()} {nameof(ProcessVrrpStatus)} {v.Id} = {v.Data.ToString()}");
        }
    }
}
