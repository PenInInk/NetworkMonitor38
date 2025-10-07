using System;
using System.Collections.Generic;
using System.Reflection;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using log4net;

namespace NetworkMonitor.snmp;

public class aUpsArgus : aDevice
{
	public enum argusUpsBatteryStatus
	{
		unknown = 1,
		Normal,
		Low,
		Depleted
	}

	public enum argusUpsOutputSource
	{
		standby = 1,
		line,
		boost2,
		boost1,
		buck1,
		buck2,
		inverter,
		retransfer,
		transfer,
		shutdown,
		bypass
	}

	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	public const string strArgus_upsBatteryStatus = "1.3.6.1.4.1.7309.6.1.2.1";

	public const string strArgus_upsOutputSource = "1.3.6.1.4.1.7309.6.1.4.1";

	public const string strArgus_upsMinutesOnBattery = "1.3.6.1.4.1.7309.6.1.2.3";

	public const string strArgus_upsOutputPercentLoad = "1.3.6.1.4.1.7309.6.1.4.4.1.7";

	public const string strArgus_upsAgentShutdownTrap = "1.3.6.1.4.1.7309.6.1.7.0.3";

	public const string strArgus_upsAgentStartupTrap = "1.3.6.1.4.1.7309.6.1.7.0.2";

	public const string strArgus_upsAlarmsPresent = "1.3.6.1.4.1.7309.6.1.5.1.0";

	private ObjectIdentifier oidUpsBatteryStatus;

	private ObjectIdentifier oidUpsMinutesOnBattery;

	private ObjectIdentifier oidUpsOutputPercentLoad;

	private ObjectIdentifier oidUpsOutputSource;

	private Variable UpsBatteryStatus;

	private Variable UpsMinutesOnBattery;

	private Variable UpsOutputPercentLoad;

	private Variable UpsOutputSource;

	private List<Variable> upsStatesInfo;

	private GetBulkRequestMessage msgScanUps;

	public override void InitOids(Enterprise brand)
	{
		log.Info("InitOids");
		oidUpsOutputSource = new ObjectIdentifier("1.3.6.1.4.1.7309.6.1.4.1");
		oidUpsBatteryStatus = new ObjectIdentifier("1.3.6.1.4.1.7309.6.1.2.1");
		oidUpsMinutesOnBattery = new ObjectIdentifier("1.3.6.1.4.1.7309.6.1.2.3");
		oidUpsOutputPercentLoad = new ObjectIdentifier("1.3.6.1.4.1.7309.6.1.4.4.1.7");
		UpsBatteryStatus = new Variable(oidUpsBatteryStatus);
		UpsMinutesOnBattery = new Variable(oidUpsMinutesOnBattery);
		UpsOutputSource = new Variable(oidUpsOutputSource);
		UpsOutputPercentLoad = new Variable(oidUpsOutputPercentLoad);
		upsStatesInfo = new List<Variable> { UpsBatteryStatus, UpsMinutesOnBattery, UpsOutputSource, UpsOutputPercentLoad };
		foreach (Variable v in upsStatesInfo)
		{
			log.Info("initialized " + v.Id.ToString());
		}
	}

	public aUpsArgus(HostDevice me)
		: base(me)
	{
	}

	public override bool Poll()
	{
		return ScanUps();
	}

	private bool ScanUps()
	{
		ISnmpMessage response = null;
		try
		{
			GetBulkRequestMessage msgScanUps = new GetBulkRequestMessage(version, Messenger.NextMessageId, Messenger.NextRequestId, hostdevice.snmpvalues.CommunityString, 1, 1, upsStatesInfo, snmpV3User.privacy, Messenger.MaxMessageSize, response);
			if (msgScanUps == null)
			{
				msgScanUps = new GetBulkRequestMessage(Messenger.NextRequestId, version, hostdevice.snmpvalues.CommunityString, 1, 2, upsStatesInfo);
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
			log.Error("ScanUps GetResponse returned: " + Enum.GetName(typeof(ErrorCode), errorStatus));
			return false;
		}
		foreach (Variable v in response.Pdu().Variables)
		{
			if (log.IsDebugEnabled)
			{
				log.Debug("ScanUps " + v.Id.ToString() + " " + Enum.GetName(typeof(SnmpType), v.Data.TypeCode) + " = " + v.Data.ToString());
			}
			ProcessData(hostdevice.upsStatus, v);
		}
		return true;
	}

	private void ProcessData(upsStatus upsstate, Variable v)
	{
		string vid = v.Id.ToString();
		if (vid.StartsWith("1.3.6.1.4.1.7309.6.1.2.3"))
		{
			if (v.Data.TypeCode == SnmpType.Integer32)
			{
				setRemainingTime(upsstate, v.Data.ToString());
			}
			else
			{
				log.Error("strArgus_upsMinutesOnBattery: TypeCode " + Enum.GetName(typeof(SnmpType), v.Data.TypeCode));
			}
		}
		else if (vid.StartsWith("1.3.6.1.4.1.7309.6.1.4.1"))
		{
			if (log.IsDebugEnabled)
			{
				log.Debug(string.Format("upsOutputSource = ", v.Data.ToString()));
			}
			if (v.Data.TypeCode == SnmpType.Integer32)
			{
				setOutputSource(upsstate, v.Data.ToString());
			}
		}
		else if (vid.Contains("1.3.6.1.4.1.7309.6.1.2.1"))
		{
			if (log.IsDebugEnabled)
			{
				log.Debug("upsBatteryStatus_test =  " + v.Data.ToString());
			}
			if (v.Data.TypeCode == SnmpType.Integer32)
			{
				setBatteryStatus(upsstate, v.Data.ToString());
			}
		}
		else if (vid.StartsWith("1.3.6.1.4.1.7309.6.1.4.4.1.7"))
		{
			if (v.Data.TypeCode == SnmpType.Integer32)
			{
				setOutputPercentLoad(upsstate, v.Data.ToString());
			}
		}
		else
		{
			if (log.IsDebugEnabled)
			{
				log.Debug("ProcessDataText(" + vid + ")");
			}
			ProcessDataText(upsstate, v);
		}
	}

	private void setRemainingTime(upsStatus upsStatus, string DataReceived)
	{
		if (long.TryParse(DataReceived, out var lngValue))
		{
			if (hostdevice.upsStatus.BatteryRunTimeRemaining != lngValue && lngValue > 0)
			{
				hostdevice.upsStatus.BatteryRunTimeRemaining = lngValue;
				if (log.IsDebugEnabled)
				{
					log.Debug(hostdevice.IP?.ToString() + " setRemainingTime() RemainingTime changed  " + Convert.ToString(hostdevice.upsStatus.BatteryRunTimeRemaining));
				}
				hostdevice.upsStatus.UpdateBatteryRunTimeRequested = true;
			}
		}
		else
		{
			log.Error("SetRemaintingTime(" + DataReceived + ") is not an integer");
		}
		if (log.IsDebugEnabled)
		{
			log.Debug($"setRemainingTime() upsStatus = {hostdevice.upsStatus}");
		}
	}

	private void setOutputSource(upsStatus upsstatus, string strOutputSource)
	{
		if (!int.TryParse(strOutputSource, out var OutputSourceValue))
		{
			log.Error("setOutputSource() Not a number: " + strOutputSource);
			return;
		}
		if (!Enum.IsDefined(typeof(argusUpsOutputSource), OutputSourceValue))
		{
			log.Error($"setOutputSource() Undefined value: {OutputSourceValue}");
			return;
		}
		switch ((argusUpsOutputSource)OutputSourceValue)
		{
		case argusUpsOutputSource.line:
			upsstatus.OnLine = true;
			break;
		case argusUpsOutputSource.shutdown:
			upsstatus.PoweredOff = true;
			upsstatus.OnLine = false;
			break;
		default:
			upsstatus.OnLine = false;
			break;
		}
	}

	private void setBatteryStatus(upsStatus upsstatus, string strBatteryStatus)
	{
		if (int.TryParse(strBatteryStatus, out var upsBatteryStatusValue))
		{
			argusUpsBatteryStatus status = (argusUpsBatteryStatus)upsBatteryStatusValue;
			bool BatteryLowActif = (uint)(status - 3) <= 1u;
			if (hostdevice.upsStatus.LowBattery == BatteryLowActif)
			{
				log.Error("setBatteryStatus() value not integer: " + strBatteryStatus);
				return;
			}
			hostdevice.upsStatus.LowBattery = BatteryLowActif;
			hostdevice.tagvalues.UpdateStatusTagRequested = true;
			if (log.IsDebugEnabled)
			{
				log.Debug("setBatteryStatus() " + hostdevice.IP?.ToString() + " Is battery low actif ?" + Convert.ToString(hostdevice.upsStatus.LowBattery));
			}
		}
		if (!Enum.IsDefined(typeof(argusUpsBatteryStatus), upsBatteryStatusValue))
		{
			log.Error("setBatteryStatus() value undefined: " + upsBatteryStatusValue);
		}
	}

	private void setOutputPercentLoad(upsStatus upsstatus, string strOutputLoad)
	{
		if (!int.TryParse(strOutputLoad, out var upsOutputPercentLoad))
		{
			return;
		}
		bool OverloadActif = upsOutputPercentLoad > 90;
		if (hostdevice.upsStatus.Overload != OverloadActif)
		{
			hostdevice.upsStatus.Overload = OverloadActif;
			if (log.IsDebugEnabled)
			{
				log.Debug(hostdevice.IP?.ToString() + " setOutputPercentLoad() Is Overload actif ?" + Convert.ToString(hostdevice.upsStatus.Overload));
			}
		}
		log.Error("setOutputPercentLoad() conversion error Integer32: upsOutputPercentLoad " + strOutputLoad);
	}

	private void ProcessDataText(upsStatus upsstate, Variable v)
	{
		string strUpsState = v.Data.ToString();
		upsstate.SwitchedOn = true;
		upsstate.PoweredOff = false;
		if (strUpsState.IndexOf("unknown", StringComparison.OrdinalIgnoreCase) < 0)
		{
			if (strUpsState.IndexOf("Online", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				upsstate.OnLine = true;
				upsstate.BatteryInuse = false;
			}
			else if (strUpsState.IndexOf("On Battery", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				upsstate.OnLine = false;
				upsstate.BatteryInuse = true;
			}
			else if (strUpsState.IndexOf("On Boost", StringComparison.OrdinalIgnoreCase) < 0)
			{
				if (strUpsState.IndexOf("Sleeping", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					upsstate.SwitchedOn = false;
					upsstate.PoweredOff = true;
				}
				else if (strUpsState.IndexOf("On Bypass", StringComparison.OrdinalIgnoreCase) < 0)
				{
					if (strUpsState.IndexOf("Rebooting", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						upsstate.PoweredOff = true;
					}
					else if (strUpsState.IndexOf("Standby", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						upsstate.PoweredOff = false;
					}
					else if (strUpsState.IndexOf("On Buck", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						upsstate.PoweredOff = false;
					}
				}
			}
		}
		upsstate.AbnormalCondition = false;
		upsstate.Overload = true;
		upsstate.LowBattery = true;
		upsstate.ReplaceBattery = true;
		upsstate.NoBattery = false;
	}

	public override bool ProcessTrap(Variable trap)
	{
		if (trap != null)
		{
			string id = trap.Id.ToString();
			long lngValue = 0L;
			if (id.StartsWith("1.3.6.1.4.1.7309.6.1.2.1"))
			{
				setBatteryStatus(hostdevice.upsStatus, trap.Data.ToString());
				return true;
			}
			if (id.StartsWith("1.3.6.1.4.1.7309.6.1.4.1"))
			{
				setOutputSource(hostdevice.upsStatus, trap.Data.ToString());
				hostdevice.tagvalues.UpdateStatusTagRequested = true;
				return true;
			}
			if (id.StartsWith("1.3.6.1.4.1.7309.6.1.2.3"))
			{
				if (log.IsDebugEnabled)
				{
					log.Debug(hostdevice.IP?.ToString() + " upsMinutesOnBattery = " + trap.Data.ToString() + ")");
				}
				if (!long.TryParse(trap.Data.ToString(), out lngValue))
				{
					log.Error(hostdevice.IP?.ToString() + " upsMinutesOnBattery = " + trap.Data.ToString() + " is not a number");
					return false;
				}
				if (hostdevice.upsStatus.BatteryRunTimeRemaining != lngValue && lngValue > 0)
				{
					hostdevice.upsStatus.BatteryRunTimeRemaining = lngValue;
					hostdevice.upsStatus.UpdateBatteryRunTimeRequested = true;
					return true;
				}
			}
			else if (id.StartsWith("reboot"))
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
			return false;
		}
		return false;
	}
}
