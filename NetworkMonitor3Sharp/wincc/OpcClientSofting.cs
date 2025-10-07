using System.Collections.Generic;
using OpcSoftingDaClient;

namespace NetworkMonitor.wincc;

public class OpcClientSofting : aWinccConnection
{
	public ClientConnection OpcConnection;

	public OpcClientSofting(string OpcServerName)
	{
		if (TagConnections.GetAllWriteTagList().Count > 0)
		{
			OpcConnection = new ClientConnection(OpcServerName);
		}
	}

	public override bool Start()
	{
		if (OpcConnection != null)
		{
			return OpcConnection.Initialise(TagConnections.GetAllWriteTagList());
		}
		return false;
	}

	public override void Stop()
	{
		if (OpcConnection != null)
		{
			OpcConnection.Stop();
		}
	}

	public override bool Write(List<KeyValuePair<string, object>> updates)
	{
		if (OpcConnection != null)
		{
			return OpcConnection.Write(updates);
		}
		return false;
	}

	public override bool RunTimeActive()
	{
		return true;
	}
}
