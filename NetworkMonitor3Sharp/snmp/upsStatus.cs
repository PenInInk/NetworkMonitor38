namespace NetworkMonitor.snmp;

public class upsStatus
{
	public bool AbnormalCondition;

	public bool BatteryInuse;

	public bool LowBattery;

	public bool NoBattery;

	public bool OnLine;

	public bool Overload;

	public bool PoweredOff;

	public bool ReplaceBattery;

	public bool SwitchedOn;

	public long BatteryRunTimeRemaining;

	public bool SnmpConnection;

	public bool UpdateBatteryRunTimeRequested;

	public override string ToString()
	{
		return $"TimeRemain={UpdateBatteryRunTimeRequested}";
	}
}
