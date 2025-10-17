using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib.Security;
using log4net;
using System.Net;
using System.Reflection;

namespace NetworkMonitor.snmp;

public class V3User
{
    public enum SecurityLevel
    {
        NoAuthNoPriv = 1,
        AuthNoPriv,
        AuthPriv
    }

    public enum AuthenticationProtocol
    {
        MD5 = 1,
        SHA
    }

    public enum PrivacyProtocol
    {
        DES = 1,
        AES
    }

    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    private IPEndPoint myIpEndpoint;

    private Discovery discovery;

    private ReportMessage discoveryReportMessage;

    public SecurityLevel snmpv3SecurityLevel = SecurityLevel.NoAuthNoPriv;

    public OctetString username;

    public OctetString ContextName = new OctetString(string.Empty);

    public OctetString contextEngineId = new OctetString("");

    public IAuthenticationProvider auth;

    public IPrivacyProvider privacy;

    public IPEndPoint IpEndpoint
    {
        get
        {
            return myIpEndpoint;
        }
        set
        {
            myIpEndpoint = value;
        }
    }

    public V3User(string userName)
    {
        snmpv3SecurityLevel = SecurityLevel.NoAuthNoPriv;
        username = new OctetString(userName);
        auth = DefaultAuthenticationProvider.Instance;
        privacy = new DefaultPrivacyProvider(auth);
    }

    public V3User(string userName, string authPassword, AuthenticationProtocol authenticationprotocol)
    {
        username = new OctetString(userName);
        if (authPassword == string.Empty)
        {
            auth = DefaultAuthenticationProvider.Instance;
            snmpv3SecurityLevel = SecurityLevel.NoAuthNoPriv;
            log.Info("snmp v3 no password used");
            return;
        }
        if (authPassword.Length < 13)
        {
            auth = DefaultAuthenticationProvider.Instance;
            snmpv3SecurityLevel = SecurityLevel.NoAuthNoPriv;
            log.Error("Password should be min 13 characters");
            return;
        }
        switch (authenticationprotocol)
        {
            case AuthenticationProtocol.MD5:
                auth = new MD5AuthenticationProvider(new OctetString(authPassword));
                snmpv3SecurityLevel = SecurityLevel.AuthNoPriv;
                break;
            case AuthenticationProtocol.SHA:
                auth = new SHA1AuthenticationProvider(new OctetString(authPassword));
                snmpv3SecurityLevel = SecurityLevel.AuthNoPriv;
                break;
            default:
                auth = DefaultAuthenticationProvider.Instance;
                snmpv3SecurityLevel = SecurityLevel.NoAuthNoPriv;
                break;
        }
        privacy = new DefaultPrivacyProvider(auth);
    }

    public void EnableEncryption(string privPassword, PrivacyProtocol privacyprotocol)
    {
        if (!(privPassword == string.Empty) && privPassword.Length == 8)
        {
            switch (privacyprotocol)
            {
                case PrivacyProtocol.DES:
                    privacy = new DESPrivacyProvider(new OctetString(privPassword), auth);
                    break;
                case PrivacyProtocol.AES:
                    privacy = new AESPrivacyProvider(new OctetString(privPassword), auth);
                    break;
            }
        }
    }

    public ReportMessage GetDiscoveryResponseMessage(SnmpType snmpType, bool Refresh)
    {
        if ((discoveryReportMessage != null) & !Refresh)
        {
            return discoveryReportMessage;
        }
        discovery = Messenger.GetNextDiscovery(snmpType);
        try
        {
            discoveryReportMessage = discovery.GetResponse(2000, myIpEndpoint);
        }
        catch (Lextm.SharpSnmpLib.Messaging.TimeoutException)
        {
            return null;
        }
        catch (System.Exception ex)
        {
            log.Error($"{myIpEndpoint} GetNextDiscovery: Exception {ex.Message}");
            return null;
        }
        if (discoveryReportMessage == null)
        {
            log.Error($"{myIpEndpoint} GetNextDiscovery: no response");
            return null;
        }
        if (log.IsDebugEnabled)
        {
            log.Debug($"GetNextDiscovery({snmpType}) {discoveryReportMessage}");
        }
        SecurityParameters secParams = discoveryReportMessage.Parameters;
        if (secParams.EngineId == null)
        {
            log.Error($"{myIpEndpoint} GetNextDiscovery: Configure an Engine Id in the device");
            return null;
        }
        if (log.IsDebugEnabled)
        {
            log.Debug(string.Format("{0} username: {1} EngineTime: {2} EngineBoots: {3} EngineId: {4}", "GetDiscoveryMessage", secParams.UserName, secParams.EngineTime, secParams.EngineBoots, secParams.EngineId.ToHexString()));
            log.Debug("GetDiscoveryMessage AuthenticationParameters: " + secParams.AuthenticationParameters.ToString());
        }
        return discoveryReportMessage;
    }
    public ReportMessage GetDiscoveryResponseMessage(SnmpType snmpType)
    {
        return GetDiscoveryResponseMessage(snmpType, false);
    }
}
