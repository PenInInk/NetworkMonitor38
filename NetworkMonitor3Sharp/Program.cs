using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using log4net;
using log4net.Config;

namespace NetworkMonitor;

internal static class Program
{
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	public static bool RunStandAlone = false;

	public static string parameterIpAddress = "";

	public static GUI mainApp = null;

	public static bool RequestShutdown = false;

	public static EventWaitHandle FormClosedResetEvent { get; set; }

	[STAThread]
	private static void Main(string[] args)
	{
		XmlConfigurator.Configure();
		GlobalContext.Properties["tab"] = '\t';
		string BuildVersionOfMscorlib = FileVersionInfo.GetVersionInfo(typeof(int).Assembly.Location).ProductVersion;
		FileVersionInfo thisFileVersionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
		if (log.IsInfoEnabled)
		{
			log.Info("---- start ---- " + thisFileVersionInfo.FileVersion + " -- target framework " + Environment.Version?.ToString() + " -- Running on " + BuildVersionOfMscorlib);
		}
		foreach (string argument in args)
		{
			if (argument.ToUpper().Equals("SHOW"))
			{
				RunStandAlone = true;
			}
			else if (argument.ToUpper().Equals("DEBUG"))
			{
				RunStandAlone = true;
			}
			else if (argument.Length > 0 && argument.Contains('.'))
			{
				parameterIpAddress = argument;
			}
		}
		WindowsAPI.TerminatePrevous();
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		Application.Run(new GUI());
		log.Info("---- end ---- ");
	}
}
