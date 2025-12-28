using System;

namespace GM
{
    public static class Utils
    {
        public static string ShortDeviceID()
        {
            // 取得 Unity 內建唯一裝置識別碼
            string unique = UnityEngine.SystemInfo.deviceUniqueIdentifier;
            // 建議 hash 並取前 8 碼，避免太長
            int hash = unique.GetHashCode();
            return Math.Abs(hash).ToString("X8"); // 8位16進位字串
            // 或直接 return unique.Substring(0, 8); 但 hash 更保險
        }
    }
}