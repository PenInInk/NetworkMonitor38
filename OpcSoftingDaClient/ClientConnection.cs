using System;
using System.Timers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Softing.OPCToolbox.Client;
using Softing.OPCToolbox;

namespace OpcSoftingDaClient
{
    public class ClientConnection
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static OpcClient m_opcClient;

        private string m_OpcServerUrl = "";
        private List<string> m_TagNames = null;
        private Dictionary<string, UInt32> m_TagValuesServer = null;
        bool SyncInterface = false; // use the OPC Sync interface, false = use ASync interface
        private ServerStatus mConnectionStatus = ServerStatus.Disconnected;

        public ServerStatus ConnectionStatus
        {
            set
            {
                if (mConnectionStatus != value)
                {
                    log.Info("ConnectionStatus: " + mConnectionStatus + " -> " + value);
                    mConnectionStatus = value;
                }
            }
            get
            {
                return mConnectionStatus;
            }
        }
        public enum ServerStatus : int
        {
            Connected,
            Disconnected,
            AccessDenied,
            OpcEnumConnectFailed, // connecto to opcenum.exe failed on target computer
            OutOfmemory,
            WaitForDisposal,
            unknown
        }
        
        public ClientConnection(string OpcServerUrl)
        {
            m_OpcServerUrl = OpcServerUrl;
            if (log.IsDebugEnabled) log.Debug("ClienConnection OPC DA Client Softing " + OpcServerUrl);
        }
        public bool Initialise(List<string> TagNames)
        {
            if (log.IsDebugEnabled) log.Debug("ClientConnection Initialise(TagNames)");
            try
            {
                this.m_TagNames = TagNames;
                if (m_opcClient == null)
                {
                    if (log.IsDebugEnabled) log.Debug("ClientConnection Initialise new OpcClient");
                    m_opcClient = new OpcClient();
                }
                if (log.IsDebugEnabled) log.Debug("ClientConnection Initialise InitializeOpc");
                InitializeOpc();
                if (m_opcClient != null)
                {
                    if (log.IsDebugEnabled) log.Debug("ClientConnection Initialise ConnectSession");
                    m_opcClient.ConnectSession();
                    if (log.IsDebugEnabled) log.Debug("<ClienConnection");
                    return true;
                }
            }
            catch (Exception ex)
            {
                log.Error("<ClientConnection Initialise... " + ex.Message);
            }
            return false;
        }
        private void InitializeOpc()
        {

            Softing.OPCToolbox.Trace.FileName = "NetworkMonitorOpcTrace.log";
            Softing.OPCToolbox.Trace.EnableTraceToFile = true;
            Softing.OPCToolbox.Trace.InfoLevelMask = 1;
            Softing.OPCToolbox.Trace.WriteLine((byte)EnumTraceLevel.INF, 255, "test", "init");

            if (log.IsDebugEnabled) log.Debug("ClientConnection InitialiseOpc");
            int result = (int)EnumResultCode.S_OK;
            if (!ResultCode.SUCCEEDED(m_opcClient.Initialize(m_OpcServerUrl)))
            {
                m_opcClient = null;
                return;
            }
            result |= m_opcClient.InitializeDaObjects(this.m_TagNames);
            m_opcClient.ActivateSession(this.SyncInterface);
            m_opcClient.ActivateConnectionMonitor();
        }
        private void SaveLastKnownIntegerValue(string tagname, object tagvalue)
        {
            if (tagvalue.GetType().Equals(typeof(UInt32)))
            {
                if (this.m_TagValuesServer.ContainsKey(tagname))
                {
                    this.m_TagValuesServer[tagname] = (UInt32)tagvalue;
                }
            }
        }

        private void Restore()
        {
            List<KeyValuePair<string, uint>> lst = this.m_TagValuesServer.ToList<KeyValuePair<string, uint>>();
            Write(lst);
        }

        public bool Write(List<KeyValuePair<string, object>> updates)
        {
            if (m_opcClient != null)
            {
                m_opcClient.WriteItems(updates);
            }
            return false;
        }
        private bool Write(List<KeyValuePair<string, UInt32>> updates)
        {
            if (m_opcClient != null)
            {
                m_opcClient.WriteItems(updates);
            }
            return false;
        }
        public void Stop()
        {
            if (log.IsDebugEnabled) log.Debug("ClienConnection Stop");
            Softing.OPCToolbox.Trace.EnableTraceToFile = false;

            if (m_opcClient != null)
            {
                m_opcClient.DeactivateConnectionMonitor();
                m_opcClient.DisconnectSession();
                m_opcClient.Terminate();
                m_opcClient = null;
            }
        }
    }
}
