using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using log4net;

namespace NetworkMonitor;

internal static class WindowsAPI
{
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	[DllImport("user32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
	public static extern bool SetForegroundWindow(IntPtr hwnd);

	public static bool SetWinccToForeground()
	{
		Process[] pdlrt = Process.GetProcessesByName("PdlRt");
		if (pdlrt.Length != 0)
		{
			return SetForegroundWindow(pdlrt[0].MainWindowHandle);
		}
		return false;
	}

	public static void TerminatePrevous()
	{
		Process CurrentProcess = Process.GetCurrentProcess();
		Process[] MNs = Process.GetProcessesByName(CurrentProcess.ProcessName);
		if (MNs == null || MNs.Length <= 1)
		{
			return;
		}
		for (int p = 0; p < MNs.Length - 1; p++)
		{
			if (MNs[p].StartTime < CurrentProcess.StartTime)
			{
				if (MNs[p].CloseMainWindow())
				{
					log.Warn("Terminated previous instance");
				}
				else
				{
					log.Error("Terminating previous instance");
				}
				MNs[p].WaitForExit();
				log.Error("Wait For Exit previous instance done");
			}
		}
	}
}
