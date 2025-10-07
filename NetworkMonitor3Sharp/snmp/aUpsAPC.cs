using System;
using System.Collections.Generic;
using System.Reflection;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using log4net;
using log4net.Core;

namespace NetworkMonitor.snmp;

public class aUpsAPC : aDevice
{
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	private static string apcUpsBattery = ".318.1.1.1.2";

	private static string apcBattery1 = ".1.3.6.1.4.1.318.1.1.1.2.1.1.0";

	private static string apcUpsAdvBattery = apcUpsBattery + ".2";

	private static string apcBattery2 = ".1.3.6.1.4.1.318.1.1.1.2.2.4.0";

	private static string apcBattery3 = ".1.3.6.1.4.1.318.1.1.1.2.2.3.0";

	private static string upsBasicStateOutputState = ".1.3.6.1.4.1.318.1.1.1.11.1.1.0";

	private static string apcReboot = "iso.org.dod.internet.private.enterprises.318.2.3.3.0";

	private static string mtrapargsString = "1.3.6.1.4.1.318.2.3.3.0";

	private string strUpsBasicStateOutputState = "";

	private string strApcBatteryTimeRemaining = "";

	private ObjectIdentifier oidUpsOutputState;

	private ObjectIdentifier oidUpsBattertyTime;

	private List<Variable> upsStatesInfo;

	private GetBulkRequestMessage msgScanUps;

	public override void InitOids(Enterprise brand)
	{
		switch (brand)
		{
		default:
			return;
		case Enterprise.APC:
			strUpsBasicStateOutputState = "1.3.6.1.4.1.318.1.1.1.11.1";
			strApcBatteryTimeRemaining = "1.3.6.1.4.1.318.1.1.1.2.2.3";
			break;
		case Enterprise.PPC:
			strUpsBasicStateOutputState = ".1.3.6.1.4.1.935.1.1.1.4.1.1.0";
			strApcBatteryTimeRemaining = ".1.3.6.1.4.1.935.1.1.1.2.2.4.0";
			break;
		}
		oidUpsOutputState = new ObjectIdentifier(strUpsBasicStateOutputState);
		oidUpsBattertyTime = new ObjectIdentifier(strApcBatteryTimeRemaining);
		Variable UpsBasicOutputState = new Variable(oidUpsOutputState);
		Variable UpsBatteryTime = new Variable(oidUpsBattertyTime);
		upsStatesInfo = new List<Variable> { UpsBasicOutputState, UpsBatteryTime };
	}

	public aUpsAPC(HostDevice me)
		: base(me)
	{
	}

	public override bool Poll()
	{
		return ScanUpsStatus();
	}

	private bool ScanUpsStatus()
	{
		ISnmpMessage response = null;
		try
		{
			if (version == VersionCode.V2)
			{
				msgScanUps = new GetBulkRequestMessage(Messenger.NextMessageId, VersionCode.V2, hostdevice.snmpvalues.CommunityString, upsStatesInfo.Count, 1, upsStatesInfo);
			}
			else if (version == VersionCode.V3)
			{
				ReportMessage report = null;
				report = snmpV3User.GetDiscoveryResponseMessage(SnmpType.GetRequestPdu);
				msgScanUps = new GetBulkRequestMessage(VersionCode.V3, Messenger.NextMessageId, Messenger.NextRequestId, snmpV3User.username, snmpV3User.ContextName, upsStatesInfo.Count, 1, upsStatesInfo, snmpV3User.privacy, Messenger.MaxMessageSize, report);
			}
			response = msgScanUps.GetResponse(configuration.snmpTimeOutMs, myIpEndpoint);
		}
		catch (Lextm.SharpSnmpLib.Messaging.TimeoutException)
		{
			return false;
		}
		catch (Exception ex2)
		{
			log.Error(hostdevice.IP?.ToString() + " ScanUps: " + ex2.Message);
			return false;
		}
		int errorStatus = response.Pdu().ErrorStatus.ToInt32();
		if (errorStatus != 0)
		{
			log.Error("ScanUps GetResponse " + Enum.GetName(typeof(Lextm.SharpSnmpLib.ErrorCode), errorStatus));
			return false;
		}
		foreach (Variable v in response.Pdu().Variables)
		{
			ProcessVariable(v);
		}
		return true;
	}

	private void ProcessVariable(Variable v)
	{
		string vid = v.Id.ToString();
		if (log.IsDebugEnabled)
		{
			log.Debug("ScanUps " + vid + " type " + v.Data.TypeCode.ToString() + " = " + v.Data.ToString());
		}
		if (vid.StartsWith(strUpsBasicStateOutputState))
		{
			if (v.Data.TypeCode == SnmpType.OctetString)
			{
				ProcessUpsStatus(v.Data.ToString());
				hostdevice.tagvalues.UpdateStatusTagRequested = true;
			}
		}
		else
		{
			if (!vid.StartsWith(strApcBatteryTimeRemaining) || v.Data.TypeCode != SnmpType.TimeTicks)
			{
				return;
			}
			string[] HMS = v.Data.ToString().Split(':');
			if (HMS.Length != 3 || !byte.TryParse(HMS[0], out var hours) || !byte.TryParse(HMS[1], out var minutes) || !byte.TryParse(HMS[2], out var _))
			{
				return;
			}
			long lngValue = minutes + hours * 60;
			if (hostdevice.upsStatus.BatteryRunTimeRemaining != lngValue && lngValue > 0)
			{
				hostdevice.upsStatus.BatteryRunTimeRemaining = lngValue;
				if (log.IsDebugEnabled)
				{
					log.Debug(hostdevice?.ToString() + " Battery RemainingTime changed  " + Convert.ToString(hostdevice.upsStatus.BatteryRunTimeRemaining));
				}
				hostdevice.upsStatus.UpdateBatteryRunTimeRequested = true;
			}
		}
	}

	private void ProcessUpsStatus(string Value)
	{
		try
		{
			char[] chs = Value.ToString().ToCharArray();
			hostdevice.upsStatus.AbnormalCondition = chs[0] == '1';
			hostdevice.upsStatus.BatteryInuse = chs[1] == '1';
			hostdevice.upsStatus.LowBattery = chs[2] == '1';
			hostdevice.upsStatus.OnLine = chs[3] == '1';
			hostdevice.upsStatus.ReplaceBattery = chs[4] == '1';
			hostdevice.upsStatus.Overload = chs[8] == '1';
			hostdevice.upsStatus.SnmpConnection = chs[5] == '1';
			hostdevice.upsStatus.SwitchedOn = chs[18] == '1';
			if (hostdevice.upsStatus.SnmpConnection)
			{
				if (!hostdevice.upsStatus.OnLine & !hostdevice.upsStatus.SwitchedOn)
				{
					hostdevice.upsStatus.PoweredOff = true;
				}
				else if (hostdevice.upsStatus.OnLine | hostdevice.upsStatus.SwitchedOn)
				{
					hostdevice.upsStatus.PoweredOff = false;
				}
			}
		}
		catch (Exception ex)
		{
			if (log.IsInfoEnabled)
			{
				log.Info("setme.upsStatus: " + ex.Message);
			}
		}
	}

	public override bool ProcessTrap(Variable trap)
	{
		if (trap != null)
		{
			long lngValue = 0L;
			if (trap.Id.ToString().StartsWith(mtrapargsString))
			{
				string msg = trap.Data.ToString();
				if (msg.IndexOf("UPS: On battery power", 0, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					hostdevice.upsStatus.BatteryInuse = true;
					if (log.IsDebugEnabled)
					{
						log.Debug(hostdevice.IP?.ToString() + " ProcessTrapUps() BatteryInuse = true");
					}
				}
				else if (msg.IndexOf("UPS: No longer on battery", 0, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					hostdevice.upsStatus.BatteryInuse = false;
					if (log.IsDebugEnabled)
					{
						log.Debug(hostdevice.IP?.ToString() + " ProcessTrapUps() BatteryInuse = false");
					}
				}
			}
			else
			{
				if (trap.Id.ToString().StartsWith(apcBattery1))
				{
					if (log.IsDebugEnabled)
					{
						if (log.IsDebugEnabled)
						{
							log.Debug(hostdevice.IP?.ToString() + " ProcessTrapUps(apcBattery1 = " + lngValue + ")");
						}
					}
					else
					{
						Logging.ReportMessage("TRAP received battery1 update ", Level.Debug);
					}
					long num = lngValue - 1;
					if ((ulong)num <= 3uL)
					{
						switch (num)
						{
						case 1L:
							hostdevice.upsStatus.LowBattery = false;
							hostdevice.upsStatus.NoBattery = false;
							break;
						case 2L:
							hostdevice.upsStatus.LowBattery = true;
							break;
						case 3L:
							hostdevice.upsStatus.LowBattery = true;
							break;
						}
					}
					return true;
				}
				if (trap.Id.ToString().StartsWith(apcBattery2))
				{
					if (log.IsDebugEnabled)
					{
						if (log.IsDebugEnabled)
						{
							log.Debug(hostdevice.IP?.ToString() + " ProcessTrapUps(apcBattery2 = " + lngValue + ")");
						}
					}
					else
					{
						Logging.ReportMessage("TRAP received battery2 update ", Level.Debug);
					}
					switch (lngValue)
					{
					case 1L:
						hostdevice.upsStatus.NoBattery = true;
						break;
					case 2L:
						hostdevice.upsStatus.ReplaceBattery = true;
						break;
					}
					hostdevice.tagvalues.UpdateStatusTagRequested = true;
					return true;
				}
				if (trap.Id.ToString().StartsWith(apcBattery3))
				{
					if (log.IsDebugEnabled)
					{
						if (log.IsDebugEnabled)
						{
							log.Debug(hostdevice.IP?.ToString() + " ProcessTrapUps(apcBattery3 = " + lngValue + ")");
						}
					}
					else
					{
						Logging.ReportMessage("TRAP received battery3 update Battery Remaining Time ", Level.Debug);
					}
					if (long.TryParse(trap.Data.ToString(), out lngValue) && hostdevice.upsStatus.BatteryRunTimeRemaining != lngValue && lngValue > 0)
					{
						hostdevice.upsStatus.BatteryRunTimeRemaining = lngValue;
						Logging.ReportMessage(hostdevice?.ToString() + " Battery RunTime changed  " + Convert.ToString(hostdevice.upsStatus.BatteryRunTimeRemaining), Level.Debug);
						hostdevice.upsStatus.UpdateBatteryRunTimeRequested = true;
						return true;
					}
				}
				else
				{
					if (trap.Id.ToString().StartsWith(upsBasicStateOutputState))
					{
						if (log.IsDebugEnabled)
						{
							if (log.IsDebugEnabled)
							{
								log.Debug(hostdevice.IP?.ToString() + " ProcessTrapUps(upsBasicStateOutputState = " + lngValue + ")");
							}
						}
						else
						{
							ProcessUpsStatus(trap.Data.ToString());
						}
						hostdevice.tagvalues.UpdateStatusTagRequested = true;
						return true;
					}
					if (trap.Id.ToString().StartsWith(apcReboot))
					{
						if (trap.Data.ToString().Contains("turned on"))
						{
							hostdevice.upsStatus.PoweredOff = false;
						}
						else if (trap.Data.ToString().Contains("reboot"))
						{
							hostdevice.upsStatus.PoweredOff = true;
						}
						else if (trap.Data.ToString().Contains("turned off"))
						{
							hostdevice.upsStatus.PoweredOff = true;
						}
						hostdevice.tagvalues.UpdateStatusTagRequested = true;
						return true;
					}
					if (trap.Id.ToString() == "iso.org.dod.internet.6.3.1.1.4.1.0")
					{
						if (trap.Data.ToString().Contains(".1.3.6.1.4.1.318.0.5"))
						{
							Logging.ReportMessage("ON BATTERY", Level.Info);
							hostdevice.upsStatus.BatteryInuse = true;
							return true;
						}
						if (trap.Data.ToString().Contains(".1.3.6.1.4.1.318.0.9"))
						{
							Logging.ReportMessage("NO LONGER ON BATTERY");
							hostdevice.upsStatus.BatteryInuse = false;
							return true;
						}
						if (trap.Data.ToString().Contains(".1.3.6.1.4.1.318.0.7"))
						{
							Logging.ReportMessage("The battery power is too low");
							hostdevice.upsStatus.LowBattery = true;
							return true;
						}
						if (trap.Data.ToString().Contains(".1.3.6.1.4.1.318.0.4"))
						{
							Logging.ReportMessage("Battery power insufficient");
							hostdevice.upsStatus.LowBattery = true;
							return true;
						}
						if (trap.Data.ToString().Contains(".1.3.6.1.4.1.318.0.298"))
						{
							Logging.ReportMessage("The power is back on");
							hostdevice.upsStatus.PoweredOff = false;
							return true;
						}
						if (trap.Data.ToString().Contains(".1.3.6.1.4.1.318.0.299"))
						{
							Logging.ReportMessage("The power is now turned off");
							hostdevice.upsStatus.PoweredOff = true;
							return true;
						}
						if (trap.Data.ToString().Contains(".1.3.6.1.4.1.318.0.732"))
						{
							Logging.ReportMessage("shutdown initiated");
						}
						else if (trap.Data.ToString().Contains(".1.3.6.1.4.1.318.0.38"))
						{
							Logging.ReportMessage("discharged battery condition no longer exists");
							hostdevice.upsStatus.LowBattery = false;
							return true;
						}
					}
				}
			}
		}
		return false;
	}
}
