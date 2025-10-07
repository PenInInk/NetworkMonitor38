using System.Collections.Generic;

namespace NetworkMonitor.wincc;

public abstract class aWinccConnection
{
	public delegate void ServerDownDelegate(bool serverDown);

	public static event ServerDownDelegate WinccServerDownEvent;

	public static event ServerDownDelegate PlcDownEvent;

	public static void RaiseWinccServerDown(bool down)
	{
		if (aWinccConnection.WinccServerDownEvent != null)
		{
			aWinccConnection.WinccServerDownEvent(down);
		}
	}

	public static void RaisePlcDown(bool down)
	{
		if (aWinccConnection.PlcDownEvent != null)
		{
			aWinccConnection.PlcDownEvent(down);
		}
	}

	public abstract bool Start();

	public abstract void Stop();

	public abstract bool RunTimeActive();

	public abstract bool Write(List<KeyValuePair<string, object>> TagUpdates);

	public List<KeyValuePair<string, object>> GetChangedTags()
	{
		return TagConnections.GetChangedTags();
	}

	public List<KeyValuePair<string, object>> GetChangedTags(ref HostDevice Device)
	{
		return TagConnections.GetChangedTags(ref Device);
	}

	public void UpdatePortTags(ref HostDevice Device)
	{
		TagConnections.UpdatePortTags(ref Device);
	}
}
