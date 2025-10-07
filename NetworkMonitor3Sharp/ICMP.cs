using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using log4net;

namespace NetworkMonitor;

internal static class ICMP
{
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	public static void Ping(ref HostDevice device)
	{
		if (Program.RequestShutdown || device == null)
		{
			return;
		}
		int timeout = configuration.PingTimeoutMs;
		PingReply pr = PingHost(device.IP, timeout);
		if (pr == null)
		{
			processPing(ref device, success: false);
			return;
		}
		if (device.pingReply != null)
		{
			if (pr.Status != device.pingReply.Status)
			{
				device.pingReply = pr;
			}
		}
		else
		{
			device.pingReply = pr;
		}
		try
		{
			switch (pr.Status)
			{
			case IPStatus.Success:
				if (pr.RoundtripTime > 300)
				{
					log.Info(device?.ToString() + " ping roundtrip time " + pr.RoundtripTime);
				}
				processPing(ref device, success: true);
				break;
			case IPStatus.TimedOut:
				processPing(ref device, success: false);
				break;
			case IPStatus.BadRoute:
			case IPStatus.BadDestination:
				if (device.ping.PingFailed >= long.MaxValue)
				{
					break;
				}
				device.ping.PingFailed++;
				switch (device.ping.PingFailed)
				{
				case 1L:
					if (log.IsWarnEnabled)
					{
						log.Warn(device?.ToString() + ": destination host unreachable 1x");
					}
					break;
				case 2L:
					if (log.IsWarnEnabled)
					{
						log.Warn(device?.ToString() + ": destination host unreachable ");
					}
					ConnectionBroken(ref device);
					break;
				}
				break;
			case IPStatus.DestinationHostUnreachable:
				if (log.IsDebugEnabled)
				{
					log.Debug(device?.ToString() + ": hardware error");
				}
				break;
			case IPStatus.Unknown:
				if (!device.ping.InteralApiError && device.ping.PingFailed == 0L && device.ping.PingReceived == 0L)
				{
					device.ping.InteralApiError = true;
					if (log.IsInfoEnabled)
					{
						log.Info(device?.ToString() + " ping failed. Verify IP subnet !");
					}
				}
				break;
			default:
				if (log.IsInfoEnabled)
				{
					log.Info(device?.ToString() + " ping failed (failed=" + device.ping.PingFailed + ", received=" + device.ping.PingReceived + ") Error " + Enum.GetName(typeof(IPStatus), pr.Status));
				}
				break;
			}
		}
		catch (Exception ex)
		{
			if (log.IsInfoEnabled)
			{
				log.Info("Ping " + device?.ToString() + ": " + ex.Message);
			}
			return;
		}
		device.pingReply = pr;
	}

	private static void processPing(ref HostDevice device, bool success)
	{
		if (success)
		{
			device.ping.PingAlive = true;
			if (device.ping.PingFailed > 0)
			{
				if (device.ping.PingFailed == 1 && log.IsInfoEnabled)
				{
					log.Info(device?.ToString() + " Ping back ok ");
				}
				ResetAllIcmpCounters();
			}
			else if (device.ping.PingReceived == 0L)
			{
				device.ping.PingReceived = 1L;
				device.ping.ConnectionBrokenAlarm = false;
				if (device.ping.lastConnectionTime.Equals(DateTime.MinValue))
				{
					device.ping.ReReadSystem = true;
				}
				else if (device.ping.NrMinutesOffline > 3.0)
				{
					device.ping.ReReadSystem = true;
				}
				device.ping.lastConnectionTime = DateTime.Now;
				device.tagvalues.UpdateStatusTagRequested = true;
			}
			else if (device.ping.PingReceived < 999)
			{
				device.ping.PingReceived++;
			}
			if (device.pingReply == null)
			{
				return;
			}
			device.ping.PingLastDuration = device.pingReply.RoundtripTime;
			if (device.ping.PingLastDuration > 100)
			{
				if ((device.ping.SlowPingCounter == 3) & !device.ping.SlowDevice)
				{
					device.ping.SlowDevice = true;
				}
				else if (device.ping.SlowPingCounter < 3)
				{
					device.ping.SlowPingCounter++;
				}
			}
			else if (device.ping.PingLastDuration < 50 && device.ping.SlowPingCounter > 0)
			{
				device.ping.SlowPingCounter--;
				if (device.ping.SlowPingCounter == 0L)
				{
					device.ping.SlowDevice = false;
				}
			}
			return;
		}
		device.ping.PingFailed++;
		switch (device.ping.PingFailed)
		{
		case 1L:
			if (device.pingReply != null && log.IsInfoEnabled)
			{
				log.Info(device?.ToString() + " Ping timeout 1x " + Enum.GetName(typeof(IPStatus), device.pingReply.Status));
			}
			break;
		case 2L:
			if (device.pingReply != null && log.IsErrorEnabled)
			{
				log.Error(device?.ToString() + " Ping timeout 2x " + Enum.GetName(typeof(IPStatus), device.pingReply.Status));
			}
			ConnectionBroken(ref device);
			break;
		}
	}

	public static void ConnectionBroken(ref HostDevice device)
	{
		device.ping.ConnectionBrokenAlarm = true;
		device.ping.PingAlive = false;
		device.ping.PingReceived = 0L;
		device.tagvalues.UpdateStatusTagRequested = true;
	}

	private static void ResetAllIcmpCounters()
	{
		if (configuration.deviceList.Length != 0)
		{
			for (int i = 0; i < configuration.deviceList.Length; i++)
			{
				HostDevice obj = configuration.deviceList[i];
				obj.ping.PingFailed = 0L;
				obj.ping.PingReceived = 0L;
			}
		}
	}

	public static void PingAllDevices()
	{
		if (configuration.deviceList.Length == 0)
		{
			return;
		}
		for (int i = 0; i < configuration.deviceList.Length; i++)
		{
			HostDevice device = configuration.deviceList[i];
			Ping(ref device);
			if (Program.RequestShutdown)
			{
				break;
			}
		}
	}

	public static PingReply PingHost(IPAddress nameOrAddress, int msTimeOut)
	{
		Ping pinger = null;
		PingReply reply = null;
		try
		{
			pinger = new Ping();
			reply = pinger.Send(nameOrAddress, msTimeOut);
		}
		catch (PingException)
		{
			reply = null;
		}
		finally
		{
			pinger?.Dispose();
		}
		return reply;
	}
}
