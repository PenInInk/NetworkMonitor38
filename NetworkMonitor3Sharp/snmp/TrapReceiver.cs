using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib.Security;
using log4net;
using NetworkAdapters;
//using SnmpSharpNet;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using static NetworkMonitor.snmp.aDevice;

namespace NetworkMonitor
{
    public static class TrapReceiver
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static UdpClient listener;


        public static void Start()
        {
            Task.Run(() => Run());
        }

        private static void Run()
        {
            if (log.IsDebugEnabled)
                log.Debug("Trap Receiver Start. All received trap messages will be logged!");
            else
                log.Info("Trap Receiver Start.");

            // Default SNMP trap listening port (162 requires admin rights, so use >1024 for testing)
            //int port = 1620;

            // Create a UDP listener
            using (listener = new UdpClient(configuration.PortNrTrapReceiver))
            {
                IPEndPoint EndPointTrapReceiver = new IPEndPoint(IPAddress.Any, configuration.PortNrTrapReceiver);
                if (!IpPortHelper.IsUdpPortInUse(EndPointTrapReceiver.Port))
                {
                    log.Warn("traps will not be received. port in use " + configuration.PortNrTrapReceiver);
                    GUI.AddMsgToListView("traps will not be received. port in use " + configuration.PortNrTrapReceiver);
                    return;
                }
                if (GUI.IsGuiVisible)
                {
                    GUI.AddMsgToListView("", DateTime.Now.ToShortDateString(), "start listening to snmp v2 traps on port " + configuration.PortNrTrapReceiver);
                }
                log.Info($"Trap Receiver Listening on port {configuration.PortNrTrapReceiver}");
                while (true)
                {
                    try
                    {
                        // Receive trap packet. blocks until a message is received.
                        byte[] buffer = listener.Receive(ref EndPointTrapReceiver);

                        IPAddress RemoteIpAddress = EndPointTrapReceiver.Address;

                        // Parse SNMP message
                        //ISnmpMessage message = MessageFactory.ParseMessages(buffer, 0, buffer.Length, new UserRegistry())[0];
                        var message = MessageFactory.ParseMessages(buffer, 0, buffer.Length, new UserRegistry())[0];
                        if (message == null) continue;

                        //VersionCode version = message.Version;
 
                        if (message.Parameters != null)
                        {
                            //SecurityParameters parameters = message.Parameters;
                        }
                        HostDevice device = configuration.GetDevice(RemoteIpAddress);
                        if (device == null) continue;

                        bool changedetected = false;
                        if (log.IsDebugEnabled) log.Debug($"{message.Pdu().ToString()}");
                        
                        if (message.Pdu().Variables.Count == 0)
                        {
                            RFC2576GenericTrap gt = GetGenericTrap(message.Pdu());
                            changedetected = device.data.ProcessTrap(gt);
                        }
                        foreach (var variable in message.Pdu().Variables)
                        {
                            if (device.data.ProcessTrap(variable))
                            {
                                changedetected = true;
                            }
                            if (GUI.IsGuiVisible)
                            {
                                GUI.AddMsgToListView(RemoteIpAddress.ToString(), variable.Id.ToString(), variable.Data.ToString());
                            }
                            if (log.IsDebugEnabled)
                            {
                                log.Debug($"Trap received from {RemoteIpAddress}: {variable.Id} = {variable.Data}");
                            }
                        }

                        if (changedetected)
                        {
                            GUI.TagConnection.UpdatePortTags(ref device);
                            if (device.TagValuesChangeDetected)
                            {
                                GUI.TagConnection.Write(GUI.TagConnection.GetChangedTags(ref device));
                            }
                        }
                    }
                    catch (SocketException se)
                    {
                        log.Error(se.Message);
                        GUI.AddMsgToListView("", "Trap receiver", se.Message);
                    }
                    catch (ObjectDisposedException od)
                    {
                        log.Error(od.Message);
                        GUI.AddMsgToListView("", "Trap receiver", od.Message);
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.Message);

                    }
                }
            }
            log.Info("Trap Receiver Terminated");
        }
        
        public static void Stop()
        {
            try
            {
                listener.Close();
            }
            catch { }
        }

        private static RFC2576GenericTrap GetGenericTrap(ISnmpPdu pdu)
        {
            RFC2576GenericTrap genericTrap = RFC2576GenericTrap.unknown;

            if (pdu is TrapV2Pdu)
            {
                TrapV2Pdu trapv2 = (TrapV2Pdu)pdu;
                if (RFC2576GenericTraps.ContainsKey(trapv2.Enterprise))
                {
                    return RFC2576GenericTraps[trapv2.Enterprise];
                }
            }
            else if (pdu is TrapV1Pdu)
            {
                TrapV1Pdu trap1 = (TrapV1Pdu)pdu;
                if (!RFC2576GenericTraps.ContainsKey(trap1.Enterprise))
                {
                    return RFC2576GenericTraps[(trap1.Enterprise)]; 
                }
            }
            return RFC2576GenericTrap.unknown;
        }
    }
}
