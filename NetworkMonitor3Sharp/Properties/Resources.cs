using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace NetworkMonitor.Properties;

[GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
[DebuggerNonUserCode]
[CompilerGenerated]
public class Resources
{
	private static ResourceManager resourceMan;

	private static CultureInfo resourceCulture;

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	public static ResourceManager ResourceManager
	{
		get
		{
			if (resourceMan == null)
			{
				resourceMan = new ResourceManager("NetworkMonitor.Properties.Resources", typeof(Resources).Assembly);
			}
			return resourceMan;
		}
	}

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	public static CultureInfo Culture
	{
		get
		{
			return resourceCulture;
		}
		set
		{
			resourceCulture = value;
		}
	}

	public static Icon ADB_SAFEGATE_logo_64x64 => (Icon)ResourceManager.GetObject("ADB_SAFEGATE_logo_64x64", resourceCulture);

	public static Bitmap cable_blue => (Bitmap)ResourceManager.GetObject("cable_blue", resourceCulture);

	public static Bitmap cable_green => (Bitmap)ResourceManager.GetObject("cable_green", resourceCulture);

	public static Bitmap cable_red => (Bitmap)ResourceManager.GetObject("cable_red", resourceCulture);

	public static Bitmap cable_white => (Bitmap)ResourceManager.GetObject("cable_white", resourceCulture);

	public static Icon cable_white_96 => (Icon)ResourceManager.GetObject("cable_white_96", resourceCulture);

	public static Bitmap cable_yellow => (Bitmap)ResourceManager.GetObject("cable_yellow", resourceCulture);

	public static Bitmap close_24 => (Bitmap)ResourceManager.GetObject("close_24", resourceCulture);

	public static Bitmap close_48 => (Bitmap)ResourceManager.GetObject("close_48", resourceCulture);

	public static Bitmap close_64 => (Bitmap)ResourceManager.GetObject("close_64", resourceCulture);

	public static Bitmap hide32x32 => (Bitmap)ResourceManager.GetObject("hide32x32", resourceCulture);

	public static Bitmap hide64x64 => (Bitmap)ResourceManager.GetObject("hide64x64", resourceCulture);

	public static Icon icon_cable_blue => (Icon)ResourceManager.GetObject("icon_cable_blue", resourceCulture);

	public static Icon icon_cable_green => (Icon)ResourceManager.GetObject("icon_cable_green", resourceCulture);

	public static Icon icon_cable_red => (Icon)ResourceManager.GetObject("icon_cable_red", resourceCulture);

	public static Icon icon_cable_white => (Icon)ResourceManager.GetObject("icon_cable_white", resourceCulture);

	public static Icon icon_cable_yellow => (Icon)ResourceManager.GetObject("icon_cable_yellow", resourceCulture);

	public static Icon icon_hide => (Icon)ResourceManager.GetObject("icon_hide", resourceCulture);

	public static Icon icon_show => (Icon)ResourceManager.GetObject("icon_show", resourceCulture);

	public static Bitmap show32x32 => (Bitmap)ResourceManager.GetObject("show32x32", resourceCulture);

	public static Bitmap show64x64 => (Bitmap)ResourceManager.GetObject("show64x64", resourceCulture);

	internal Resources()
	{
	}
}
