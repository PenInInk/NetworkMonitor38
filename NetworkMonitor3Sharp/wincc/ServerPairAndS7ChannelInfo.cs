using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using log4net;
using Siemens.Industry.ProcessManagement.ManagedODK;

namespace NetworkMonitor.wincc;

public class ServerPairAndS7ChannelInfo
{
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	private static ServerPairAndS7ChannelInfo AlcsStatusInfo = new ServerPairAndS7ChannelInfo();

	private static ServerPairAndS7ChannelInfo newAlcsStatus = new ServerPairAndS7ChannelInfo();

	public Dictionary<string, bool> ServersDown;

	public Dictionary<string, bool> PlcDown;

	public static bool IsS7ConnectionDown
	{
		get
		{
			if (AlcsStatusInfo == null)
			{
				return false;
			}
			if (AlcsStatusInfo.PlcDown != null)
			{
				lock (AlcsStatusInfo.PlcDown)
				{
					if (AlcsStatusInfo.PlcDown.ContainsKey("South") && AlcsStatusInfo.PlcDown["South"])
					{
						return true;
					}
					foreach (bool value in AlcsStatusInfo.PlcDown.Values)
					{
						if (value)
						{
							return true;
						}
					}
				}
			}
			return false;
		}
	}

	public static bool RefuseSmanConnections
	{
		get
		{
			if (newAlcsStatus == null)
			{
				return false;
			}
			lock (AlcsStatusInfo.ServersDown)
			{
				log.Warn("AlcsStatusInfo.ServersDown lock");
				if (AlcsStatusInfo.ServersDown.ContainsKey("South") && AlcsStatusInfo.ServersDown["South"])
				{
					return true;
				}
				log.Warn("AlcsStatusInfo.ServersDown unlock");
			}
			lock (AlcsStatusInfo.PlcDown)
			{
				if (AlcsStatusInfo.PlcDown.ContainsKey("South") && AlcsStatusInfo.PlcDown["South"])
				{
					return true;
				}
			}
			return false;
		}
	}

	public static bool getCriticalEquipmentDown
	{
		get
		{
			if (AlcsStatusInfo.IsCriticalEquipmentDown)
			{
				return true;
			}
			if (AlcsStatusInfo.IsCriticalEquipmentDown)
			{
				return true;
			}
			return false;
		}
	}

	public bool IsCriticalEquipmentDown
	{
		get
		{
			if (ServersDown != null)
			{
				lock (ServersDown)
				{
					if (ServersDown.ContainsKey("South") && ServersDown["South"])
					{
						return true;
					}
				}
			}
			if (PlcDown != null)
			{
				lock (PlcDown)
				{
					if (PlcDown.ContainsKey("South") && PlcDown["South"])
					{
						return true;
					}
				}
			}
			return false;
		}
	}

	public static void Initialize(IEnumerable<string> TagNames)
	{
		string ServerPair = "";
		lock (AlcsStatusInfo.ServersDown)
		{
			AlcsStatusInfo.ServersDown.Clear();
			lock (newAlcsStatus.ServersDown)
			{
				newAlcsStatus.ServersDown.Clear();
				lock (AlcsStatusInfo.PlcDown)
				{
					AlcsStatusInfo.PlcDown.Clear();
					lock (newAlcsStatus.PlcDown)
					{
						newAlcsStatus.PlcDown.Clear();
						foreach (string TagName in TagNames)
						{
							string[] parts = TagName.Split(':');
							if (parts.Length >= 2)
							{
								ServerPair = parts[0];
								if (!AlcsStatusInfo.ServersDown.ContainsKey(ServerPair))
								{
									AlcsStatusInfo.ServersDown.Add(parts[0], value: false);
									AlcsStatusInfo.PlcDown.Add(parts[0], value: false);
									newAlcsStatus.ServersDown.Add(parts[0], value: false);
									newAlcsStatus.PlcDown.Add(parts[0], value: false);
								}
							}
						}
					}
				}
			}
		}
	}

	public static void Dispose()
	{
		AlcsStatusInfo = null;
		newAlcsStatus = null;
	}

	public static List<string> GetRedundantServerStateTagNames()
	{
		List<string> tags = new List<string>();
		if (AlcsStatusInfo != null)
		{
			lock (AlcsStatusInfo.ServersDown)
			{
				foreach (string serverpair in AlcsStatusInfo.ServersDown.Keys)
				{
					if (serverpair.Length > 0)
					{
						tags.Add(serverpair + "::@RedundantServerState");
					}
				}
			}
		}
		return tags;
	}

	public static void ResetLastReadStatus()
	{
		if (newAlcsStatus == null)
		{
			return;
		}
		if (newAlcsStatus.ServersDown != null)
		{
			lock (newAlcsStatus.ServersDown)
			{
				foreach (string name in newAlcsStatus.ServersDown.Keys.ToList())
				{
					newAlcsStatus.ServersDown[name] = false;
				}
			}
		}
		if (newAlcsStatus.PlcDown == null)
		{
			return;
		}
		lock (newAlcsStatus.PlcDown)
		{
			foreach (string name2 in newAlcsStatus.PlcDown.Keys.ToList())
			{
				newAlcsStatus.PlcDown[name2] = false;
			}
		}
	}

	public static void UpdateLastReadStatus(string tagname, DMVarState State)
	{
		switch (State)
		{
		case DMVarState.DM_VARSTATE_INVALID_KEY:
			if (log.IsDebugEnabled)
			{
				log.Debug("UpdateLastReadStatus tag not found: " + tagname);
			}
			break;
		case DMVarState.DM_VARSTATE_SERVERDOWN:
		{
			string[] parts = tagname.Split(':');
			if (parts.Length >= 2)
			{
				if (log.IsDebugEnabled)
				{
					log.Debug("UpdateLastReadStatus server down: " + tagname);
				}
				SetServerDown(parts[0]);
			}
			break;
		}
		case DMVarState.DM_VARSTATE_NOT_ESTABLISHED:
		{
			string[] tagfragments = tagname.Split(':');
			if (tagfragments.Length >= 2)
			{
				if (log.IsDebugEnabled)
				{
					log.Debug("UpdateLastReadStatus plc down: " + tagname);
				}
				SetPlcDown(tagfragments[0]);
			}
			break;
		}
		default:
			if (log.IsDebugEnabled)
			{
				log.Debug("UpdateLastReadStatus " + Enum.GetName(typeof(DMVarState), State) + ": " + tagname);
			}
			break;
		case DMVarState.DM_VARSTATE_OK:
		case DMVarState.DM_VARSTATE_STARTUP_VALUE:
			break;
		}
	}

	public static void SetServerDown(string ServerPair)
	{
		if (newAlcsStatus == null || newAlcsStatus.ServersDown == null || !newAlcsStatus.ServersDown.ContainsKey(ServerPair))
		{
			return;
		}
		lock (newAlcsStatus.ServersDown)
		{
			newAlcsStatus.ServersDown[ServerPair] = true;
		}
	}

	public static void SetPlcDown(string ServerPair)
	{
		if (newAlcsStatus == null || newAlcsStatus.PlcDown == null || !newAlcsStatus.PlcDown.ContainsKey(ServerPair))
		{
			return;
		}
		lock (newAlcsStatus.PlcDown)
		{
			newAlcsStatus.PlcDown[ServerPair] = true;
		}
	}

	public static bool CheckAndRaiseEvents()
	{
		if (AlcsStatusInfo == null)
		{
			return false;
		}
		bool HasChanged = false;
		lock (AlcsStatusInfo.ServersDown)
		{
			lock (newAlcsStatus.ServersDown)
			{
				try
				{
					foreach (KeyValuePair<string, bool> newServerInfo in newAlcsStatus.ServersDown)
					{
						if (newServerInfo.Value == AlcsStatusInfo.ServersDown[newServerInfo.Key])
						{
							continue;
						}
						HasChanged = true;
						if (!newServerInfo.Value)
						{
							if (log.IsDebugEnabled)
							{
								log.Debug("Server back online: " + newServerInfo.Key);
							}
							TagConnections.RequestWriteAllTags = true;
							aWinccConnection.RaiseWinccServerDown(down: false);
						}
						else
						{
							if (log.IsDebugEnabled)
							{
								log.Debug("Server is offline: " + newServerInfo.Key);
							}
							aWinccConnection.RaiseWinccServerDown(down: true);
						}
						AlcsStatusInfo.ServersDown[newServerInfo.Key] = newServerInfo.Value;
					}
				}
				catch (Exception ex)
				{
					log.Error(ex.Message);
				}
			}
		}
		lock (AlcsStatusInfo.PlcDown)
		{
			lock (newAlcsStatus.PlcDown)
			{
				foreach (KeyValuePair<string, bool> newPlcinfo in newAlcsStatus.PlcDown)
				{
					if (AlcsStatusInfo.PlcDown[newPlcinfo.Key] != newPlcinfo.Value)
					{
						if (log.IsDebugEnabled)
						{
							log.Debug("CheckAndRaiseEvents plc  [" + newPlcinfo.Key + "]");
						}
						HasChanged = true;
						if (!newPlcinfo.Value)
						{
							log.Info("PlcDown back online: " + newPlcinfo.Key);
							aWinccConnection.RaisePlcDown(down: false);
						}
						else
						{
							log.Info("PlcDown is offline: " + newPlcinfo.Key);
							aWinccConnection.RaisePlcDown(down: true);
						}
						AlcsStatusInfo.PlcDown[newPlcinfo.Key] = newPlcinfo.Value;
					}
				}
				return HasChanged;
			}
		}
	}

	public ServerPairAndS7ChannelInfo()
	{
		ServersDown = new Dictionary<string, bool>();
		PlcDown = new Dictionary<string, bool>();
	}

	public override string ToString()
	{
		StringBuilder msg = new StringBuilder();
		if (ServersDown != null && PlcDown != null)
		{
			bool servers = false;
			bool plcs = false;
			lock (ServersDown)
			{
				foreach (KeyValuePair<string, bool> ServerPairName in ServersDown)
				{
					if (ServerPairName.Value)
					{
						if (!servers)
						{
							msg.Append("Servers Down:");
							servers = true;
						}
						msg.AppendFormat(" {0}", ServerPairName.Key);
					}
				}
			}
			lock (PlcDown)
			{
				foreach (KeyValuePair<string, bool> ServerPairName2 in PlcDown)
				{
					if (ServerPairName2.Value)
					{
						if (!plcs)
						{
							msg.Append("Plc Down:");
							plcs = true;
						}
						msg.AppendFormat(" {0}", ServerPairName2.Key);
					}
				}
			}
		}
		if (msg.Length == 0)
		{
			msg.Append("All connections Up!");
		}
		return msg.ToString();
	}
}
