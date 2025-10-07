using System.Reflection;
using log4net;
using log4net.Core;

namespace NetworkMonitor;

internal static class Logging
{
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	public static void ReportMessage(string Message)
	{
		log.Info(Message);
	}

	public static void ReportMessage(string Message, Level Level)
	{
		if (Level == Level.Error)
		{
			if (log.IsErrorEnabled)
			{
				log.Error(Message);
			}
		}
		else if (Level == Level.Warn)
		{
			if (log.IsWarnEnabled)
			{
				log.Warn(Message);
			}
		}
		else if (Level == Level.Debug)
		{
			if (log.IsDebugEnabled)
			{
				log.Debug(Message);
			}
		}
		else
		{
			log.Info(Message);
		}
	}
}
