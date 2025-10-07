using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Lextm.SharpSnmpLib;
using log4net;
using NetworkMonitor.Properties;
using NetworkMonitor.snmp;

namespace NetworkMonitor;

public static class configuration
{
	public struct HostsDevice
	{
		public string IP;

		public string HostName;

		public string Description;
	}

	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	public static HostDevice[] deviceList = new HostDevice[0];

	public static string mySwitchIpAddress = "";

	public static int PingTimeoutMs = 200;

	public static int PingTimeoutSlowDevice;

	public static string WinccServerPairTagUsed = "";

	public static string OPCserverUrl = "";

	public static int PollInterval = 0;

	public static bool OnlyIntegerTags = false;

	public static int snmpTimeOutMs = 1000;

	public static int snmpRetries = 1;

	public static bool snmpListenToTraps = false;

	public static bool snmpBulkWalk = false;

	public static string SnmpCommunity = "";

	public static string AuthentificationPassword = "ADBSafegate_03";

	public static int PortNrTrapReceiver = 162;

	public static bool UpsRFC = false;

	public static VersionCode SnmpVersion = VersionCode.V2;

	public static bool Initialise()
	{
		LoadUserSettings(MirrorSnmpCommunityString: false);
		LogSettings();
		List<HostDevice> devicelist = LoadConfigurationFile();
		if (devicelist != null && devicelist.Count > 0)
		{
			deviceList = devicelist.ToArray();
			SetMySwitch();
			LogDeviceList();
			return true;
		}
		log.Error("No device loaded. Verfy hosts file in application dir, and system32.");
		log.Info("hosts file needs spaces, no tabs. ip address should have no leading zeros.");
		return false;
	}

	private static List<HostDevice> LoadConfigurationFile()
	{
		List<HostsDevice> HostsFound = null;
		string DeviceListFileName = Path.GetDirectoryName(Application.ExecutablePath) + "\\hosts";
		if (File.Exists(DeviceListFileName))
		{
			HostsFound = ReadHosts(DeviceListFileName);
			if (HostsFound.Count > 0)
			{
				log.Info(DeviceListFileName);
				return LoadDeviceList(HostsFound);
			}
		}
		DeviceListFileName = Path.GetDirectoryName(Application.ExecutablePath) + "\\hosts.txt";
		HostsFound = ReadHosts(DeviceListFileName);
		if (File.Exists(DeviceListFileName) && HostsFound.Count > 0)
		{
			log.Info(DeviceListFileName);
			return LoadDeviceList(HostsFound);
		}
		DeviceListFileName = Path.GetDirectoryName(Application.ExecutablePath) + "\\DeviceList.txt";
		HostsFound = ReadHosts(DeviceListFileName);
		if (File.Exists(DeviceListFileName) && HostsFound.Count > 0)
		{
			log.Info(DeviceListFileName);
			return LoadDeviceList(HostsFound);
		}
		DeviceListFileName = Environment.SystemDirectory + "\\drivers\\etc\\hosts";
		HostsFound = ReadHosts(DeviceListFileName);
		if (HostsFound.Count > 0)
		{
			log.Info(DeviceListFileName);
			return LoadDeviceList(HostsFound);
		}
		return new List<HostDevice>();
	}

	public static void Dispose()
	{
	}

	private static void SetMySwitch()
	{
		string[] Addresses = new string[2];
		try
		{
			string[] myswitches = Settings.Default.mySwitch.Split(',');
			Addresses[0] = ((myswitches.Length != 0) ? myswitches[0] : "");
			Addresses[1] = ((myswitches.Length > 1) ? myswitches[1] : "");
		}
		catch (Exception ex)
		{
			log.Error("LoadSettings: " + ex.Message);
		}
		SetMySwitch(Addresses[0]);
		SetMySwitch(Addresses[1]);
	}

	private static void SetMySwitch(string AddressOrName)
	{
		IPAddress ipa = null;
		HostDevice[] array;
		if (AddressOrName.Length > 0 && !IPAddress.TryParse(AddressOrName, out ipa))
		{
			array = deviceList;
			foreach (HostDevice device in array)
			{
				if (device.host.HostName.Equals(AddressOrName, StringComparison.OrdinalIgnoreCase))
				{
					ipa = device.IP;
					break;
				}
			}
		}
		HostDevice mSwitch = GetDevice(ipa);
		if (mSwitch == null || mSwitch.IP == null)
		{
			return;
		}
		byte[] ab = mSwitch.IP.GetAddressBytes();
		array = deviceList;
		foreach (HostDevice device2 in array)
		{
			byte[] abDeviceIp = device2.IP.GetAddressBytes();
			if (abDeviceIp[0] == ab[0] && abDeviceIp[1] == ab[1] && abDeviceIp[2] == ab[2])
			{
				device2.mySwitch = mSwitch;
			}
		}
	}

	private static List<HostDevice> LoadDeviceList(List<HostsDevice> HostsList)
	{
		List<HostDevice> tmpList = new List<HostDevice>();
		if (HostsList != null && HostsList.Count > 0)
		{
			try
			{
				foreach (HostsDevice host in HostsList)
				{
					HostDevice newdevice = new HostDevice();
					if (host.Description.IndexOf("argus", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						newdevice.ups = true;
						newdevice.argus = true;
					}
					else if (host.HostName.ToUpper().Contains("UPS") || host.Description.ToUpper().Contains("UPS"))
					{
						newdevice.ups = true;
					}
					else if (!host.HostName.ToUpper().Contains("SWITCH") && !host.Description.ToUpper().Contains("SWITCH"))
					{
						continue;
					}
					string[] names = host.HostName.Split(':');
					if (names.Length == 3)
					{
						WinccServerPairTagUsed = names[0];
						newdevice.host.HostName = names[2].ToUpper();
					}
					else
					{
						newdevice.host.HostName = host.HostName.ToUpper();
					}
					if (!IPAddress.TryParse(host.IP, out newdevice.IP))
					{
						log.Error("invalid ip address " + host.IP + " for " + host.HostName);
						newdevice.IP = null;
					}
					if (newdevice.IP == null)
					{
						continue;
					}
					newdevice.tagvalues.TagName = host.HostName.ToUpper();
					if (newdevice.ups)
					{
						if (newdevice.argus)
						{
							newdevice.data = new aUpsArgus(newdevice);
						}
						else
						{
							newdevice.data = new aUpsAPC(newdevice);
						}
					}
					else
					{
						newdevice.data = new aSwitch(newdevice);
					}
					tmpList.Add(newdevice);
					if (log.IsDebugEnabled)
					{
						log.Debug(string.Format("Load {0}\tip: {1}, tagname: {2}", newdevice.ups ? "UPS" : "SWITCH", newdevice.IP, newdevice.tagvalues.TagName));
					}
				}
			}
			catch (Exception ex)
			{
				log.Error("Load " + ex.Message);
			}
		}
		return tmpList;
	}

	public static List<HostsDevice> ReadHosts(string FileName)
	{
		List<HostsDevice> hostslist = new List<HostsDevice>();
		if (File.Exists(FileName))
		{
			string Line = null;
			StreamReader myStream = null;
			try
			{
				myStream = new StreamReader(FileName);
			}
			catch (Exception ex)
			{
				if (log.IsInfoEnabled)
				{
					log.Info("Cannot read file from disk. " + ex.Message);
				}
			}
			int LineNr = 0;
			try
			{
				HostsDevice ReadBuffer = default(HostsDevice);
				for (Line = myStream.ReadLine().Trim(); Line != null; Line = myStream.ReadLine())
				{
					LineNr++;
					if ((Line.Length > 0) & !Line.StartsWith("#"))
					{
						string[] fields = Split(Line);
						if (fields.Length >= 2 && fields[0].Length > 0 && fields[1].Length > 0)
						{
							ReadBuffer.IP = fields[0];
							ReadBuffer.HostName = fields[1];
							ReadBuffer.Description = fields[2];
							hostslist.Add(ReadBuffer);
							if (log.IsDebugEnabled)
							{
								log.Debug(ReadBuffer.IP + "  " + ReadBuffer.HostName + " " + ReadBuffer.Description);
							}
						}
					}
				}
			}
			catch (Exception ex2)
			{
				if (log.IsInfoEnabled)
				{
					log.Info("line " + LineNr + ": " + ex2.Message);
				}
				return null;
			}
			myStream.Close();
			if (hostslist.Count == 0 && log.IsInfoEnabled)
			{
				log.Info("Windows hosts file empty");
			}
		}
		return hostslist;
	}

	private static string[] Split(string LineFromHostsFile)
	{
		string[] fields = new string[3];
		int position = 0;
		_ = new char[3] { ' ', '\t', '#' };
		char[] cLine;
		for (cLine = LineFromHostsFile.ToCharArray(); cLine[position] == ' ' || cLine[position] == '\t'; position++)
		{
		}
		int StartIndex = position;
		for (; cLine[position] != ' ' && cLine[position] != '\t'; position++)
		{
		}
		fields[0] = LineFromHostsFile.Substring(StartIndex, position - StartIndex);
		StartIndex = position;
		for (; cLine[position] == ' ' || cLine[position] == '\t'; position++)
		{
		}
		StartIndex = position;
		while (cLine[position] != ' ' && cLine[position] != '\t' && cLine[position] != '#' && position < cLine.Length)
		{
			position++;
			if (position >= cLine.Length)
			{
				break;
			}
		}
		fields[1] = LineFromHostsFile.Substring(StartIndex, position - StartIndex);
		fields[2] = "";
		StartIndex = position;
		if (cLine.Length > position)
		{
			position = LineFromHostsFile.IndexOf("#", StartIndex);
			if (position == -1)
			{
				position = LineFromHostsFile.IndexOf('\t', StartIndex);
				if (position == -1)
				{
					return fields;
				}
			}
			StartIndex = position + 1;
			if (StartIndex >= LineFromHostsFile.Length)
			{
				fields[2] = "";
			}
			else if (position < cLine.Length)
			{
				while ((cLine[position] == ' ' || cLine[position] == '\t' || cLine[position] == '#') && ++position < cLine.Length)
				{
					if (StartIndex >= LineFromHostsFile.Length)
					{
						fields[2] = "";
						continue;
					}
					StartIndex = position;
					fields[2] = LineFromHostsFile.Substring(StartIndex);
				}
			}
		}
		return fields;
	}

	private static void LoadUserSettings(bool MirrorSnmpCommunityString)
	{
		_ = new string[2];
		try
		{
			if (Settings.Default.pingTimeout > 50m)
			{
				PingTimeoutMs = (int)Settings.Default.pingTimeout;
			}
		}
		catch
		{
			PingTimeoutMs = 50;
		}
		if (PingTimeoutMs < 50)
		{
			PingTimeoutMs = 50;
		}
		PingTimeoutSlowDevice = PingTimeoutMs + 3000;
		try
		{
			OPCserverUrl = Settings.Default.OPCserverUrl;
			if (log.IsDebugEnabled)
			{
				log.Debug("LoadSettings OpcServerId = " + OPCserverUrl);
			}
		}
		catch
		{
		}
		if (OPCserverUrl.Contains("OPC.SimaticNET"))
		{
			OnlyIntegerTags = true;
		}
		try
		{
			PollInterval = Settings.Default.PollInterval;
			if (PollInterval < 20)
			{
				PollInterval = 20;
				log.Info("PollInterval smaller than 20");
			}
			else if (PollInterval > 100)
			{
				PollInterval = 100;
				log.Info("PollInterval larger than 100");
			}
		}
		catch
		{
			PollInterval = 30;
		}
		try
		{
			snmpBulkWalk = Settings.Default.snmpBulkWalk;
		}
		catch
		{
			snmpBulkWalk = false;
			log.Info("snmpBulkWalk not loaded. Using default value. snmpTimeOut");
		}
		try
		{
			snmpTimeOutMs = Settings.Default.snmpTimeOut;
		}
		catch
		{
			snmpTimeOutMs = 1000;
			log.Info("snmpTimeOut not loaded. Using default value. snmpTimeOut");
		}
		try
		{
			snmpListenToTraps = Settings.Default.snmpTraps;
		}
		catch
		{
			snmpListenToTraps = false;
			log.Warn("snmpListenToTraps not loaded.c");
		}
		try
		{
			if (Settings.Default.SnmpCommunity.Trim().Length > 0)
			{
				if (MirrorSnmpCommunityString)
				{
					byte[] bCommunitySetting = Encoding.ASCII.GetBytes(Settings.Default.SnmpCommunity.Trim());
					byte[] bCommunity = new byte[bCommunitySetting.Length * 2];
					Array.Copy(bCommunitySetting, bCommunity, bCommunitySetting.Length);
					int position = bCommunitySetting.Length;
					for (int i = bCommunitySetting.Length - 1; i >= 0; i--)
					{
						bCommunity[position++] = bCommunitySetting[i];
					}
					SnmpCommunity = Encoding.ASCII.GetString(bCommunity);
				}
				else
				{
					SnmpCommunity = Settings.Default.SnmpCommunity.Trim();
				}
			}
		}
		catch (Exception ex)
		{
			log.Error("Config file UserSetting SnmpCommunity " + ex.Message);
		}
		try
		{
			PortNrTrapReceiver = (int)Settings.Default.PortNrSnmpTraps;
		}
		catch
		{
			PortNrTrapReceiver = 162;
		}
		try
		{
			if (Settings.Default.SNMPv3)
			{
				SnmpVersion = VersionCode.V3;
			}
		}
		catch
		{
		}
		try { Program.RunStandAlone = Settings.Default.RunStandAlone; }
		catch { }
    }

	private static void LogSettings()
	{
		log.Info("poll interval: " + PollInterval + " seconds");
		log.Info("ping Time Out: " + PingTimeoutMs + " ms");
		log.Info("snmp TimeOutMs: " + snmpTimeOutMs);
		log.Info("snmp BulkWalk: " + snmpBulkWalk);
		log.Info("snmp Listen to traps: " + snmpListenToTraps);
		log.Info("snmp trap receive port nr: " + PortNrTrapReceiver);
		log.Info("snmp version: " + SnmpVersion);
		log.Info("snmp community/user: " + SnmpCommunity);
		if (OPCserverUrl.Length > 0)
		{
			log.Info("OPC server url: " + OPCserverUrl);
		}
		HostDevice[] array = deviceList;
		foreach (HostDevice device in array)
		{
			if (device.mySwitch != null)
			{
				log.Info(device?.ToString() + ", mySwitch: " + device.mySwitch);
			}
		}
	}

	private static void LogDeviceList()
	{
		HostDevice[] array = deviceList;
		foreach (HostDevice device in array)
		{
			log.Info(device);
		}
	}

	public static HostDevice GetDevice(IPAddress address)
	{
		HostDevice[] array = deviceList;
		foreach (HostDevice device in array)
		{
			if (device.IP.Equals(address))
			{
				return device;
			}
		}
		return null;
	}
}
