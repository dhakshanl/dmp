using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MissionPlanner.Utilities
{
    /// <summary>
    /// Shared PX4 ULog loading/parsing helper, extracted from GCSViews.Help so both the
    /// Help tab and the Mission dashboard's log browser can drive ULog playback from the
    /// same code instead of duplicating the topic-filtering logic.
    /// </summary>
    public static class ULogLoader
    {
        public class ULogTopics
        {
            public List<ULog.message_data_s> Attitude;
            public List<ULog.message_data_s> Battery;
            public List<ULog.message_data_s> LocalPos;
            public List<ULog.message_data_s> LandDetected;
            public List<ULog.message_data_s> Armed;
            public ulong BaseTimestamp;
            public ulong EndTimestamp;
        }

        /// <summary>
        /// Reads and topic-filters a .ulg file. Throws InvalidOperationException if the
        /// file contains none of the recognised topics, or the underlying read/parse
        /// exception on failure.
        /// </summary>
        public static ULogTopics Load(string filePath)
        {
            var ulog = new ULog();
            using (var fs = File.OpenRead(filePath))
                ulog.read(fs);

            var topics = new ULogTopics
            {
                Attitude = ulog.Data.Where(a => a.Type == "vehicle_attitude" && a.MultiID == 0).OrderBy(GetTimestamp).ToList(),
                Battery = ulog.Data.Where(a => a.Type == "battery_status" && a.MultiID == 0).OrderBy(GetTimestamp).ToList(),
                LocalPos = ulog.Data.Where(a => a.Type == "vehicle_local_position" && a.MultiID == 0).OrderBy(GetTimestamp).ToList(),
                LandDetected = ulog.Data.Where(a => a.Type == "vehicle_land_detected" && a.MultiID == 0).OrderBy(GetTimestamp).ToList(),
                Armed = ulog.Data.Where(a => a.Type == "actuator_armed" && a.MultiID == 0).OrderBy(GetTimestamp).ToList(),
            };

            var allTimestamps = topics.Attitude.Concat(topics.Battery).Concat(topics.LocalPos)
                .Concat(topics.LandDetected).Concat(topics.Armed)
                .Select(GetTimestamp).Where(t => t > 0).ToList();

            if (allTimestamps.Count == 0)
                throw new InvalidOperationException("No recognised topics found in this log.");

            topics.BaseTimestamp = allTimestamps.Min();
            topics.EndTimestamp = allTimestamps.Max();

            return topics;
        }

        public static ulong GetTimestamp(ULog.message_data_s msg)
        {
            if (msg.Raw != null && msg.Raw.TryGetValue("timestamp", out var t))
                return Convert.ToUInt64(t);
            return 0;
        }

        public static bool TryGetDouble(Dictionary<string, object> raw, string key, out double value)
        {
            value = 0;
            if (raw == null || !raw.TryGetValue(key, out var obj) || obj == null)
                return false;
            try
            {
                value = Convert.ToDouble(obj);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryGetArray(Dictionary<string, object> raw, string key, out object[] value)
        {
            value = null;
            if (raw == null || !raw.TryGetValue(key, out var obj) || !(obj is object[] arr))
                return false;
            value = arr;
            return true;
        }
    }
}
