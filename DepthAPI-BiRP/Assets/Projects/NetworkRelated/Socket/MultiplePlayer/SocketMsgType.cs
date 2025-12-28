using System.Collections.Generic;
using UnityEngine;

namespace MultiPlayer {
    [System.Serializable]
    public class SocketPayloadBasic : GM.JsonFuncBase {
        public string message_type = "";

        public SocketPayloadBasic() {
            message_type = "";
        }

        public void SetupMessageType(SocketMsgType.ToServer val) {
            message_type = SocketMsgType.GetString(val);
        }

        public void SetupMessageType(SocketMsgType.FromServer val) {
            message_type = SocketMsgType.GetString(val);
        }
    }

    public static class SocketMsgType {
        public enum ToServer {
            Unknown,
            Heartbeat,
            ReadyToMove,
            ShotEvent,
            Lantern,
            QA,
            ResumeQA
        }
        public enum FromServer {
            Unknown,
            Update,
            MoveCommand,
            ShotEvent,
            Lantern,
            QA,
            AssignSequence
        }

        public static readonly string TS_Heartbeat = "heartbeat";
        public static readonly string TS_Ready2Move = "ready_to_move";
        public static readonly string TS_Shot = "shot_event";
        public static readonly string TS_Lantern = "lantern";
        public static readonly string TS_QA = "qa";
        public static readonly string TS_ResumeQA = "resume_qa";

        public static readonly string FS_Update = "update";
        public static readonly string FS_MoveCmd = "move_command";
        public static readonly string FS_ShotEvt = "shot_event";
        public static readonly string FS_LanternEvt = "lantern";
        public static readonly string FS_QAEvt = "qa";
        public static readonly string FS_AssignSeq = "assign_sequence";

        private static readonly Dictionary<string, ToServer> toServerStringToEnumMap;
        private static readonly Dictionary<ToServer, string> toServerEnumToStringMap;

        private static readonly Dictionary<string, FromServer> fromServerStringToEnumMap;
        private static readonly Dictionary<FromServer, string> fromServerEnumToStringMap;

        static SocketMsgType() {
            toServerStringToEnumMap = new Dictionary<string, ToServer>();
            toServerEnumToStringMap = new Dictionary<ToServer, string>();
            fromServerStringToEnumMap = new Dictionary<string, FromServer>();
            fromServerEnumToStringMap = new Dictionary<FromServer, string>();

            // 初始化 To Server 訊息的映射
            AddToServerMapping(TS_Heartbeat, ToServer.Heartbeat);
            AddToServerMapping(TS_Ready2Move, ToServer.ReadyToMove);
            AddToServerMapping(TS_Shot, ToServer.ShotEvent);
            AddToServerMapping(TS_Lantern, ToServer.Lantern);
            AddToServerMapping(TS_QA, ToServer.QA);
            AddToServerMapping(TS_ResumeQA, ToServer.ResumeQA);

            // 初始化 From Server 訊息的映射
            AddFromServerMapping(FS_Update, FromServer.Update);
            AddFromServerMapping(FS_MoveCmd, FromServer.MoveCommand);
            AddFromServerMapping(FS_ShotEvt, FromServer.ShotEvent);
            AddFromServerMapping(FS_LanternEvt, FromServer.Lantern);
            AddFromServerMapping(FS_QAEvt, FromServer.QA);
            AddFromServerMapping(FS_AssignSeq, FromServer.AssignSequence);
        }

        private static void AddToServerMapping(string strValue, ToServer enumValue) {
            if (!toServerStringToEnumMap.ContainsKey(strValue)) {
                toServerStringToEnumMap.Add(strValue, enumValue);
            } else if (toServerStringToEnumMap[strValue] != enumValue) {
                Debug.LogWarning($"Warning: ToServer string '{strValue}' is already mapped to '{toServerStringToEnumMap[strValue]}', cannot map to '{enumValue}'.");
            }

            if (!toServerEnumToStringMap.ContainsKey(enumValue)) {
                toServerEnumToStringMap.Add(enumValue, strValue);
            }
        }

        private static void AddFromServerMapping(string strValue, FromServer enumValue) {
            if (!fromServerStringToEnumMap.ContainsKey(strValue)) {
                fromServerStringToEnumMap.Add(strValue, enumValue);
            } else if (fromServerStringToEnumMap[strValue] != enumValue) {
                Debug.LogWarning($"Warning: FromServer string '{strValue}' is already mapped to '{fromServerStringToEnumMap[strValue]}', cannot map to '{enumValue}'.");
            }

            if (!fromServerEnumToStringMap.ContainsKey(enumValue)) {
                fromServerEnumToStringMap.Add(enumValue, strValue);
            }
        }

        public static ToServer GetToServerMessageType(string messageTypeString) {
            if (string.IsNullOrEmpty(messageTypeString)) {
                return ToServer.Unknown;
            }

            if (toServerStringToEnumMap.TryGetValue(messageTypeString, out ToServer type)) {
                return type;
            }
            return ToServer.Unknown;
        }

        public static string GetString(ToServer messageType) {
            if (toServerEnumToStringMap.TryGetValue(messageType, out string messageString)) {
                return messageString;
            }
            return string.Empty;
        }

        public static FromServer GetFromServerMessageType(string messageTypeString) {
            if (string.IsNullOrEmpty(messageTypeString)) {
                return FromServer.Unknown;
            }

            if (fromServerStringToEnumMap.TryGetValue(messageTypeString, out FromServer type)) {
                return type;
            }
            return FromServer.Unknown;
        }

        public static string GetString(FromServer messageType) {
            if (fromServerEnumToStringMap.TryGetValue(messageType, out string messageString)) {
                return messageString;
            }
            return string.Empty;
        }
    }
}