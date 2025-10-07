//-----------------------------------------------------------------------------
//                                                                            |
//                   Softing Industrial Automation GmbH                       |
//                        Richard-Reitzner-Allee 6                            |
//                           85540 Haar, Germany                              |
//                                                                            |
//                 This is a part of the Softing OPC Toolkit                  |
//       Copyright (c) 1998 - 2015 Softing Industrial Automation GmbH         |
//                           All Rights Reserved                              |
//                                                                            |
//-----------------------------------------------------------------------------
//-----------------------------------------------------------------------------
//                             OPC Toolkit C#                                 |
//                                                                            |
//  Filename    : MyDaSession.cs		                                      |
//  Version     : 4.41                                                        |
//  Date        : 30-January-2015                                             |
//                                                                            |
//  Description : OPC DA Session template class definition                    |
//                                                                            |
//-----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Softing.OPCToolbox.Client;
using Softing.OPCToolbox;
using System.Threading;
using log4net;

namespace OpcSoftingDaClient
{
	public class MyDaSession : DaSession
	{
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public MyDaSession (string url) : base (url) 
		{
			StateChangeCompleted += new StateChangeEventHandler(HandleStateChanged);
			PerformStateTransitionCompleted += new PerformStateTransitionEventHandler(HandlePerformStateTransition);
			ShutdownRequest += new ShutdownEventHandler(HandleServerShutdown);
			ReadCompleted += new SessionReadEventHandler(HandleSessionReadCompleted);
			WriteCompleted += new SessionWriteEventHandler(HandleSessionWriteCompleted);
			GetStatusCompleted += new GetStatusEventHandler(HandleGetServerStatus);
			LogonCompleted += new LogonEventHandler(HandleLogonCompleted);
			LogoffCompleted += new LogoffEventHandler(HandleLogoffCompleted);
		}

		#region Public Properties
		//-----------------------


		//--
		#endregion

		#region Handles
		//---------------------

		public static void HandleStateChanged(ObjectSpaceElement sender, EnumObjectState state)
		{
			if (log.IsDebugEnabled) log.Debug( sender + " State Changed - " + Enum.GetName(typeof(EnumObjectState),state));
		}

		public static void HandlePerformStateTransition(
			ObjectSpaceElement sender, 
			uint executionContext, 
			int result)
		{			
			if(ResultCode.SUCCEEDED(result))
			{
				if (log.IsDebugEnabled) log.Debug(sender + " Performed state transition - "  + executionContext );
			}
			else
			{
				log.Error(sender + "  Performed state transition failed! Result: " + Enum.GetName(typeof(EnumResultCode), result));
			}
		}

		public static bool HandleServerShutdown(ObjectSpaceElement sender, string reason)
		{			
			log.Info(sender + " DaSession.HandleServerShutdown - " + reason);
			return true; 
		} 

		public static void HandleSessionReadCompleted(
			DaSession daSession,
			uint executionContext,
			string[] itemIds,
			string[] itemPaths,
			ValueQT[] values,
			int[] results,
			int result)
		{
			if(ResultCode.SUCCEEDED(result))
			{
				if (log.IsDebugEnabled) log.Debug(daSession + " asynchronous read succeeded! ");
                for (int i = 0; i < itemIds.Length; i++)
                {
                    if (ResultCode.SUCCEEDED(results[i]))
                    {
                        if (log.IsDebugEnabled) log.Debug(String.Format("{0,-19} {1} {2,-50} ", itemIds[i], "-", values[i].ToString()));
                    }
                    else
                    {
                        log.Error(" Session asynchronous read failed for item " + " Item: " + itemIds[i] + " [ResultID: " + Enum.GetName(typeof(EnumResultCode), results[i]) + " ]");
                    }
                }
			}
			else
			{
				log.Error(" Session asynchronous read failed! Result: " + Enum.GetName(typeof(EnumResultCode), result));
			}
		}
		
		public static void HandleSessionWriteCompleted(
			DaSession daSession, 
			uint executionContext,
			string[] itemIds,
			string[] itemPaths,
			ValueQT[] values,
			int[] results,
			int result)
		{
			if(ResultCode.SUCCEEDED(result))
            {
				return;
            }

			for(int i = 0 ; i< itemIds.Length; i++)
			{
				if (!ResultCode.SUCCEEDED(results[i]))
				{
					log.Error("write " + itemIds[i] + " [ResultID: " + Enum.GetName(typeof(EnumResultCode), results[i]) + " ]");
				}				
			}
		}
        
		public static void HandleGetServerStatus(
			ObjectSpaceElement sender,
			uint executionContext, 
			ServerStatus serverStatus,
			int result)
		{			
			if(ResultCode.SUCCEEDED(result))
			{
                if (log.IsDebugEnabled)
                {
                    log.Debug(sender);
                    log.Debug("Server Status");
                    log.Debug("	Vendor info: " + serverStatus.VendorInfo);
                    log.Debug("	Product version: " + serverStatus.ProductVersion);
                    log.Debug("	State: " + serverStatus.State);
                    log.Debug("	Start time: " + serverStatus.StartTime);
                    log.Debug("	Last update time: " + serverStatus.LastUpdateTime);
                    log.Debug("	Current time: " + serverStatus.CurrentTime);
                    log.Debug("	GroupCount: " + serverStatus.GroupCount);
                    log.Debug("	Bandwidth: " + serverStatus.Bandwidth);
                    for (int i = 0; i < serverStatus.SupportedLcIds.Length; i++)
                    {
                        log.Debug("	Supported LCID: " + serverStatus.SupportedLcIds[i]);
                    }
                    log.Info("	Status info: " + serverStatus.StatusInfo);
                }
			}
			else
			{
				log.Error("Asynchronous get server status failed! Result: " + Enum.GetName(typeof(EnumResultCode), result));
			}
		} 

		public static void HandleLogonCompleted(
			ObjectSpaceElement sender,
			uint executionContext,
			String UserName,
			String Password,
			int result)
		{
			if (log.IsDebugEnabled) log.Debug("\n Logon for secure communication");
			if (ResultCode.SUCCEEDED(result))
			{
				if (log.IsDebugEnabled) log.Debug("- successfully logon for user:" + UserName);
			}
			else
			{
				log.Error("- failed logon for user:" + UserName+"  " + Enum.GetName(typeof(EnumResultCode), result));
			}
		}

		public static void HandleLogoffCompleted(
			ObjectSpaceElement sender,
			uint executionContext,
			int result)
		{
			if (log.IsDebugEnabled) log.Debug("\n Logon for secure communication");
			if (ResultCode.SUCCEEDED(result))
			{
 				if (log.IsDebugEnabled) log.Debug("- successfully logoff");
			}
			else
			{
			    log.Error("- failed logoff"+"  " + Enum.GetName(typeof(EnumResultCode), result));
			}
		}

		#endregion	
		
	}

}
