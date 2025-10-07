using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace NetworkMonitor;

public static class NotepadPlus
{
	private static bool GetNotePadPlusPlus(out string PathAndFileName)
	{
		string NotePad = "C:\\Program Files (x86)\\Notepad++\\notepad++.exe";
		if (File.Exists(NotePad))
		{
			PathAndFileName = NotePad;
			return true;
		}
		NotePad = "C:\\Program Files\\Notepad++\\Notepad++.exe";
		if (!File.Exists(NotePad))
		{
			PathAndFileName = NotePad;
			return true;
		}
		PathAndFileName = "notepad.exe";
		return false;
	}

	private static string GetNotePadPlus()
	{
		if (GetNotePadPlusPlus(out var npp))
		{
			return npp;
		}
		return "notepad.exe";
	}

	public static void Open(string FilePathAndName)
	{
		Process process = new Process();
		process.StartInfo.FileName = GetNotePadPlus();
		process.StartInfo.Arguments = FilePathAndName;
		process.StartInfo.CreateNoWindow = true;
		process.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
		process.Start();
	}

	public static void Open(List<string> FileNames, string Directory)
	{
		Process p = new Process();
		p.StartInfo.WorkingDirectory = Directory;
		if (GetNotePadPlusPlus(out var NotePad))
		{
			p.StartInfo.FileName = NotePad;
			StringBuilder Arg = new StringBuilder();
			foreach (string FileName in FileNames)
			{
				if (File.Exists(Directory + "\\" + FileName))
				{
					if (Arg.Length > 0)
					{
						Arg.Append(", ");
					}
					Arg.Append("\"");
					Arg.Append(Directory + "\\" + FileName);
					Arg.Append("\"");
				}
			}
			p.StartInfo.Arguments = "-nosession " + Arg.ToString();
		}
		else
		{
			NotePad = "notepad.exe";
			p.StartInfo.Arguments = FileNames[0];
		}
		p.StartInfo.CreateNoWindow = true;
		p.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
		p.Start();
	}
}
