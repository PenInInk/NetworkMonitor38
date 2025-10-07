using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Timers;
using log4net;
using Siemens.Industry.ProcessManagement.ManagedODK;

namespace NetworkMonitor.wincc;

public class WinccDm : aWinccConnection
{
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	private static ControlCenter ControlCenter;

	private static DMTagSet ReadTagSet;

	private Timer ReadStatusTagsTimer;

	public override bool Start()
	{
		try
		{
			ControlCenter = new ControlCenter("ADBSG");
			if (ControlCenter == null)
			{
				log.Error("WinCC ControlCenter null");
			}
			else if (ControlCenter.ProjectInfo != null)
			{
				if (log.IsInfoEnabled)
				{
					log.Info(ControlCenter.ProjectInfo.ProjectFile);
				}
				if (ControlCenter.IsRuntimeProject)
				{
					ServerPairAndS7ChannelInfo.Initialize(TagConnections.GetReadTagSet());
					List<string> ServerTags = ServerPairAndS7ChannelInfo.GetRedundantServerStateTagNames();
					ReadTagSet = ControlCenter.CreateTagSet();
					if (ServerTags.Count > 0)
					{
						ReadTagSet.Add(ServerTags);
					}
					else
					{
						ReadTagSet.Add("@CurrentUser");
					}
					Check();
					try
					{
						ReadTagSet.StartVarUpdate(VarCycle.Cycle2s);
						ReadStatusTagsTimer = new Timer(1000.0);
						ReadStatusTagsTimer.Elapsed += ReadStatusTagsTimer_Elapsed;
						ReadStatusTagsTimer.Start();
						return true;
					}
					catch (ODKException ex)
					{
						CommonError er = ex.Error;
						if (log.IsInfoEnabled)
						{
							log.Info("WinCC StartVarUpdate() " + er.ErrorText);
						}
					}
				}
				else if (log.IsInfoEnabled)
				{
					log.Info("WinCC Runtime not active ");
				}
			}
		}
		catch (ODKException ex2)
		{
			if (log.IsInfoEnabled)
			{
				log.Info("WinCC not installed " + ex2.Message);
			}
		}
		return false;
	}

	private void ReadStatusTagsTimer_Elapsed(object sender, ElapsedEventArgs e)
	{
		ReadStatusServerAndPlc();
	}

	public override void Stop()
	{
		if (ReadStatusTagsTimer != null)
		{
			ReadStatusTagsTimer.Stop();
			ReadStatusTagsTimer.Elapsed -= ReadStatusTagsTimer_Elapsed;
		}
		if (ReadTagSet != null && !ReadTagSet.IsDisposed)
		{
			ReadTagSet.StopVarUpdate();
			ReadTagSet.Dispose();
			if (log.IsDebugEnabled)
			{
				log.Debug("ReadTagSet Disposed");
			}
		}
		if (ControlCenter != null)
		{
			try
			{
				ControlCenter.DisConnect();
				if (log.IsDebugEnabled)
				{
					log.Debug("ControlCenter DisConnected");
				}
			}
			catch (Exception ex)
			{
				if (log.IsDebugEnabled)
				{
					log.Debug("ControlCenter.DisConnect() " + ex.Message);
				}
			}
			ControlCenter.Dispose();
		}
		ServerPairAndS7ChannelInfo.Dispose();
	}

	public override bool Write(List<KeyValuePair<string, object>> updates)
	{
		if (updates != null && updates.Count > 0 && ControlCenter != null && ControlCenter.IsRuntimeProject)
		{
			DMTagSet WriteTagSet = ControlCenter.CreateTagSet(updates);
			if (WriteTagSet.Count > 0)
			{
				writetolog(updates);
				DMTagSet writeTagSetToDisposeInHandler = WriteTagSet;
				WriteTagSet = null;
				try
				{
					writeTagSetToDisposeInHandler.WriteSync(WriteTagSetCallback);
				}
				catch (ODKException ex)
				{
					CommonError err = ex.Error;
					if (Enum.IsDefined(typeof(DMError), err.Error1))
					{
						_ = err.Error1;
						log.Error("WinCC DM Write " + Enum.GetName(typeof(DMError), err.Error1) + ". " + err.ErrorText);
					}
					else
					{
						log.Error("WinCC DM Write... " + err.ErrorText);
					}
				}
				catch (Exception ex2)
				{
					log.Error("WinCC DM Write... " + ex2.Message);
				}
			}
		}
		return false;
	}

	public override bool RunTimeActive()
	{
		if (ControlCenter != null)
		{
			try
			{
				return ControlCenter.IsRuntimeProject;
			}
			catch (DllNotFoundException)
			{
				return false;
			}
		}
		return false;
	}

	private void writetolog(List<KeyValuePair<string, object>> tagwrites)
	{
		if (!log.IsInfoEnabled)
		{
			return;
		}
		foreach (KeyValuePair<string, object> update in tagwrites)
		{
			long l;
			if (update.Value is uint)
			{
				l = (uint)update.Value;
			}
			else if (update.Value is int)
			{
				l = (int)update.Value;
			}
			else if (update.Value is ushort)
			{
				l = (ushort)update.Value;
			}
			else
			{
				if (!(update.Value is short))
				{
					log.Info($"Write\t{update.Key}\t0x{update.Value}");
					break;
				}
				l = (short)update.Value;
			}
			log.Info($"Write\t{update.Key}\t0x{l:X4}");
		}
	}

	private void Check()
	{
		IEnumerable<DMConnectionDataStruct> connectionData = null;
		connectionData = ControlCenter.ConnectionData;
		if (connectionData == null)
		{
			return;
		}
		foreach (DMConnectionDataStruct item in connectionData)
		{
			_ = item;
		}
	}

	private void WriteTagSetCallback(object sender, WriteSyncEventArgs eventArgs)
	{
		DMTagSet ts = (DMTagSet)sender;
		foreach (WriteSyncResult wsr in eventArgs.ResultSet)
		{
			DMVarKeyStruct vkStructure = wsr.VarKey;
			DMVarState State = wsr.State;
			if (State != 0)
			{
				log.Warn("WriteTag Callback " + State.ToString() + " : " + vkStructure.Name + " : " + wsr.Value);
			}
		}
		ts.Dispose();
	}

	private void ReadStatusServerAndPlc()
	{
		ServerPairAndS7ChannelInfo.ResetLastReadStatus();
		if (ReadTagSet.Read())
		{
			foreach (string tagname in ReadTagSet.TagNames)
			{
				DMVarUpdateStructEx v = ReadTagSet[tagname];
				ServerPairAndS7ChannelInfo.UpdateLastReadStatus(v.VarKey.Name, v.State);
			}
		}
		ServerPairAndS7ChannelInfo.CheckAndRaiseEvents();
	}

	public static void Write(string[] tagnames, uint[] values)
	{
		if (ControlCenter != null)
		{
			DMTagSet WriteTagSet = ControlCenter.CreateTagSet();
			for (int i = 0; i < tagnames.Count(); i++)
			{
				WriteTagSet.Add(tagnames[i], values[i]);
			}
			WriteTagSet.Write();
			CommonError err = WriteTagSet.Error;
			if (err.Error1 != 0 && log.IsInfoEnabled)
			{
				log.Info(err.ErrorText);
			}
			WriteTagSet.Dispose();
		}
	}

	public static uint Read(string tagname)
	{
		DMVarUpdateStructEx ex = ReadTagSet[tagname];
		decimal dValue = default(decimal);
		switch (ex.State)
		{
		case DMVarState.DM_VARSTATE_OK:
			dValue = Convert.ToDecimal(ex.Value);
			break;
		case DMVarState.DM_VARSTATE_STARTUP_VALUE:
		case DMVarState.DM_VARSTATE_SERVERDOWN:
			dValue = Convert.ToDecimal(ex.Value);
			break;
		case DMVarState.DM_VARSTATE_INVALID_KEY:
			throw new NotImplementedException(tagname);
		}
		return (uint)dValue;
	}
}
