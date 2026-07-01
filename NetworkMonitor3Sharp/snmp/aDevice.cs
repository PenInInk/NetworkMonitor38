using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using log4net;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

namespace NetworkMonitor.snmp;

public class aDevice
{
    public enum Enterprise
    {
        Unknown = 1,
        cisco = 2,
        Hirschmann = 248,
        APC = 318,
        PPC = 935,
        Westermo = 16177,
        Korenix = 24062,
        catalist1000 = 2959,
        catalistL2Switch = 1208,
        catalistL3Router = 2066,
        ScalanceXR328 = 4329,
        Moxa = 8691
    }
    private static string strSystemOID = "1.3.6.1.2.1.1.2";
    private static string strSystemName = "1.3.6.1.2.1.1.5";
    private static string strSystemDescription = "1.3.6.1.2.1.1.6";
    private static string strNrOfInterfaces = "1.3.6.1.2.1.2.1";

    public static ObjectIdentifier oidSystemOID = new ObjectIdentifier(strSystemOID);
    public static ObjectIdentifier oidSystemName = new ObjectIdentifier(strSystemName);
    public static ObjectIdentifier oidSystemDescription = new ObjectIdentifier(strSystemDescription);
    private static ObjectIdentifier oidNrOfInterfaces = new ObjectIdentifier(strNrOfInterfaces);

    public static string sysUpTime = "1.3.6.1.2.1.1.3";
    public static string snmpTrapOID = "1.3.6.1.6.3.1.1.4.1";
    public static string apcTestTrap = "1.3.6.1.4.1.318.0.636";


    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    public HostDevice hostdevice;

    public IPEndPoint myIpEndpoint;

    public VersionCode version = VersionCode.V2;

    public V3User snmpV3User;

    public OctetString CommunityString { get; set; }

    public Enterprise enterprise = Enterprise.Unknown;

    public List<Variable> mySystemInfo = null;

    private ReportMessage DiscoveryReport;

    public Enterprise GetEnterprise(string SystemObjectId)
    {
        if (SystemObjectId.Contains(".8691."))
        {
            return Enterprise.Moxa;
        }
        else if (SystemObjectId.Contains(".9.1.2959"))
        {
            return Enterprise.catalist1000;
        }
        else if (SystemObjectId.Contains(".9.1.1208"))
        {
            return Enterprise.catalistL2Switch;
        }
        else if (SystemObjectId.Contains(".9.1.2066"))
        {
            return Enterprise.catalistL3Router;
        }
        else if (SystemObjectId.Contains(".9.1."))
        {
            return Enterprise.cisco;
        }
        else if (SystemObjectId.Contains(".248"))
        {
            return Enterprise.Hirschmann;
        }
        else if (SystemObjectId.Contains(".16177"))
        {
            return Enterprise.Westermo;
        }
        else if (SystemObjectId.Contains(".24062"))
        {
            return Enterprise.Korenix;
        }
        else if (SystemObjectId.Contains(".318"))
        {
            return Enterprise.APC;
        }
        else if (SystemObjectId.Contains(".4329."))
        {
            return Enterprise.ScalanceXR328;
        }
        else if (SystemObjectId.Contains(".935."))
        {
            return Enterprise.PPC;
        }
        else
        {
            log.Error("SetEnterprise unknown " + SystemObjectId);
            return Enterprise.Unknown;
        }
    }

    public aDevice(HostDevice me)
    {
        this.hostdevice = me;
        this.CommunityString = hostdevice.snmpvalues.CommunityString;
        myIpEndpoint = new IPEndPoint(me.IP, me.snmpvalues.PortSNMP);

        mySystemInfo = new List<Variable> { new Variable(oidSystemOID), new Variable(oidSystemName), new Variable(oidNrOfInterfaces) };

        if (configuration.SnmpVersion == VersionCode.V3)
        {
            version = VersionCode.V3;
            snmpV3User = new V3User(configuration.SnmpCommunity, configuration.AuthentificationPassword, V3User.AuthenticationProtocol.MD5);
            snmpV3User.IpEndpoint = myIpEndpoint;
        }

    }

    public void getSystem()
    {
        sharpsnmplib.getSystem(this);
    }

    public virtual bool Poll()
    {
        return false;
    }

    public void ProcessSystem(Variable v)
    {
        string vid = v.Id.ToString();
        if (log.IsDebugEnabled)
        {
            log.Debug($"{hostdevice.IP} getSystem {v.Id} = {v.Data}");
        }
        if (vid.StartsWith(strSystemName))
        {
            if (v.Data.TypeCode == SnmpType.OctetString)
            {
                string SystemName = v.Data.ToString();
                if (log.IsDebugEnabled)
                {
                    log.Debug(hostdevice?.ToString() + " getSystem SystemName type " + v.Data.TypeCode.ToString() + " = " + v.Data.ToString());
                }
                if (!hostdevice.snmpvalues.SystemName.Equals(SystemName))
                {
                    hostdevice.snmpvalues.SystemName = SystemName;
                    hostdevice.tagvalues.UpdateSystemTagRequested = true;
                }
            }
        }
        else if (vid.StartsWith(strSystemOID))
        {
            if (v.Data.TypeCode == SnmpType.ObjectIdentifier)
            {
                string SystemObjectId = v.Data.ToString();
                hostdevice.snmpvalues.sysObjectID = SystemObjectId;
                enterprise = GetEnterprise(SystemObjectId);
                if (log.IsDebugEnabled)
                {
                    log.Info($"{this.hostdevice.IP} snmp Enterprise {enterprise}");
                }
                if (hostdevice.ups)
                {
                    InitOids(enterprise);
                }
                else
                {
                    InitOids(enterprise);
                }
                hostdevice.tagvalues.UpdateSystemTagRequested = true;
                if (log.IsDebugEnabled)
                {
                    log.Debug(hostdevice?.ToString() + " system OID " + SystemObjectId);
                }
                hostdevice.ping.ReReadSystem = false;
            }
        }
        else if (vid.StartsWith(strSystemDescription))
        {
            if (v.Data.TypeCode == SnmpType.OctetString)
            {
                string SystemDescription = v.Data.ToString();
                if (SystemDescription.Length > 0 && !hostdevice.snmpvalues.Description.Equals(SystemDescription))
                {
                    hostdevice.snmpvalues.Description = SystemDescription;
                    hostdevice.tagvalues.UpdateSystemTagRequested = true;
                    if (log.IsInfoEnabled)
                    {
                        log.Info(hostdevice?.ToString() + " system.sysDescr = " + SystemDescription);
                    }
                }
            }
        }
        else if (vid.StartsWith(strNrOfInterfaces))
        {
            if (v.Data.TypeCode == SnmpType.Integer32)
            {
                try
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug(hostdevice?.ToString() + " nrInterfaces " + v.Id.ToString() + " type " + v.Data.TypeCode.ToString() + " = " + v.Data.ToString());
                    }
                    int NrOfInterfaces = int.Parse(v.Data.ToString());
                    hostdevice.snmpvalues.nrInterfaces = NrOfInterfaces;
                    if (hostdevice.Ports == null)
                    {
                        hostdevice.Ports = new HostDevice.SnmpPort[hostdevice.snmpvalues.nrInterfaces];
                    }
                    else if (hostdevice.Ports.Length < hostdevice.snmpvalues.nrInterfaces)
                    {
                        hostdevice.Ports = new HostDevice.SnmpPort[hostdevice.snmpvalues.nrInterfaces];
                    }
                    return;
                }
                catch (ArgumentNullException)
                {
                    return;
                }
                catch (FormatException)
                {
                    return;
                }
                catch (OverflowException)
                {
                    return;
                }
            }
        }
    }

    public virtual bool ProcessTrap(Variable v)
    {
        return false;
    }

    public virtual void InitOids(Enterprise brand)
    {
    }
}
