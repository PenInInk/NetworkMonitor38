using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;


namespace NetworkMonitor.snmp
{
    public class sharpsnmplib
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static Stopwatch chrono = new Stopwatch();

        public static void getSystem(aDevice device)
        {
            string logname = $"{device.myIpEndpoint}";
            if (log.IsDebugEnabled)
            {
                log.Debug($"{logname} getSystem {device.CommunityString},");
            }
            List<Variable> variables = new List<Variable>();

            GetNextRequestMessage request = null;
            try
            {
                request = new GetNextRequestMessage(Messenger.NextRequestId, configuration.SnmpVersion, device.CommunityString, device.mySystemInfo);
            }
            catch (Exception ex)
            {
                log.Error($"{logname}\t{ex.Message}");
            }
            if (request == null)
            {
                return;
            }
            ISnmpMessage reply = null;
            try
            {
                reply = request.GetResponse(configuration.snmpTimeOutMs, device.myIpEndpoint);
            }
            catch (Lextm.SharpSnmpLib.Messaging.TimeoutException)
            {
                device.hostdevice.snmpvalues.CountSnmpGetTimeOut++;
                if (device.hostdevice.snmpvalues.CountSnmpGetTimeOut == 2)
                {
                    log.Error($"{logname} getSystem: {configuration.snmpTimeOutMs} ms timeout {device.hostdevice.snmpvalues.CountSnmpGetTimeOut}");
                }
                return;
            }
            catch (Exception ex3)
            {
                log.Error($"{logname} getSystem: {ex3.Message}");
                return;
            }
            if (reply.Pdu().ErrorStatus.ToInt32() != 0)
            {
                log.Error($"{logname} ErrorCode {reply.Pdu().ErrorStatus} {Enum.GetName(typeof(ErrorCode), reply.Pdu().ErrorStatus)}");
                return;
            }
            if (reply.Pdu().Variables.Count == 1)
            {
                Variable v = reply.Pdu().Variables.First();
                if (v.Id.ToString().StartsWith("1.3.6.1.6.3.15.1.1.3.0"))
                    log.Error($"{logname}unknown username or community string");
                if (v.Id.ToString().StartsWith("1.3.6.1.2.1.1"))
                    log.Error($"{logname} {v.Data.ToString()}");
                return;
            }
            foreach (Variable v in reply.Pdu().Variables)
            {
                device.ProcessSystem(v);
            }
        }

        public static void getSystemBulkRequest(aDevice device)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug(string.Format("{0} {1}", device, "getSystemBulkRequest"));
            }
            ISnmpMessage response = null;
            try
            {
                response = new GetBulkRequestMessage(Messenger.NextRequestId, device.version, device.hostdevice.snmpvalues.CommunityString, device.mySystemInfo.Count, 1, device.mySystemInfo).GetResponse(configuration.snmpTimeOutMs, device.myIpEndpoint);
            }
            catch (Lextm.SharpSnmpLib.Messaging.TimeoutException)
            {
                device.hostdevice.snmpvalues.CountSnmpGetTimeOut++;
                log.Warn($"{device} getSystem: timeout {device.hostdevice.snmpvalues.CountSnmpGetTimeOut} x");
                return;
            }
            catch (Exception ex2)
            {
                log.Error(string.Format("{0} getSystem: {1}", device + "; ", ex2.Message));
                return;
            }
            device.hostdevice.snmpvalues.CountSnmpGetTimeOut = 0;
            int errorStatus = response.Pdu().ErrorStatus.ToInt32();
            if (errorStatus != 0)
            {
                log.Error("ScanSystem GetResponse " + Enum.GetName(typeof(ErrorCode), errorStatus));
                return;
            }
            foreach (Variable v in response.Pdu().Variables)
            {
                device.ProcessSystem(v);
            }
        }
        /*
        public static void GetSystemBulkWalk(aDevice device)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug(string.Format("{0}, {1}, {2}, {3}", device, "GetSystemBulkWalk", device.hostdevice.snmpvalues.CommunityString, device.mySystemInfo.ToString()));
            }
            List<Variable> results = new List<Variable>();
            Discovery discovery = Messenger.GetNextDiscovery(SnmpType.GetBulkRequestPdu);
            ReportMessage response = null;
            try
            {
                response = discovery.GetResponse(60000, device.myIpEndpoint);
                if (log.IsDebugEnabled)
                {
                    log.Debug($"{device} GetSystemBulkWalk response {response.GetType()}");
                }
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
            }
            try
            {
                Messenger.BulkWalk(configuration.SnmpVersion, device.myIpEndpoint, device.snmpV3User.username, device.snmpV3User.ContextName, device.oidSystemOID, results, configuration.snmpTimeOutMs, configuration.snmpRetries, WalkMode.WithinSubtree, device.snmpV3User.privacy, response);
            }
            catch (Exception ex2)
            {
                log.Error(ex2.Message);
                log.Error(ex2.StackTrace);
            }
            foreach (Variable v in results)
            {
                device.ProcessSystem(v);
            }
        }
        */

        /// <summary>
        /// supports SNMP V1 & V2
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        public static bool WalkPortOperStatus(aSwitch device)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug(string.Format("{0}, {1}, {2}, {3}", device.myIpEndpoint, "WalkPortOperStatus", device.CommunityString, device.oidPortOperStatus.ToString()));
            }
            List<Variable> result = new List<Variable>();
            chrono.Reset();
            chrono.Start();
            try
            {
                Messenger.Walk(VersionCode.V2, device.myIpEndpoint, device.CommunityString, device.oidPortOperStatus, result, configuration.snmpTimeOutMs, WalkMode.WithinSubtree);
            }
            catch (Exception ex)
            {
                log.Error(device.myIpEndpoint + " WalkPortOperStatus " + ex.Message);
                return false;
            }
            chrono.Stop();
            if (chrono.ElapsedMilliseconds > 2500 && log.IsDebugEnabled)
            {
                log.Debug(device.myIpEndpoint + " WalkPortOperStatus " + chrono.ElapsedMilliseconds + " ms");
            }
            return device.handlePortStatus(result, typeof(ifOperStatus));
        }

        public static bool BulkWalkPortOperStatus(aSwitch device)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug(string.Format("{0}, {1}, {2}, {3}", device.myIpEndpoint, "BulkWalkPortOperStatus", device.CommunityString, device.oidPortOperStatus.ToString()));
            }
            List<Variable> result = new List<Variable>();

            chrono.Reset();
            chrono.Start();
            try
            {
                ReportMessage response = device.snmpV3User.GetDiscoveryResponseMessage(SnmpType.GetBulkRequestPdu);

                Messenger.BulkWalk(
                    configuration.SnmpVersion,
                    device.myIpEndpoint,
                    device.CommunityString,
                    device.snmpV3User.ContextName,
                    device.oidPortOperStatus,
                    result,
                    configuration.snmpTimeOutMs,
                    configuration.snmpRetries,
                    WalkMode.WithinSubtree,
                    device.snmpV3User.privacy,
                response);

                if (log.IsDebugEnabled)
                {
                    log.Debug($"{device.myIpEndpoint} {device.version} BulkWalkPortOperStatus returned {result.Count} variables");
                }

            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled)
                {
                    log.Error(device.myIpEndpoint + "BulkWalkPortOperStatus " + ex.Message);
                }
                return false;
            }
            chrono.Stop();
            if (chrono.ElapsedMilliseconds > 2000)
            {
                log.Info(device.myIpEndpoint + " BulkWalkPortOperStatus " + chrono.ElapsedMilliseconds + " ms");
            }
            return device.handlePortStatus(result, typeof(ifOperStatus));
        }

        public static bool WalkPortAdminStatus(aSwitch device)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug(string.Format("{0}, {1}, {2}, {3}", device.myIpEndpoint, "WalkPortAdminStatus", device.CommunityString, device.oidPortAdminStatus.ToString()));
            }
            List<Variable> result = new List<Variable>();
            chrono.Reset();
            chrono.Start();
            try
            {
                Messenger.Walk(device.version, device.myIpEndpoint, device.CommunityString, device.oidPortAdminStatus, result, configuration.snmpTimeOutMs, WalkMode.WithinSubtree);
            }
            catch (Exception ex)
            {
                log.Error(device.myIpEndpoint + " WalkPortAdminStatus " + ex.Message);
                return false;
            }
            chrono.Stop();
            if (chrono.ElapsedMilliseconds > 2500 && log.IsDebugEnabled)
            {
                log.Debug(device.myIpEndpoint + " WalkPortAdminStatus " + chrono.ElapsedMilliseconds + " ms");
            }
            device.handlePortStatus(result, typeof(ifAdminStatus));
            return true;
        }

        public static bool BulkWalkPortAdminStatus(aSwitch device)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug(string.Format("{0}, {1}, {2}, {3}", device.myIpEndpoint, "BulkWalkPortAdminStatus", device.CommunityString, device.oidPortAdminStatus.ToString()));
            }
            List<Variable> result = new List<Variable>();
            chrono.Reset();
            chrono.Start();
            try
            {
                ReportMessage response = device.snmpV3User.GetDiscoveryResponseMessage(SnmpType.GetBulkRequestPdu);
                Messenger.BulkWalk(device.version, device.myIpEndpoint, device.CommunityString, device.snmpV3User.ContextName, device.oidPortAdminStatus, result, configuration.snmpTimeOutMs, configuration.snmpRetries, WalkMode.WithinSubtree, device.snmpV3User.privacy, response);
            }
            catch (Exception ex)
            {
                log.Error(device.myIpEndpoint + " BulkWalkPortAdminStatus " + ex.Message);
                return false;
            }
            chrono.Stop();
            if (chrono.ElapsedMilliseconds > 2500)
            {
                log.Info(device.myIpEndpoint + " BulkWalkPortAdminStatus " + chrono.ElapsedMilliseconds + " ms");
            }
            return device.handlePortStatus(result, typeof(ifAdminStatus));
        }

        #region SNMPv3

        public static bool V3GetPortOperStatus(aSwitch device)
        {
            IList<Variable> result = V3GetBulkRequest(device, new List<Variable> { new Variable(device.oidPortOperStatus) });
            if (result == null) return false;
            if (result.Count == 0) return false;
            return device.handlePortStatus(result, typeof(ifOperStatus));
        }

        public static bool V3GetPortAdminStatus(aSwitch device)
        {
            IList<Variable> result = V3GetBulkRequest(device, new List<Variable> { new Variable(device.oidPortAdminStatus) });
            if (result == null) return false;
            if (result.Count == 0) return false;
            return device.handlePortStatus(result, typeof(ifAdminStatus));
        }

        public static bool V3GetSystem(aDevice device)
        {
            IList<Variable> result = V3GetBulkRequest(device, device.mySystemInfo);
            if (result == null) return false;
            if (result.Count <= 0)
            {

                log.Warn($"{device.myIpEndpoint} getSystem: {configuration.snmpTimeOutMs} ms timeout {device.hostdevice.snmpvalues.CountSnmpGetTimeOut} x - verify snmp V3 User & password");
                return false;
            }
            foreach (Variable v in result)
            {
                device.ProcessSystem(v);
            }
            return true;
        }
        public static IList<Variable> V3GetBulkRequest(aDevice device, IList<Variable> oid)
        {
            string logname = $"V3GetBulkRequest {device.myIpEndpoint}";
            if (log.IsDebugEnabled)
            {
                foreach (Variable v in oid)
                {
                    log.Debug($"{logname} {v.Id}");
                }
            }
            ReportMessage DiscoveryResponseReport = device.snmpV3User.GetDiscoveryResponseMessage(SnmpType.GetBulkRequestPdu);
            GetBulkRequestMessage request;
            try
            {
                request = new GetBulkRequestMessage(
                    VersionCode.V3,
                    Messenger.NextMessageId,
                    Messenger.NextRequestId,
                    device.CommunityString,
                    device.snmpV3User.ContextName,
                    0,
                    10,
                    oid,
                    device.snmpV3User.privacy,
                    Messenger.MaxMessageSize,
                    DiscoveryResponseReport);

                ISnmpMessage reply = request.GetResponse(configuration.snmpTimeOutMs, device.myIpEndpoint);
                if (reply.Pdu().Variables.Count == 0)
                {
                    log.Error($"{logname} wrong report message received.");
                    return null;
                }
                if (reply is ReportMessage)
                {
                    if (log.IsDebugEnabled) log.Debug($"{logname} Get again (sync time)");

                    DiscoveryResponseReport = device.snmpV3User.GetDiscoveryResponseMessage(SnmpType.GetBulkRequestPdu, true);

                    // according to RFC 3414, send a second request to sync time. Then try again.
                    request = new GetBulkRequestMessage(
                        VersionCode.V3,
                        Messenger.NextMessageId,
                        Messenger.NextRequestId,
                        device.CommunityString,
                        device.snmpV3User.ContextName,
                        0,
                        10,
                        oid,
                        device.snmpV3User.privacy,
                        Messenger.MaxMessageSize,
                        DiscoveryResponseReport);

                    reply = request.GetResponse(configuration.snmpTimeOutMs, device.myIpEndpoint);

                }

                if (reply.Pdu().ErrorStatus.ToInt32() != 0) // != ErrorCode.NoError
                {
                    log.Error($"{logname}: {Enum.GetName(typeof(ErrorCode), reply.Pdu().ErrorStatus)}");
                }

                IList<Variable> result = reply.Pdu().Variables;
                if (log.IsDebugEnabled) log.Debug($"{logname}  returned {result.Count} variables");
                if (result.Count == 1)
                {
                    var id = result.First().Id;
                    if (id == Messenger.NotInTimeWindow)
                    {
                        //1.3.6.1.2.1.2.2.1.8
                        if (log.IsDebugEnabled) log.Error($"{logname} received NotInTimeWindow.");
                        return null;
                    }
                }
                return result;
            }
            catch (Lextm.SharpSnmpLib.Messaging.TimeoutException)
            {
                device.hostdevice.snmpvalues.CountSnmpGetTimeOut++;
                if (device.hostdevice.snmpvalues.CountSnmpGetTimeOut == 2)
                    log.Warn($"{device} getSystem: timeout {device.hostdevice.snmpvalues.CountSnmpGetTimeOut} x");
                return null;
            }
            catch (Exception ex)
            {
                log.Error($"{logname} {ex.Message}");
            }

            return null;
        }

        #endregion
    }
}
