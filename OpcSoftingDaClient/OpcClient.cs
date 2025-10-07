using System;
using System.Collections;
using Softing.OPCToolbox.Client;
using Softing.OPCToolbox;
using System.Threading;
using System.Collections.Generic;
using log4net;

namespace OpcSoftingDaClient
{
	public class OpcClient
	{
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static Dictionary<string, ValueQT> ValuesReceivedFromServer = new Dictionary<string, ValueQT>();

        public OpcClient()
        {
            instance = this;
            this.tmrConnected.Enabled = false;
            this.tmrConnected.Elapsed += TmrConnected_Elapsed;
        }

        #region Private Members
        string m_usr;
		string m_pwd;
        string url = "";
        public bool sync = false;
        private static OpcClient instance = null;
        DateTime dtRefreshAllTags = DateTime.Now;

        private MyDaSession m_daSession = null;
		private MyDaSubscription m_daSubscription = null;
        /// <summary>
        /// indexed on the wincc tagname. The itemName has the prefix "S7:[@LOCALSERVER]"
        /// m_dictValues: keeps the last received value
        /// </summary>
        private Dictionary<string, MyDaItem> m_dictItems = new Dictionary<string, MyDaItem>();
        private Dictionary<string, ValueQT> m_dictValues = new Dictionary<string, ValueQT>();
        // temporary arrays for writes
		private MyDaItem[] m_itemList;
		private string [] m_itemIds;
		private int[] m_results; 
		private ValueQT[] m_values;
		private ExecutionOptions m_executionOptions;
        #endregion
        
        #region ServerValues
        //private Dictionary<string, ValueQT> m_dictServerValues = new Dictionary<string, ValueQT>();

        #endregion

        #region Public Methods

		public int Initialize(string OpcDaUrl)
		{
            if (log.IsDebugEnabled) log.Debug("OpcClient Initialize");
            Softing.OPCToolbox.Client.Application app = Application.Instance;

            this.url = OpcDaUrl;
			int result = (int)EnumResultCode.S_OK;
			app.VersionOtb = 441;
            if (log.IsDebugEnabled)
                app.EnableTracing(EnumTraceGroup.OPCCLIENT, EnumTraceGroup.CLIENT, EnumTraceGroup.CLIENT, EnumTraceGroup.NOTHING, "TraceOpcClient", 10000, 10);
            else
                app.EnableTracing(EnumTraceGroup.NOTHING, EnumTraceGroup.NOTHING, EnumTraceGroup.NOTHING, EnumTraceGroup.NOTHING, "", 0, 0);
			//	proceed with the OPC Toolkit core initialization
			result = app.Initialize();
            //	activate the COM-DA Client feature
            switch (Application.Instance.VersionOtb)
            {
                case 422:
                    result = Application.Instance.Activate(EnumFeature.DA_CLIENT, "09c0-0000-0295-ab97-9b5f");
                    //log.Error("Old version of TBN35.dll & OTBu.dll");
                    break;
                case 441:
                    result = Application.Instance.Activate(EnumFeature.DA_CLIENT, "0ed0-0393-8740-9aa7-898b");
                    break;
            }
            if (!ResultCode.SUCCEEDED(result))
            {
                log.Error("Activation Softing DA Client failed. Verify OTBu.dll and TBN40.dll " + Enum.GetName(typeof(EnumResultCode), result));
                return result;
            }
            else
            {
                log.Info("Activated Softing DA Client. ResultCode Success");
            }
            if (!ResultCode.SUCCEEDED(result))
			{
				return result;
			}
			return result;
		}

		public int ProcessCommandLine(string commandLine)
		{
			//	forward the command line arguments to the OPC Toolkit core internals
			return Application.Instance.ProcessCommandLine(commandLine);
		}

        public void Terminate()
        {
            if (m_daSubscription != null)
            { 
                if (m_daSubscription.CurrentState != EnumObjectState.DISCONNECTED)
                {
                    m_daSubscription.Disconnect(new ExecutionOptions());
                }
                if (m_daSession.CurrentState != EnumObjectState.DISCONNECTED)
                {
                    m_daSession.Disconnect(new ExecutionOptions());
                }
                lock (this.m_dictItems)
                {
                    foreach (MyDaItem daitem in this.m_dictItems.Values)
                    {
                        m_daSubscription.RemoveDaItem(daitem);
                    }
                }
            }
            if (m_daSession != null)
            {
                if (m_daSession.CurrentState != EnumObjectState.DISCONNECTED)
                {
                    m_daSession.Disconnect(new ExecutionOptions());
                }
                Application.Instance.RemoveDaSession(m_daSession);
                m_daSession.RemoveDaSubscription(m_daSubscription);
            }
            Application.Instance.RemoveDaSession(m_daSession);
			Application.Instance.Terminate();
			m_daSession = null;
			m_daSubscription = null;
            this.m_dictItems.Clear();
		}

		public int InitializeDaObjects(List<string> TagNames)
		{
            if (log.IsDebugEnabled) log.Debug(">OpcClient InitializeDaObjects");
            int connectResult = (int)EnumResultCode.E_FAIL;
            m_executionOptions = new ExecutionOptions();
            if (sync)
            {
                m_executionOptions.ExecutionType = EnumExecutionType.SYNCHRONOUS; // not realy necessary. sycnrhonous is default value.
            }
            else
            {
                m_executionOptions.ExecutionType = EnumExecutionType.ASYNCHRONOUS;
                /*
                The ExecutionOptions.ExecutionContext property MUST BE INCREMENTED after each asynchronous operation execution.
                e.g. in case of three read operations on the same subscription, the ExecutionContext will identify the read for which the callback of read_complete came).
                This means that the value of the ExecutionContext must be carefully chosen such that it uniquely identifies an operation among the others.
               */
                m_executionOptions.ExecutionContext = 0;
            }
            try
            {
                m_daSession = new MyDaSession(url);
            }
            catch (Exception ex)
            {
                log.Error("OPC DA Session: " + ex.Message);
            }
            if ( !m_daSession.Valid)
			{
                log.Error("OPC DA Session not valid. Abort initialisation.");
				return connectResult;
			}
            if (log.IsInfoEnabled) log.Info("OPC DA session is valid.");
            try
            {
                m_daSubscription = new MyDaSubscription(1000, m_daSession);
            }
            catch (Exception ex)
            {
                log.Error("OPC DA Subscription: " + ex.Message);
            }
            if (! m_daSubscription.Valid)
			{
                log.Error("OPC DA Subscription not valid. Abort initialisation.");
				return connectResult;
			}
            if(log.IsInfoEnabled) log.Info("OPC DA Subscription valid.");
            InitialiseDAItems(TagNames);
            try
            {
                connectResult = m_daSession.Connect(true, false, m_executionOptions);
            }
			catch(Exception exc)
			{
                log.Error("OPC DA session Connect: " + exc.Message);
			}
            if (log.IsInfoEnabled) log.Info("<OpcClient InitializeDaObjects " + Enum.GetName(typeof(EnumResultCode),connectResult));
            return connectResult;
		}
        /// <summary>
        /// Creates DAItems. Needs DASubscription
        /// </summary>
        /// <param name="TagNames"></param>
        private void InitialiseDAItems(List<string> TagNames)
        {
            if (this.m_daSubscription != null)
            {
                MyDaItem mdai;
                string prefix = "";
                System.Text.StringBuilder ItemName = new System.Text.StringBuilder();
                if (this.url.Contains("OPC.SimaticNET"))
                {
                    prefix = "S7:[@LOCALSERVER]";
                }
                lock (this.m_dictItems)
                {
                    lock (this.m_dictItems)
                    {
                        for (int t = 0; t < TagNames.Count; t++)
                        {
                            ItemName.Clear();
                            ItemName.Append(prefix);
                            ItemName.Append(TagNames[t].ToUpper());
                            mdai = new MyDaItem(ItemName.ToString(), this.m_daSubscription);
                            if (mdai.Valid)
                            {
                                if (log.IsDebugEnabled) log.Debug("Added " + mdai.Id);
                                this.m_dictItems.Add(TagNames[t], mdai);
                                this.m_dictValues.Add(TagNames[t], new ValueQT());
                            }
                            else
                            {
                                log.Error("invalid OPC ItemName: " + mdai.Id);
                            }
                        }
                    }
                }
            }
        }

        public string ReadItems2()
		{
			string message = String.Empty;
			try
			{
				ValueQT[] m_values = null;
				int[] m_results = null;

				if (ResultCode.SUCCEEDED(
					m_daSubscription.Read(
					0,
					m_itemList,
					out m_values,
					out m_results,
					null)))
				{
					message += " \nRead item synchronously using subscription \n";

					for (int i = 0; i< m_values.Length;i++)
					{
						if (ResultCode.SUCCEEDED(m_results[i]))
						{
							message += " " + m_itemList[i].Id + " - ";
							message += m_values[i].ToString() + "\n\n";
						}
						else
						{
							message += "Read failed for item " + m_itemList[i].Id + "\n\n";
						}	//	end if...else
					}	//	end for
				}
				else
				{
					message += " Subscription synchronous read failed!" + "\n\n";
				}	//	end if...else
			}
			catch(Exception exc)
			{
                log.Error("ReadItems2: " + exc.Message);
			}
			return message;
		}

		public void ActivateSession(bool sync)
		{
            this.sync = sync;
			if (sync)
			{
                if (log.IsDebugEnabled) log.Debug(">OpcClient ActivateSession(Sync)");
                int result = this.m_daSession.Connect(true, true, new ExecutionOptions());
                string msg = "<OPC client ActivateSession: " + Enum.GetName(typeof(EnumResultCode), result);
                if (ResultCode.FAILED(result))
				{
                    log.Error(msg);
                }
                else
                {
                    if (log.IsDebugEnabled) log.Debug(msg);
                }
            }
			else
			{
                if (log.IsDebugEnabled) log.Debug(">OpcClient ActivateSession(Async)");
                m_daSession.Connect(true, true, m_executionOptions);	
				m_executionOptions.ExecutionContext++;		
			}
		}

		public void ConnectSession()
		{
            if (log.IsDebugEnabled) log.Debug(">OpcClient ConnectSession");
			if (sync)
			{
                int result = m_daSession.Connect(true, false, new ExecutionOptions());
                string msg = "<OPC client ConnectSession: " + Enum.GetName(typeof(EnumResultCode), result);
                if (ResultCode.FAILED(result))
				{
					log.Error(msg);
				}
                else
                {
                    if (log.IsDebugEnabled) log.Debug(msg);
                }
                //RemoveInvalidItems();
			}
			else
			{
				m_daSession.Connect(true, false, m_executionOptions);
				m_executionOptions.ExecutionContext++;		
			}
		}

        private void RemoveInvalidItems()
        {
            if (log.IsDebugEnabled) log.Debug("RemoveInvalidItems");
            if (ValuesReceivedFromServer.Count > 0)
            {
                // ValuesReceivedFromServer contain the confirmed ValueQT from the OPC Server
                // All items not in this list, do not exist in the server address space
                // These will be removed from our list, to avoid writing to not-existing Items
                lock (this.m_dictItems)
                    lock (this.m_dictItems)
                    {
                        List<string> InvalidTagNames = new List<string>();
                        foreach (KeyValuePair<string, MyDaItem> kvpDAI in this.m_dictItems)
                        {
                            string tagname = kvpDAI.Key;
                            if (!ValuesReceivedFromServer.ContainsKey(tagname))
                            {
                                InvalidTagNames.Add(tagname);
                            }
                        }
                        if (ValuesReceivedFromServer.Count < this.m_dictItems.Count)
                        {
                            lock (this.m_dictValues)
                                lock (this.m_dictItems)
                                {
                                    foreach (string tagname in InvalidTagNames)
                                    {
                                        log.Warn("disable tag updates. " + tagname);
                                        this.m_dictItems.Remove(tagname);
                                        this.m_dictValues.Remove(tagname);
                                    }
                                }
                        }
                    }
            }
        }

		public void DisconnectSession()
		{
			if (sync)
			{
				int result = m_daSession.Disconnect(new ExecutionOptions());
				if (ResultCode.FAILED(result))
				{							
					log.Error("OPC client DisconnectSession failed!");						
				}
			}
			else 
			{
				m_daSession.Disconnect(m_executionOptions);
				m_executionOptions.ExecutionContext++;		
			}
		}

		public void ReadItem(string tagname)
		{
            //string tagname = "S7:[@LOCALSERVER]block.cmd.LifeSign";
            ValueQT itemValue;
			int itemResult;
			if (sync)
			{
                MyDaItem daItem = this.m_dictItems[tagname];
				int readResult = daItem.Read(100, out itemValue, out itemResult, new ExecutionOptions());
                if (ResultCode.SUCCEEDED(readResult))
				{
                    if (log.IsDebugEnabled)
					log.Debug( String.Format("{0} {1,-19} {2} {3,-50} ", "synchronous read ", tagname, " = ", itemValue.ToString()));
				}
				else
				{
                    log.Error(String.Format("{0} {1,-19} {2} {3,-50} ", "synchronous read ", tagname, " : ", Enum.GetName(typeof(EnumResultCode),readResult)));
				}
			}				
			else 
			{
                MyDaItem daItem = this.m_dictItems[tagname];
                daItem.Read(100, out itemValue, out itemResult, m_executionOptions);
				m_executionOptions.ExecutionContext++;
			}
		}
        public void ReadItems()
        {
            if (log.IsDebugEnabled) log.Debug(">OpcClient ReadItems");
            if (sync)
            {
                int result = (int)EnumResultCode.E_FAIL;

                if (ResultCode.SUCCEEDED(result = m_daSubscription.Read(100, m_itemList, out m_values, out m_results, new ExecutionOptions())))
                {
                    if (log.IsDebugEnabled)
                        log.Debug("OpcClient ReadItems: Subscription synchronous read succeeded");
                    for (int i = 0; i < m_itemList.Length; i++)
                    {
                        if (m_results[i] >= 0)
                        {
                            if (log.IsDebugEnabled) log.Debug(String.Format("{0} {1,-19} {2} {3,-50} ", "synchronous read", m_itemList[i].Id, " - ", m_values[i].ToString()));
                        }
                        else
                        {
                            log.Error("synchronous read: Item read failed " + m_itemList[i].Id + " - " + Enum.GetName(typeof(EnumResultCode), m_results[i]));
                        }
                    }
                }
                else
                {
                    log.Error("OpcClient ReadItems: Synchronous subscription read failed! Result: " + Enum.GetName(typeof(EnumResultCode), result));
                }
            }
            else
            {
                m_daSubscription.Read(100, m_itemList, out m_values, out m_results, m_executionOptions);
                m_executionOptions.ExecutionContext++;
            }
            if (log.IsDebugEnabled) log.Debug("<OpcClient ReadItems");
        }
        public void WriteItems(List<KeyValuePair<string, UInt32>> updates)
        {
            if (log.IsDebugEnabled) log.Debug("OpcClient.WriteItems(UInt32)");
            updates.RemoveAll((u) => !this.m_dictItems.ContainsKey(u.Key));

            DateTime writeDateTime = new DateTime();
            ValueQT[] inValues = new ValueQT[updates.Count];
            MyDaItem[] inItems = new MyDaItem[updates.Count];
            int i = 0;
            foreach (KeyValuePair<string, UInt32> update in updates)
            {
                inItems[i] = this.m_dictItems[update.Key];
                inValues[i] = new ValueQT(update.Value, EnumQuality.QUALITY_NOT_SET, writeDateTime);
            }
            WriteItems(inItems, inValues);
            //WriteUsingSession(validTagNames.ToArray(), inValues);
        }
        public void WriteItems(List<KeyValuePair<string, object>> updates)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug("OpcClient.WriteItems(objects)");

                foreach (KeyValuePair<string, object> upd in updates)
                {
                    if (!this.m_dictItems.ContainsKey(upd.Key))
                    {
                        log.Error("WriteItems() tag not found " + upd.Key);
                    }
                }
            }
            // remove the update if the tagname is not know 
            // if the (update.Key) does not exist in the dictionary of validated items (this.m_dictItems)
            updates.RemoveAll((u) => !this.m_dictItems.ContainsKey(u.Key));
            DateTime writeDateTime = new DateTime();
            ValueQT[] inValues = new ValueQT[updates.Count];
            MyDaItem[] inItems = new MyDaItem[updates.Count];
            int i = 0;
            foreach (KeyValuePair<string, object> update in updates)
            {
                inItems[i] = this.m_dictItems[update.Key];
                inValues[i] = new ValueQT(update.Value, EnumQuality.QUALITY_NOT_SET, writeDateTime);
                i++;
            }
            WriteItems(inItems, inValues);
        }
        public void WriteItems(MyDaItem[] inItems, ValueQT[] inValues)
		{
            if (log.IsDebugEnabled) log.Debug("OpcClient.WriteItems(DaItems)");
            if (m_daSubscription != null)
            {
                int result = (int)EnumResultCode.E_FAIL;
                int[] inResults = new int[inItems.Length];
                if (sync)
                {
                    try
                    {
                        result = m_daSubscription.Write(inItems, inValues, out inResults, new ExecutionOptions());
                    }
                    catch (Exception ex)
                    {
                        log.Error("OPC DA subscription Write: " + ex.Message);
                    }
                    StoreWrittenItems(inItems, inValues, inResults);
                }
                else
                {
                    m_daSubscription.Write(inItems, inValues, out inResults, m_executionOptions);
                    m_executionOptions.ExecutionContext++;
                    // logging will be done in daSubscription.HandleSubscriptionWriteCompleted()
                    StoreItems(inItems, inValues);
                }
            }
        }
        public void StoreWrittenItems(DaItem[] items, ValueQT[] values, int[] results)
        {
            if (log.IsDebugEnabled) log.Debug("StoreWrittenItems()");
            for (int i = 0; i < values.Length; i++)
            {
                string tagname = items[i].Id;
                this.m_dictValues[tagname] = values[i];
                if (results[i] < 0)
                {
                    log.Error($"Write {items[i]} = {values[i]} => {Enum.GetName(typeof(EnumResultCode), results[i])}");
                }
                else
                {
                    LogItem(items[i], values[i]);
                }
            }
        }
        public void StoreItems(DaItem[] items, ValueQT[] values)
        {
            if (log.IsDebugEnabled) log.Debug("StoreItems()");
            for (int i = 0; i < values.Length; i++)
            {
                string tagname = items[i].Id;
                this.m_dictValues[tagname] = values[i];
                LogItem(items[i], values[i]);
            }
        }
        private void LogItem(DaItem aItem, ValueQT aValueQT)
        {
            if (log.IsDebugEnabled) log.Debug("LogItem()");

            Type mType = aValueQT.Data.GetType();
            if (mType == typeof(UInt16))
            {
                UInt16 mValue = (UInt16)aValueQT.Data;
                log.Info($"Writing {aItem.Id} {mValue} = 0x{mValue.ToString("X2")}");
            }
            else if (mType == typeof(UInt32))
            {
                UInt32 mValue = (UInt32)aValueQT.Data;
                log.Info($"Writing {aItem.Id} {mValue} = 0x{mValue.ToString("X4")}");
            }
            else if (mType == typeof(UInt64))
            {
                UInt64 mValue = (UInt64)aValueQT.Data;
                log.Info($"Writing {aItem.Id} {mValue} = 0x{mValue.ToString("X6")}");
            }
            else if (mType == typeof(Int16))
            {
                Int16 mValue = (Int16)aValueQT.Data;
                log.Info($"Writing {aItem.Id} {mValue} = 0x{mValue.ToString("X2")}");
            }
            else if (mType == typeof(Int32))
            {
                Int32 mValue = (Int32)aValueQT.Data;
                log.Info($"Writing {aItem.Id} {mValue} = 0x{mValue.ToString("X4")}");
            }
            else if (mType == typeof(Int64))
            {
                Int64 mValue = (Int64)aValueQT.Data;
                log.Info($"Writing {aItem.Id} {mValue} = 0x{mValue.ToString("X6")}");
            }
            else
            {
                log.Info($"Writing {aItem.Id} {aValueQT.Data} {mType.Name}");
            }
        }
        public void ReadUsingSession(bool sync)
		{
			if (sync)
			{
				int result = (int)EnumResultCode.E_FAIL;
				// in case of a XML-DA server use a valid item paths array instead of "null"
				if (ResultCode.SUCCEEDED(result = m_daSession.Read(0, m_itemIds, null, out m_values, out m_results, new ExecutionOptions())))
				{
					if (log.IsDebugEnabled) log.Debug(" Session synchronous read succeeded!");
					for (int i = 0; i< m_itemList.Length;i++)
					{
						if (m_results[i] >= 0)
						{
                            if (log.IsDebugEnabled)
							log.Debug( String.Format("{0,-19} {1} {2,-50} ",m_itemIds[i]," = ", m_values[i].ToString()));	
						}
						else
						{
							log.Error("OPC client Synchronous read failed for item " + m_itemList[i].Id + " : " + Enum.GetName(typeof(EnumResultCode), m_results[i]));
						}
					}
				}
				else
				{
					log.Error("OPC client Session synchronous read failed! Result: " + Enum.GetName(typeof(EnumResultCode), result));
				}
			}
			else 
			{
				// in case of a XML-DA server use a valid item paths array instead of "null"
				m_daSession.Read(0, m_itemIds, null, out m_values, out m_results, m_executionOptions);
				m_executionOptions.ExecutionContext++;	
			}
		}

		public void WriteUsingSession(string[] inItemIds, ValueQT[] inValues)
		{
            int[] inResults = new int[inValues.Length];
            int result = (int)EnumResultCode.E_FAIL;
            if (sync)
			{
				// in case of a XML-DA server use a valid item paths array instead of "null"
				if (ResultCode.SUCCEEDED(result = m_daSession.Write(inItemIds, null, inValues, out inResults, new ExecutionOptions())))
				{
                    if (log.IsDebugEnabled)
    					log.Debug("OPC client WriteUsingSession: synchronous write succeeded");
					for (int i = 0; i< inItemIds.Length;i++)
					{
						if (inResults[i] >= 0)
						{
                            if (log.IsDebugEnabled)
    							log.Debug( String.Format("{0,-19} {1} {2,-50} ",inItemIds[i]," = ", inValues[i].ToString()));	
						}
						else
						{
							log.Error("OPC client WriteUsingSession: synchronous write failed! " + " Item: " + inItemIds[i] + " : " + Enum.GetName(typeof(EnumResultCode), inResults[i]));
						}
					}
				}
				else
				{
					log.Error("OPC client WriteUsingSession: synchronous write failed! " + Enum.GetName(typeof(EnumResultCode),result));
				}
			}
			else //Async execution
			{
				// in case of a XML-DA server use a valid item paths array instead of "null"
				m_daSession.Write(inItemIds, null, inValues, out inResults, m_executionOptions);
				m_executionOptions.ExecutionContext++;
			}
		}

		public void GetServerStatus()
		{
			ServerStatus newServerStatus;
			ServerStatus serverStatus;
			if (sync)
			{
				int result = (int)EnumResultCode.E_FAIL;

				if (ResultCode.SUCCEEDED(result = m_daSession.GetStatus(out serverStatus, new ExecutionOptions()))) 
				{
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("Server Status");
                        log.Debug("	Vendor info: " + serverStatus.VendorInfo);
                        log.Debug("	Product version: " + serverStatus.ProductVersion);
                        log.Debug("	State: " + serverStatus.State);
                        log.Debug("	Start time: " + serverStatus.StartTime.ToString());
                        log.Debug("	Last update time: " + serverStatus.LastUpdateTime.ToString());
                        log.Debug("	Current time: " + serverStatus.CurrentTime.ToString());
                        log.Debug("	GroupCount: " + serverStatus.GroupCount);
                        log.Debug("	Bandwidth: " + serverStatus.Bandwidth);
                        for (int i = 0; i < serverStatus.SupportedLcIds.Length; i++)
                        {
                            log.Debug("	Supported LcId: " + serverStatus.SupportedLcIds[i]);
                        }
                        log.Debug("	Status info: " + serverStatus.StatusInfo);
                    }
                }	
				else 
				{
					log.Error("Synchronous get status failed! Result: " + result);
				}
			}
			else 
			{
				m_daSession.GetStatus(out newServerStatus, m_executionOptions);
				m_executionOptions.ExecutionContext++;
			}
		}

		public void ActivateConnectionMonitor()
		{
			int result = (int)EnumResultCode.E_FAIL;
			if(ResultCode.SUCCEEDED( result = m_daSession.ActivateConnectionMonitor(true, 5000, 0, 10000, 300000)))
			{
				if (log.IsDebugEnabled) log.Debug("OPC client Activated connection monitor");

			}
			else
			{
				log.Error("OPC client Activate connection monitor failed! Result " + Enum.GetName(typeof(EnumResultCode),result));
			}
		}

		public void DeactivateConnectionMonitor()
		{
			int result = (int)EnumResultCode.E_FAIL;
			if(ResultCode.SUCCEEDED(result = m_daSession.ActivateConnectionMonitor(false, 0, 0, 0, 0)))
			{
				if (log.IsDebugEnabled) log.Debug("OPC client Deactivated connection monitor");
			}
			else
			{
				log.Error("OPC client Deactivate connection monitor failed! Result: " + Enum.GetName(typeof(EnumResultCode),result));
			}		
		}

        #endregion

        #region Reconnect

        private DateTime dtStartup = DateTime.MinValue;
        private System.Timers.Timer tmrConnected = new System.Timers.Timer();
        public static void ConnectionStateChanged(EnumObjectState state)
        {
            if (log.IsDebugEnabled) log.Debug("OpcClient ConnectionStateChanged " + Enum.GetName(typeof(EnumObjectState), state));
            if (state == EnumObjectState.CONNECTED)
            {
                instance.WriteAll(500);
            }
        }
        public void WriteAll(Int32 msDelay)
        {
            if (dtStartup == DateTime.MinValue)
            {
                dtStartup = DateTime.Now;
            }
            else 
            {
                TimeSpan t = DateTime.Now - dtStartup;
                if (t.Minutes > 1)
                {
                    Int32 interval = (msDelay >= 100) ? msDelay : 100;

                    if (!this.tmrConnected.Enabled)
                    {
                        this.tmrConnected.Interval = interval;
                    }
                    this.tmrConnected.Enabled = true;
                }
            }
        }
        private void TmrConnected_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.tmrConnected.Enabled = false;
            this.WriteAll();
        }
        private bool WriteAll()
        {
            if (log.IsDebugEnabled) log.Debug("OpcClient Write all tags");
            if (m_daSubscription != null)
            {
                ValueQT[] inValues = new ValueQT[m_dictValues.Count];
                m_dictValues.Values.CopyTo(inValues, 0);
                MyDaItem[] inItems = new MyDaItem[m_dictValues.Count];
                m_dictItems.Values.CopyTo(inItems, 0);
                WriteItems(inItems, inValues);
            }
            return false;
        }
        #endregion
        public void LogInSecure()
		{
			int result = (int)EnumResultCode.E_ACCESSDENIED;
			EnumObjectState SessionState;
			SessionState = m_daSession.CurrentState;
			if (sync)
			{
				if (SessionState != EnumObjectState.DISCONNECTED)
				{
					result = m_daSession.Logon(m_usr,m_pwd,new ExecutionOptions());
					log.Info("Logon for user: " + m_usr + "  pass:" + m_pwd + " " +Enum.GetName(typeof(EnumResultCode), result));
				}
				else
				{
					log.Error("Logon for secure connection failed - session is not connected.");
				}
			}
			else
			{
				m_daSession.Logon(m_usr,m_pwd,m_executionOptions);
				m_executionOptions.ExecutionContext++;
			}
		}
		public void LogOffSecure()
		{
			int result = (int)EnumResultCode.E_ACCESSDENIED;
			EnumObjectState SessionState;
			SessionState = m_daSession.CurrentState;
			if (sync)
			{
				if (SessionState != EnumObjectState.DISCONNECTED)
				{
					result = m_daSession.Logoff(new ExecutionOptions());
					log.Info("Logoff for user: " + m_usr + "  pass:" + m_pwd + " " + Enum.GetName(typeof(EnumResultCode),result));
				}
				else
				{
				    log.Error("Logoff for secure connection failed - session is not connected.");
				}
			}
			else
			{
				m_daSession.Logoff(m_executionOptions);
				m_executionOptions.ExecutionContext++;
			}
		}
		
        #region Public Properties

		public string ServiceName
		{
			get
			{
				return Application.Instance.ServiceName;
			}
			set
			{
				Application.Instance.ServiceName = value;
			}
		}

		#endregion
	}
}
