using System;
using System.ComponentModel;
using log4net;

namespace NetworkAdapters
{
    public class NetworkAdapter : INotifyPropertyChanged 
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        #region notifyhandler
        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged(string caller)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(caller));
            }
        }
        #endregion

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="The name of the "></param>
        public NetworkAdapter(string IpAddress)
        {
            if (log.IsDebugEnabled) log.Debug("NetworkAdapter("+IpAddress+")");
            // need to go from ip address to adaptername, because wmi does not know the ip address
            if (dotNetNetInfo.Initialise(IpAddress))
            {
                this._name = dotNetNetInfo.NetworkInterfaceCard.Name;
                //NetworkAdapters.WmiEventWatcher.Start(this);
                //NetworkAdapters.netshProcess.Start();
            }
            else if (dotNetNetInfo.Initialise())
            {
                this._name = dotNetNetInfo.NetworkInterfaceCard.Name;
                // is this a good idea? The process WmiPrvSE.exe can 'hang' with cpu usage of 44%
                //NetworkAdapters.WmiEventWatcher.Start(this);
            }
            else
            {
                this._connected = true;
                this._enabled = true;
                this._name = "uninitialized";
                this._Description = "no network adapter found in subnet of " + IpAddress;
            }
        }
        public NetworkAdapter GetNetworkAdapter(string IpAddress)
        {
            return null;
        }
        public void Close()
        {
            //NetworkAdapters.WmiEventWatcher.Stop();
        }

        public override string ToString()
        {
            return this._name  + ", enabled: " + this._enabled + ", connected: " + this._connected + ", speed: " + this._speed + " [" + this._Description + "]";
        }

        /// <summary>
        /// Read status Synchronously, and set all properties
        /// </summary>
        public void RefreshProperties()
        {
            //NetworkAdapters.WmiEventWatcher.RefreshProperties(this);
        }
        private string _name = "";
        private bool _connected = false;
        private bool _enabled = false;
        private string _Description = "";
        private int _speed = 0;

        public string Name
        {
            get { return _name; }
        }
        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                _enabled = value;
                if (log.IsDebugEnabled) log.Debug(this._name + " Enabled = " + this.Enabled);
                RaisePropertyChanged("Enabled");
            }
        }
        public bool Connected
        {
            get { return _connected; }
            set
            {
                _connected = value;
                RaisePropertyChanged("Connected");
            }
        }
        public string Description
        {
            get { return _Description; }
            set
            {
                _Description = value;
                RaisePropertyChanged("Description");
            }
        }
        public int SpeedMb
        {
            set
            {
                _speed = value;
            }
            get
            {
                return _speed;
            }
        }
    }
}
