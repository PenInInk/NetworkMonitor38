using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using log4net;
using NetworkAdapters;
using NetworkMonitor.Properties;
using NetworkMonitor.snmp;
using NetworkMonitor.wincc;

namespace NetworkMonitor;

public class GUI : Form
{
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	public static SynchronizationContext FormSynchronizationContext = null;

	public static bool IsGuiVisible = false;

	private NotifyIcon iconSystemTray;

	private ContextMenuStrip iconSystemTrayContextMenu;

	private ToolStripMenuItem iconSystemTrayShowTraps;

	private ToolStripMenuItem iconSystemTrayHide;

	private ToolStripMenuItem iconSystemTrayTerminate;

	private ToolStripMenuItem iconSystemTraySpace = new ToolStripMenuItem(" Network Monitor ");

	private NetworkAdapter mNetworkAdapter;

	public static aWinccConnection TagConnection = null;

	private System.Timers.Timer tmrWriteTags = new System.Timers.Timer(1000.0);

	public int ScanningDeviceCounter;

	private Thread workThread;

	private static GUI instance = null;

	private IContainer components;

	private ListBox listBox1;

	protected override void SetVisibleCore(bool value)
	{
		base.SetVisibleCore(IsGuiVisible ? value : IsGuiVisible);
	}

	public GUI()
	{
		instance = this;
		FormSynchronizationContext = SynchronizationContext.Current;
		IsGuiVisible = false;
		InitializeComponent();
		initialiseSystemTrayicon();
		iconSystemTray.Visible = true;
		if (!configuration.Initialise())
		{
			log.Error("initialisation failed.");
			Environment.Exit(1);
		}
		if (configuration.OPCserverUrl.Length > 0)
		{
			TagConnection = new OpcClientSofting(configuration.OPCserverUrl);
		}
		else
		{
			TagConnection = new WinccDm();
		}
		if (!Start() && !Program.RunStandAlone)
		{
			if (log.IsInfoEnabled)
			{
				log.Info("start failed. RequestShutdown");
			}
			Program.RequestShutdown = true;
		}
		if (configuration.deviceList.Length == 0)
		{
			Program.RequestShutdown = true;
		}
		if (Program.RequestShutdown)
		{
			Environment.Exit(1);
		}
		else
		{
			workThread = new Thread(ScanThread);
			workThread.Start();
		}
		//SnmpTrapD.Start();
		tmrWriteTags.Elapsed += TmrWriteTags_Elapsed;
		tmrWriteTags.Enabled = true;
	}

	private void initialiseSystemTrayicon()
	{
		iconSystemTrayShowTraps = new ToolStripMenuItem();
		iconSystemTrayShowTraps.Image = Resources.show32x32;
		iconSystemTrayShowTraps.Name = "iconSystemTrayShowTraps";
		iconSystemTrayShowTraps.Size = new Size(256, 256);
		iconSystemTrayShowTraps.Text = "Show";
		iconSystemTrayShowTraps.Click += IconSystemTrayShowTraps_Click;
		iconSystemTrayHide = new ToolStripMenuItem();
		iconSystemTrayHide.Name = "iconSystemTrayHide";
		iconSystemTrayHide.Image = Resources.hide32x32;
		iconSystemTrayHide.Size = new Size(256, 256);
		iconSystemTrayHide.Text = "Hide";
		iconSystemTrayHide.Click += IconSystemHide_Click;
		iconSystemTrayTerminate = new ToolStripMenuItem("Terminate");
		iconSystemTrayTerminate.Name = "";
		iconSystemTrayTerminate.Image = Resources.close_24;
		iconSystemTrayTerminate.Size = new Size(24, 24);
		iconSystemTrayTerminate.Click += IconSystemTrayTerminate_Click;
		List<ToolStripMenuItem> MenuItems = new List<ToolStripMenuItem>();
		MenuItems.Add(iconSystemTrayShowTraps);
		MenuItems.Add(iconSystemTrayHide);
		MenuItems.Add(iconSystemTraySpace);
		MenuItems.Add(iconSystemTrayTerminate);
		MenuItems.Add(iconSystemTraySpace);
		iconSystemTrayContextMenu = new ContextMenuStrip();
		ToolStripItemCollection items = iconSystemTrayContextMenu.Items;
		ToolStripItem[] toolStripItems = MenuItems.ToArray();
		items.AddRange(toolStripItems);
		iconSystemTrayContextMenu.Name = "TrayIconContextMenu";
		iconSystemTrayContextMenu.Size = new Size(142, 70);
		components = new Container();
		iconSystemTray = new NotifyIcon(components);
		iconSystemTray.ContextMenuStrip = iconSystemTrayContextMenu;
		iconSystemTray.Icon = Resources.ADB_SAFEGATE_logo_64x64;
		iconSystemTray.Text = "Network Monitor";
		iconSystemTray.Visible = true;
	}

	private void IconSystemTrayTerminate_Click(object sender, EventArgs e)
	{
		if (log.IsDebugEnabled)
		{
			log.Debug("User clicked Close icon on system tray.");
		}
		Terminate();
	}

	private void IconSystemHide_Click(object sender, EventArgs e)
	{
		IsGuiVisible = false;
		Hide();
	}

	private void IconSystemTrayShowTraps_Click(object sender, EventArgs e)
	{
		IsGuiVisible = true;
		base.TopMost = true;
		Show();
	}

	public bool Start()
	{
		if (!Program.RequestShutdown)
		{
			if (Program.RunStandAlone)
			{
				log.Warn("not making tag connection. Running StandAlone ");
			}
			else if (!TagConnection.Start())
			{
				if (log.IsInfoEnabled)
				{
					log.Info("WinCC runtime not active");
				}
				return false;
			}
			if (configuration.deviceList.Length == 0)
			{
				log.Error("No host found.");
				return false;
			}
			if (TagConnection != null && !Program.RequestShutdown)
			{
				WindowsAPI.SetWinccToForeground();
			}
			if (configuration.deviceList.Length == 0 && log.IsInfoEnabled)
			{
				log.Info("no hosts found. Nothing to do...");
			}
		}
		InitialialiseListBox();
		return true;
	}

	private void Terminate()
	{
		if (log.IsInfoEnabled)
		{
			log.Info("Terminate()");
		}
		Program.RequestShutdown = true;
		tmrWriteTags.Enabled = false;
		tmrWriteTags.Elapsed -= TmrWriteTags_Elapsed;
		if (log.IsDebugEnabled)
		{
			log.Debug("SharpTraps.Stop()");
		}
		if (mNetworkAdapter != null)
		{
			if (log.IsDebugEnabled)
			{
				log.Debug("mNetworkAdapter.Close()");
			}
			mNetworkAdapter.Close();
			mNetworkAdapter = null;
		}
		try
		{
			if (log.IsDebugEnabled)
			{
				log.Debug("winccc.Stop()");
			}
			TagConnection.Stop();
		}
		catch (Exception ex)
		{
			log.Error("Disconnect Tags: " + ex.Message);
		}
		if (log.IsDebugEnabled)
		{
			log.Debug("configuration.Dispose()");
		}
		configuration.Dispose();
		if (log.IsDebugEnabled)
		{
			log.Debug("Exit");
		}
		Environment.Exit(1);
	}

	private void GUI_FormClosing(object sender, FormClosingEventArgs e)
	{
		if (log.IsDebugEnabled)
		{
			log.Debug("FormClosing(" + Enum.GetName(typeof(CloseReason), e.CloseReason) + ")");
		}
		switch (e.CloseReason)
		{
		case CloseReason.UserClosing:
			if (log.IsInfoEnabled)
			{
				log.Info("Closing application: User closed application");
			}
			break;
		}
		Terminate();
	}

	private void TmrWriteTags_Elapsed(object sender, ElapsedEventArgs e)
	{
		if (!Program.RequestShutdown)
		{
			TagConnection.Write(TagConnection.GetChangedTags());
		}
	}

	private void ScanThread()
	{
		if (configuration.deviceList != null && configuration.deviceList.Length != 0)
		{
			while (!Program.RequestShutdown)
			{
				ScanNextDevice();
				Thread.Sleep(100);
			}
			if (FormSynchronizationContext == null)
			{
				Terminate();
			}
			else
			{
				Terminate();
			}
			log.Info("< ScanThread");
		}
	}

	private void ScanNextDevice()
	{
		if (Program.RequestShutdown || configuration.deviceList == null || configuration.deviceList.Length == 0)
		{
			return;
		}
		HostDevice device = null;
		if (++ScanningDeviceCounter >= configuration.deviceList.Length)
		{
			ScanningDeviceCounter = 0;
		}
		device = configuration.deviceList[ScanningDeviceCounter];
		if (device.NextPollTime > DateTime.Now)
		{
			return;
		}
		device.NextPollTime = DateTime.Now.AddSeconds(configuration.PollInterval);
		if (Program.RequestShutdown)
		{
			return;
		}
		ICMP.Ping(ref device);
		if (device.ping.PingFailed == 1)
		{
			if (log.IsDebugEnabled)
			{
				log.Debug("ScanNext: Waiting for ping reply from " + device);
			}
			return;
		}
		if (device.ping.PingFailed <= 1 && device.ping.PingFailed == 0L && device.ping.PingAlive)
		{
			if (Program.RequestShutdown)
			{
				return;
			}
			device.Poll();
		}
		if (Program.RequestShutdown)
		{
			return;
		}
		if (device.mySwitch != null && !device.IP.Equals(device.mySwitch.IP))
		{
			ICMP.Ping(ref device.mySwitch);
			if (Program.RequestShutdown)
			{
				return;
			}
		}
		if (device.tagvalues.TagDisabledHasChangedDelayCounter > 0)
		{
			device.tagvalues.TagDisabledHasChangedDelayCounter--;
		}
	}

	private void InitialialiseListBox()
	{
		listBox1.MultiColumn = false;
	}

	public static void AddMsgToListView(string source, string Oid, string data)
	{
		string msg = $"{DateTime.Now.ToLocalTime(),-10} {source,-17} > {Oid,-30} = {data,10}.";
		FormSynchronizationContext.Send(delegate
		{
			AddMessageToListView(msg);
		}, null);
	}

	public static void AddMsgToListView(string message)
	{
		FormSynchronizationContext.Send(delegate
		{
			AddMessageToListView(message);
		}, null);
	}

	private static void AddMessageToListView(string message)
	{
		if (IsGuiVisible || instance.listBox1.Items.Count < 20)
		{
			instance.listBox1.SuspendLayout();
			if (instance.listBox1.Items.Count > 20)
			{
				instance.listBox1.Items.RemoveAt(0);
			}
			instance.listBox1.Items.Add(message);
			instance.listBox1.ResumeLayout();
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		this.listBox1 = new System.Windows.Forms.ListBox();
		base.SuspendLayout();
		this.listBox1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.listBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.listBox1.FormattingEnabled = true;
		this.listBox1.ItemHeight = 15;
		this.listBox1.Location = new System.Drawing.Point(0, 0);
		this.listBox1.Name = "listBox1";
		this.listBox1.Size = new System.Drawing.Size(783, 537);
		this.listBox1.TabIndex = 0;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(783, 537);
		base.ControlBox = false;
		base.Controls.Add(this.listBox1);
		base.Enabled = false;
		this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		base.MaximizeBox = false;
		base.Name = "GUI";
		base.Opacity = 0.8;
		base.ShowIcon = false;
		base.ShowInTaskbar = false;
		this.Text = "Network Monitor";
		base.TopMost = true;
		base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(GUI_FormClosing);
		base.ResumeLayout(false);
	}
}
