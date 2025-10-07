using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NetworkMonitor.Properties;

[CompilerGenerated]
[GeneratedCode("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "17.8.0.0")]
public sealed class Settings : ApplicationSettingsBase
{
	private static Settings defaultInstance = (Settings)SettingsBase.Synchronized(new Settings());

	public static Settings Default => defaultInstance;

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("")]
	public string myTagName
	{
		get
		{
			return (string)this["myTagName"];
		}
		set
		{
			this["myTagName"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("")]
	public string mySwitch
	{
		get
		{
			return (string)this["mySwitch"];
		}
		set
		{
			this["mySwitch"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("29")]
	public int PollInterval
	{
		get
		{
			return (int)this["PollInterval"];
		}
		set
		{
			this["PollInterval"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("False")]
	public bool snmpBulkWalk
	{
		get
		{
			return (bool)this["snmpBulkWalk"];
		}
		set
		{
			this["snmpBulkWalk"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("2500")]
	public decimal pingTimeout
	{
		get
		{
			return (decimal)this["pingTimeout"];
		}
		set
		{
			this["pingTimeout"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("5000")]
	public int snmpTimeOut
	{
		get
		{
			return (int)this["snmpTimeOut"];
		}
		set
		{
			this["snmpTimeOut"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("public")]
	public string SnmpCommunity
	{
		get
		{
			return (string)this["SnmpCommunity"];
		}
		set
		{
			this["SnmpCommunity"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("True")]
	public bool UpdateDisabledTag
	{
		get
		{
			return (bool)this["UpdateDisabledTag"];
		}
		set
		{
			this["UpdateDisabledTag"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("162")]
	public decimal PortNrSnmpTraps
	{
		get
		{
			return (decimal)this["PortNrSnmpTraps"];
		}
		set
		{
			this["PortNrSnmpTraps"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("opcda://ANY-PC/OPCServer.WinCC{75d00bbb-0000}")]
	public string OPCserverUrl
	{
		get
		{
			return (string)this["OPCserverUrl"];
		}
		set
		{
			this["OPCserverUrl"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("True")]
	public bool SNMPv3
	{
		get
		{
			return (bool)this["SNMPv3"];
		}
		set
		{
			this["SNMPv3"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("False")]
	public bool snmpTraps
	{
		get
		{
			return (bool)this["snmpTraps"];
		}
		set
		{
			this["snmpTraps"] = value;
		}
	}

    [UserScopedSetting]
    [DebuggerNonUserCode]
    [DefaultSettingValue("False")]
    public bool RunStandAlone
    {
        get
        {
            return (bool)this["RunStandAlone"];
        }
        set
        {
            this["RunStandAlone"] = value;
        }
    }

    private void SettingChangingEventHandler(object sender, SettingChangingEventArgs e)
	{
	}

	private void SettingsSavingEventHandler(object sender, CancelEventArgs e)
	{
	}
}
