using System;
using System.Reflection;
using Lextm.SharpSnmpLib;
using log4net;

namespace NetworkMonitor.snmp;

public class ifStatus
{
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	public byte portNr;

	public ifOperStatus portStatus;

	public bool PortAdminStatusInitialised;

	private static long lngx1000 = 4096L;

	public ifStatus(Variable objSnmp, aDevice.Enterprise company)
	{
		int offSet = 0;
		switch (company)
		{
		case aDevice.Enterprise.catalistL2Switch:
		case aDevice.Enterprise.catalist1000:
			offSet = 10101;
			break;
		case aDevice.Enterprise.catalistL3Router:
			offSet = 8;
			break;
		case aDevice.Enterprise.Westermo:
			offSet = 4096;
			break;
		}
		Extract(objSnmp, offSet, extraLogging: false);
	}

	private void Extract(Variable objSnmp, int ifNrFirstPort, bool extraLogging)
	{
		if (objSnmp == null)
		{
			return;
		}
		if (extraLogging)
		{
			log.Debug($"ifStatus {objSnmp.Id.ToString()} = {objSnmp.Data.ToString()}, offset {ifNrFirstPort} ");
		}
		string[] s = objSnmp.Id.ToString().Split('.');
		if (s == null || !int.TryParse(s[s.Length - 1], out var ifNr))
		{
			return;
		}
		byte pysicalPortNr = getPhysicalPortNr(ifNr, ifNrFirstPort);
		if (extraLogging)
		{
			log.Debug("ifNr = " + ifNr + ", port nr = " + pysicalPortNr + ", " + objSnmp.Id.ToString() + " - " + ifNrFirstPort);
		}
		if (pysicalPortNr <= 0)
		{
			return;
		}
		portNr = pysicalPortNr;
		long lngValue = 0L;
		if (!long.TryParse(objSnmp.Data.ToString(), out lngValue))
		{
			return;
		}
		if (extraLogging)
		{
			log.Debug($"ifStatus port {pysicalPortNr} lngValue  = {lngValue}");
		}
		if (lngValue > lngx1000)
		{
			lngValue -= lngx1000;
		}
		if (lngValue >= 255)
		{
			return;
		}
		byte bValue = (byte)lngValue;
		if (extraLogging)
		{
			log.Debug("ifStatus bValue = " + bValue);
		}
		if (Enum.IsDefined(typeof(ifOperStatus), bValue))
		{
			portStatus = (ifOperStatus)bValue;
			if (extraLogging)
			{
				log.Debug("ifStatus portStatus = " + Enum.GetName(typeof(ifOperStatus), portStatus));
			}
		}
	}

	private byte getPhysicalPortNr(int snmpIfNr, int IfNrFirstPort)
	{
		long lngPortNr = 0L;
		if (snmpIfNr < 30)
		{
			lngPortNr = snmpIfNr;
			if (log.IsDebugEnabled)
			{
				//log.Debug($"getPhysicalPortNr({snmpIfNr},{IfNrFirstPort}) use If Nr {lngPortNr}");
			}
		}
		else if (snmpIfNr > 0 && snmpIfNr >= IfNrFirstPort)
		{
			lngPortNr = snmpIfNr - IfNrFirstPort + 1;
			if (lngPortNr <= 0 || lngPortNr > 28)
			{
				if (log.IsDebugEnabled)
				{
					log.Debug($"getPhysicalPortNr({snmpIfNr},{IfNrFirstPort}) not a physical port nr: {lngPortNr}");
				}
				lngPortNr = 0L;
			}
		}
		return (byte)lngPortNr;
	}

	public override string ToString()
	{
		return "port nr " + portNr + " = " + Enum.GetName(typeof(ifOperStatus), portStatus);
	}
}
