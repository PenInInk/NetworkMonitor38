using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib.Security;
using log4net;
using NetworkAdapters;
using NetworkMonitor;
using SnmpSharpNet;
using System;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;


namespace NetworkMonitor
{
    public static class TrapReceiver
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static UdpClient listener;


        public static void Start()
        {
            Task.Run(() => StartAsync());
        }

        private static void StartAsync()
        {
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
                        ISnmpMessage message = MessageFactory.ParseMessages(buffer, 0, buffer.Length, new UserRegistry())[0];
                        VersionCode version = message.Version;
                        SecurityParameters parameters = message.Parameters;
                        HostDevice device = configuration.GetDevice(RemoteIpAddress);

                        // Print variable bindings
                        bool changedetected = false;
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
                        }
                        if (changedetected)
                        {
                            GUI.TagConnection.UpdatePortTags(ref device);
                            if (device.tagvalues.UpdatePortsTagsRequested || device.tagvalues.UpdateStatusTagRequested || device.tagvalues.UpdateDisabledTagRequested)
                            {
                                GUI.TagConnection.Write(GUI.TagConnection.GetChangedTags(ref device));
                            }
                        }
                    }
                    catch (SocketException se)
                    {
                        log.Error(se.Message);
                        //log.Error(ex.InnerException);
                        GUI.AddMsgToListView("", "Start Trap receiver", se.Message);
                    }
                    catch (ObjectDisposedException od)
                    {
                        log.Error(od.Message);
                        //log.Error(ex.InnerException);
                        GUI.AddMsgToListView("", "Start Trap receiver", od.Message);
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.Message);
                        //log.Error(ex.InnerException);
                        //GUI.AddMsgToListView("", "Start Trap receiver", ex.Message);
                        //GUI.AddMsgToListView("", "Start Trap receiver", ex.InnerException.Message);
                    }
                }
                log.Info("Trap Receiver Terminated");
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
    }
}
