using log4net;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace NetworkMonitor.wincc;

internal static class TagConnections
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    public static bool RequestWriteAllTags;

    private static HashSet<string> m_WriteTags = new HashSet<string>();

    private static HashSet<string> m_ReadTags = new HashSet<string>();

    public static List<string> GetReadTagSet()
    {
        if (configuration.deviceList.Length != 0)
        {
            HostDevice[] deviceList = configuration.deviceList;
            foreach (HostDevice obj in deviceList)
            {
                Add(obj.tagvalues.TagName + ".DISABLED", m_ReadTags);
                Add(obj.tagvalues.TagName + ".STATUS", m_ReadTags);
            }
        }
        return new List<string>(m_ReadTags);
    }

    public static List<string> GetAllWriteTagList()
    {
        if (m_WriteTags.Count == 0 && configuration.deviceList.Length != 0)
        {
            HostDevice[] deviceList = configuration.deviceList;
            foreach (HostDevice device in deviceList)
            {
                if (!configuration.OnlyIntegerTags)
                {
                    Add(device.tagvalues.TagName + ".DESCRIPTION", m_WriteTags);
                    Add(device.tagvalues.TagName + ".NAME", m_WriteTags);
                }
                Add(device.tagvalues.TagName + ".STATUS", m_WriteTags);
                if (device.ups)
                {
                    Add(device.tagvalues.TagName + ".TIME", m_WriteTags);
                    continue;
                }
                Add(device.tagvalues.TagName + ".DISABLED", m_WriteTags);
                Add(device.tagvalues.TagName + ".UP", m_WriteTags);
                Add(device.tagvalues.TagName + ".DOWN", m_WriteTags);
            }
        }
        return new List<string>(m_WriteTags);
    }

    private static void Add(string tagname, HashSet<string> List)
    {
        try
        {
            List.Add(tagname);
        }
        catch
        {
            log.Error("TagLists: already used: " + tagname);
        }
    }

    public static List<KeyValuePair<string, object>> GetAllTags()
    {
        RequestWriteAllTags = true;
        List<KeyValuePair<string, object>> TagUpdates = new List<KeyValuePair<string, object>>();
        for (int d = 0; d < configuration.deviceList.Length; d++)
        {
            GetChangedTags(ref configuration.deviceList[d], ref TagUpdates);
        }
        RequestWriteAllTags = false;
        return TagUpdates;
    }

    public static List<KeyValuePair<string, object>> GetChangedTags()
    {
        List<KeyValuePair<string, object>> TagUpdates = new List<KeyValuePair<string, object>>();
        for (int d = 0; d < configuration.deviceList.Length; d++)
        {
            GetChangedTags(ref configuration.deviceList[d], ref TagUpdates);
        }
        if (log.IsDebugEnabled && TagUpdates.Count > 0)
        {
            log.Debug("< GetChangedTags found " + TagUpdates.Count);
        }
        return TagUpdates;
    }

    public static List<KeyValuePair<string, object>> GetChangedTags(ref HostDevice Device)
    {
        List<KeyValuePair<string, object>> tags = new List<KeyValuePair<string, object>>();
        GetChangedTags(ref Device, ref tags);
        return tags;
    }

    public static void GetChangedTags(ref HostDevice device, ref List<KeyValuePair<string, object>> tags)
    {
        if (device == null)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug(">GetTags Device null");
            }
            return;
        }
        if (device.mySwitch != null && (device.mySwitch.ping.ConnectionBrokenAlarm | !device.mySwitch.ping.PingAlive))
        {
            if (log.IsDebugEnabled)
            {
                log.Debug("GetTags(" + device.IP.ToString() + ") skipped: mySwitch (" + device.mySwitch.IP.ToString() + ") Ping not Alive ");
            }
            return;
        }
        uint Value = 0u;
        string Area = null;
        Value = 0u;
        Area = "begin " + device.host.HostName;
        try
        {
            if (device.tagvalues.UpdateSystemTagRequested)
            {
                Area = "Update System";
                if (device.snmpvalues.SystemName.Length > 0)
                {
                    tags.Add(new KeyValuePair<string, object>(device.tagvalues.TagName + ".NAME", device.snmpvalues.SystemName));
                    device.tagvalues.UpdateSystemTagRequested = false;
                    if (device.snmpvalues.Description.Length > 0)
                    {
                        tags.Add(new KeyValuePair<string, object>(device.tagvalues.TagName + ".DESCRIPTION", device.snmpvalues.Description));
                    }
                }
            }
            if (device.tagvalues.UpdatePortsTagsRequested || RequestWriteAllTags)
            {
                Area = "Update Ports";
                tags.Add(new KeyValuePair<string, object>(device.tagvalues.TagName + ".UP", device.tagvalues.tagUpValue));
                if (log.IsDebugEnabled)
                {
                    log.Debug(device.tagvalues.TagName + ".UP  \t0x" + device.tagvalues.tagUpValue.ToString("X4") + "\t" + device.tagvalues.tagUpValue);
                }
                tags.Add(new KeyValuePair<string, object>(device.tagvalues.TagName + ".DOWN", device.tagvalues.tagDownValue));
                if (log.IsDebugEnabled)
                {
                    log.Debug(device.tagvalues.TagName + ".DOWN\t0x" + device.tagvalues.tagDownValue.ToString("X4") + "\t" + device.tagvalues.tagDownValue);
                }
                device.tagvalues.UpdatePortsTagsRequested = false;
            }
            Value = 0u;
            Area = "update ups Status";
            if (device.ups)
            {
                Value |= (uint)(device.upsStatus.AbnormalCondition ? 256 : 0);
                Value |= (uint)(device.upsStatus.BatteryInuse ? 512 : 0);
                Value |= (uint)(device.upsStatus.LowBattery ? 1024 : 0);
                Value |= (uint)(device.upsStatus.OnLine ? 2048 : 0);
                Value |= (uint)(device.upsStatus.ReplaceBattery ? 4096 : 0);
                Value |= (uint)(device.upsStatus.Overload ? 8192 : 0);
                Value |= (uint)(device.upsStatus.PoweredOff ? 16384 : 0);
            }


            Area = "update connection";
            if (!(device.ping.PingAlive & !device.ping.ConnectionBrokenAlarm))
            {
                // connection broke
                Value |= 2;
            }
            else
            {
                // connection ok
                Value |= 1;
                // RFC3433 Sensor
                if (device.Power1ok) Value |= 0x01000000;
                if (device.Power2ok) Value |= 0x02000000;
                if (device.Power1Failed) Value |= 0x04000000;
                if (device.Power1Failed) Value |= 0x08000000;

                // Moxa VRRP
                if (device.vrrpStatus.Available)
                {
                    if (device.vrrpStatus.Enabled) Value |= 0x10000000;
                    if (device.vrrpStatus.Disabled) Value |= 0x20000000;
                    if (device.vrrpStatus.Master) Value |= 0x40000000;
                    if (device.vrrpStatus.Backup) Value |= 0x80000000;
                }
            }
            if (device.tagvalues.TagDisabledHasChangedDelayCounter > 0)
            {
                Value |= 4;
            }
            if (device.snmpvalues.CountSnmpGetTimeOut > 1)
            {
                Value |= 8;
            }
            if (device.snmpvalues.snmpGetNoResponse > 1)
            {
                Value |= 0x10;
            }
            if (device.RingStatus == HostDevice.RingStatusValues.AbnormalOpen)
            {
                Value |= 0x20;
            }
            Area = "add tag";
            if (device.tagvalues.tagStatusConfirmed != Value || RequestWriteAllTags)
            {
                tags.Add(new KeyValuePair<string, object>(device.tagvalues.TagName + ".STATUS", Value));
                if (GUI.IsGuiVisible || log.IsDebugEnabled)
                {
                    string data = device.tagvalues.TagName + ".STATUS\t0x" + Value.ToString("X4");
                    if (log.IsDebugEnabled)
                    {
                        log.Debug(data);
                    }
                    if (GUI.IsGuiVisible)
                    {
                        GUI.AddMsgToListView(data);
                    }
                }
                device.tagvalues.UpdateStatusTagRequested = false;
                device.tagvalues.tagStatusConfirmed = Value;
            }
            Area = "Update UPS Time";
            if (device.upsStatus.UpdateBatteryRunTimeRequested || RequestWriteAllTags)
            {
                if (device.upsStatus.BatteryRunTimeRemaining > uint.MaxValue)
                {
                    tags.Add(new KeyValuePair<string, object>(device.tagvalues.TagName + ".TIME", uint.MaxValue));
                }
                else
                {
                    tags.Add(new KeyValuePair<string, object>(device.tagvalues.TagName + ".TIME", (uint)device.upsStatus.BatteryRunTimeRemaining));
                }
                device.upsStatus.UpdateBatteryRunTimeRequested = false;
            }
            Area = "disabled tag";
            if (device.tagvalues.tagDisabledExist && (device.tagvalues.UpdateDisabledTagRequested || RequestWriteAllTags))
            {
                tags.Add(new KeyValuePair<string, object>(device.tagvalues.TagName + ".DISABLED", device.tagvalues.tagDisabledValue));
                if (log.IsDebugEnabled)
                {
                    log.Debug(device.tagvalues.TagName + ".DISABLED = 0x" + device.tagvalues.tagDisabledValue.ToString("X4"));
                }
                device.tagvalues.UpdateDisabledTagRequested = false;
            }
        }
        catch (Exception ex)
        {
            if (log.IsInfoEnabled)
            {
                log.Info("GetTags(" + device.tagvalues.TagName + ",  " + Area + ") - " + ex.Message);
            }
        }
    }

    public static void UpdatePortTags(ref HostDevice device)
    {
        if (log.IsDebugEnabled)
        {
            log.Debug($">UpdatePortTags {device.IP}");
        }
        if (device == null)
        {
            return;
        }
        uint tagUp = 0u;
        uint tagDown = 0u;
        uint tagDisabled = 0u;
        try
        {
            for (int index = 0; index < device.Ports.Length; index++)
            {
                if (device.Ports[index].ifAdminStatus == 2)
                {
                    tagDisabled |= (uint)(1 << index);
                }
                long operStatus = device.Ports[index].OperStatus;
                long num = operStatus - 1;
                if ((ulong)num <= 6uL)
                {
                    switch (num)
                    {
                        case 0L:
                            tagUp |= (uint)(1 << index);
                            break;
                        case 1L:
                            tagDown |= (uint)(1 << index);
                            break;
                        case 6L:
                            tagDown |= (uint)(1 << index);
                            break;
                        case 4L:
                            tagDown |= (uint)(1 << index);
                            break;
                    }
                }
            }
            if (device.tagvalues.tagUpValue != tagUp)
            {
                device.tagvalues.tagUpValue = tagUp;
                device.tagvalues.UpdatePortsTagsRequested = true;
                if (GUI.IsGuiVisible || log.IsDebugEnabled)
                {
                    string data = "+UpdatePortTags " + device.IP?.ToString() + ": " + device.tagvalues.TagName + ".UP = " + device.tagvalues.tagUpValue + " = 0x" + device.tagvalues.tagUpValue.ToString("X2");
                    if (log.IsDebugEnabled)
                    {
                        log.Debug(data);
                    }
                    if (GUI.IsGuiVisible)
                    {
                        GUI.AddMsgToListView(data);
                    }
                }
            }
            if (device.tagvalues.tagDownValue != tagDown)
            {
                device.tagvalues.tagDownValue = tagDown;
                device.tagvalues.UpdatePortsTagsRequested = true;
                if (log.IsDebugEnabled)
                {
                    log.Debug("+UpdatePortTags " + device.IP?.ToString() + ": " + device.tagvalues.TagName + ".DOWN = " + device.tagvalues.tagDownValue + " = 0x" + device.tagvalues.tagDownValue.ToString("X2"));
                }
            }
            if (!device.tagvalues.tagDisabledExist || device.tagvalues.tagDisabledValue == tagDisabled)
            {
                return;
            }
            device.tagvalues.tagDisabledValue = tagDisabled;
            device.tagvalues.UpdateDisabledTagRequested = true;
            device.tagvalues.TagDisabledHasChangedDelayCounter = 2;
            if (GUI.IsGuiVisible || log.IsDebugEnabled)
            {
                string data2 = "+UpdatePortTags " + device.IP?.ToString() + ": " + device.tagvalues.TagName + ".DISABLED = " + device.tagvalues.tagDisabledValue + " = 0x" + device.tagvalues.tagDisabledValue.ToString("X2");
                if (log.IsDebugEnabled)
                {
                    log.Debug(data2);
                }
                if (GUI.IsGuiVisible)
                {
                    GUI.AddMsgToListView(data2);
                }
            }
        }
        catch (Exception ex)
        {
            if (log.IsInfoEnabled)
            {
                log.Info("UpdatePortTags " + device?.ToString() + ": " + ex.Message);
            }
        }
    }
}
