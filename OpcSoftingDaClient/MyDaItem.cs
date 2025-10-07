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
//  Filename    : MyDaItem.cs		                                          |
//  Version     : 4.41                                                        |
//  Date        : 30-January-2015                                             |
//                                                                            |
//  Description : OPC DA Item template class definition                       |
//                                                                            |
//-----------------------------------------------------------------------------
using System;
using System.Collections;
using Softing.OPCToolbox.Client;
using Softing.OPCToolbox;
using System.Threading;
using log4net;

namespace OpcSoftingDaClient
{
	public class MyDaItem : DaItem 
	{
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public MyDaItem (string itemId, MyDaSubscription parentSubscription)  : base (itemId, parentSubscription) 
		{
			ValueChanged += new ValueChangedEventHandler(HandleValueChanged);
			//StateChangeCompleted += new StateChangeEventHandler(HandleStateChanged);	
			PerformStateTransitionCompleted += new PerformStateTransitionEventHandler(HandlePerformStateTransition);
		}

        #region Public Methods
        public override string ToString()
        {
            return base.Id + ", " + base.ValueQT + ", valid: " + base.Valid;
        }
        #endregion

        #region Public Properties
        //-----------------------


        //--
        #endregion

        #region Handles

        public static void HandleStateChanged(ObjectSpaceElement sender, EnumObjectState state)
		{
            MyDaItem item = (MyDaItem)sender; 		
			if (log.IsDebugEnabled) log.Debug ("MyDaItem HandleStateChanged " + item + " State Changed - " + state);
		}

		public static void HandleValueChanged(DaItem aDaItem, ValueQT aValue)
		{
			if (log.IsDebugEnabled) log.Debug( String.Format("MyDaItem HandleValueChanged {0,-19} {1} {2,-50} ", aDaItem.Id,"-", aValue.ToString()));	
		}

		public static void HandlePerformStateTransition(
			ObjectSpaceElement sender, 
			uint executionContext, 
			int result)
		{			
			if(ResultCode.SUCCEEDED(result))
			{
				MyDaItem item = sender as MyDaItem;                    
				log.Info( sender + " " + item.Id + " Performed state transition - "  + executionContext );
			}
			else
			{
				log.Error(sender + "  Performed state transition failed! Result: " + result);
			}
		}
		#endregion
	}
}