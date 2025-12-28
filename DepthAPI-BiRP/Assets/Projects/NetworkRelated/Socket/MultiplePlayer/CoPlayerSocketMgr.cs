//using SIGGRAPH_2024;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
//using static SIGGRAPH_2024.TrackingSystem;

namespace MultiPlayer {
    public class CoPlayerSocketMgr : MonoBehaviour {
        public static CoPlayerSocketMgr Instance { get; private set; }


        [Header("Mode")]
        public bool IsDepthBasedMode = true;

        [Header("ChapterManager")]
        public ChapterManager ChapterManager;

        [Header("Socket Parameters")]
        public string serverIP = "127.0.0.1";
        public int serverPort = 8888;
        public string serverPath = "";
        private string deviceShortID = "";
        public string DeviceShortID {
            get { return deviceShortID; }
        }
        public string ServerUri {
            get {
                return $"ws://{serverIP}:{serverPort}/{serverPath}/{deviceShortID}";
            }
        }

        public float reconnectInterval = 5f; // in Seconds(?)
        public float heartbeatInterval = 0.025f; // 40fps: in Seconds instead of Ticks(?)
        public float heartbeatTimeout = 30f; // in Ticks(?)

        [Header("Chapter-related")]
        public ChapterManager chapterManager;

        [Header("GM Scripts")]
        //public AweTest.MRUKMapper SpaceCalibrationMapper = null;

        private ClientWebSocket wsClient;
        public bool IsConnected { get; private set; } = false;

        [Header("Player Position Sync. Part")]
        public GameObject HMDObj;
        public GameObject RightControllerObj;
        public GameObject LeftControllerObj;
        public AI4Animation.Actor Actor;
        //public TrackingSystem TS; // for Scale
        public OVRHand RightOVRHand;
        public OVRHand LeftOVRHand;
        //public Interaction.QuestDetectHand RHandDector;
        //public Interaction.QuestDetectHand LHandDector;

        [Space]
        //public GameObject PlayerVisualPrefab;
        ////!!++ Other Players Data: string "deviceShortID"
        //private Dictionary<string, GameObject> otherPlayers = new Dictionary<string, GameObject>();
        //private Dictionary<string, DateTime> lastReceivedTimePerPlayer = new Dictionary<string, DateTime>();

        private Queue<string> receivedMessages = new Queue<string>();
        private readonly object messageQueueLock = new object();

        private long lastHeartbeatSendTime;
        private long lastHeartbeatReceiveTime;

        public event Action<string> OnMessageReceived;
        public event Action<bool> OnConnectionStatusChanged;
        private CancellationTokenSource cancellationTokenSource;

#region To Server Objects
        private HeartbeatPayload heartbeatPayload;
        private ReadyToMovePayload readyToMovePayload;
#endregion

#region From Server Objects
        private PositionInfo positionInfo = new PositionInfo();
#endregion
        

        void Awake() {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
            } else {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
        }

        void InitialData() {
            deviceShortID = GM.Utils.ShortDeviceID();
            heartbeatPayload = new HeartbeatPayload(deviceShortID);
            readyToMovePayload = new ReadyToMovePayload(deviceShortID);
        }

        void Start() {
            InitialData();
            StartClient();
        }

        void FixedUpdate()
        {
            lock (messageQueueLock)
            { //!!: 全部deque+handle完才解鎖，可能可以改成全部dequeue並解鎖再慢慢處理
                while (receivedMessages.Count > 0)
                {
                    string message = receivedMessages.Dequeue();
                    // Debug.Log("<color=white>[CoPlayer Socket]</color> Recv.: " + message);
                    try { 
                        SocketPayloadBasic socketPayloadBasic = new SocketPayloadBasic();
                        socketPayloadBasic.LoadFromJson(message);
                        RecvMsgHandler(socketPayloadBasic.message_type, message);

                        OnMessageReceived?.Invoke(message);

                        lastHeartbeatReceiveTime = DateTime.Now.Ticks;
                    }
                    catch (Exception ex) { //!!++ Added: Try-Catch for JSON Parse Exception
                        // Log diagnostic info: length and snippet to help server-side debug
                        int len = message?.Length ?? 0;
                        string head = len > 128 ? message.Substring(0, 128) : message;
                        string tail = len > 128 ? message.Substring(Math.Max(0, len - 128)) : string.Empty;
                        Debug.LogError($"<color=red>[CoPlayer Socket]</color> JSON parse failed. Ex: {ex.Message}. Len={len}. Head='{head}' Tail='{tail}'");
                        // Optionally dump to file / send telemetry for postmortem
                    }

                }
            }
                    

            if (IsConnected)
            {
                // Heartbeat PING
                if (DateTime.Now.Ticks/*Time.fixedTime*/ - lastHeartbeatSendTime >= heartbeatInterval * TimeSpan.TicksPerSecond) //!*Modified: translated from second to ticks
                {
                    PassHeartbeat();
                    lastHeartbeatSendTime = DateTime.Now.Ticks;
                }

                // // Check Timeout
                // if (DateTime.Now.Ticks - lastHeartbeatReceiveTime >= heartbeatTimeout * TimeSpan.TicksPerSecond) {
                //     Debug.LogWarning("<color=white>[CoPlayer Socket]</color> Heartbeat Timeout.");
                //     Disconnect();
                //     Invoke("Reconnect", reconnectInterval);
                // }
            }

            //!!++Added: Check if other players are timeout (no data for 5 seconds)
            
            //ChapterManager.CheckOtherPlayersTimeout();
            //List<string> toRemove = new List<string>();
            //foreach (var kvp in otherPlayers)
            //{
            //    string deviceId = kvp.Key;
            //    GameObject playerObj = kvp.Value;
            //    if (lastReceivedTimePerPlayer.TryGetValue(deviceId, out var lastTime))
            //    {
            //        double secondsSinceLast = (DateTime.Now - lastTime).TotalSeconds;
            //        if (secondsSinceLast > 5.0)
            //        {
            //            // 超過5秒沒收到，視同斷線
            //            playerObj.SetActive(false);

            //            // 超過10秒，釋放物件
            //            if (secondsSinceLast > 10.0)
            //            {
            //                Destroy(playerObj);
            //                toRemove.Add(deviceId);
            //            }
            //        }
            //    }
            //    else
            //    {
            //        // 沒有收到過資料也隱藏
            //        playerObj.SetActive(false);
            //    }
            //}
            //foreach (var deviceId in toRemove)
            //{
            //    otherPlayers.Remove(deviceId);
            //    lastReceivedTimePerPlayer.Remove(deviceId);
            //}
        
        }

        void OnApplicationQuit() {
            Disconnect();
        }

        void OnDestroy() {
            Disconnect();
        }

#region Passing Data Handlers
        void RecvMsgHandler(string msg_type, string msg) {
            SocketMsgType.FromServer fromServerType = SocketMsgType.GetFromServerMessageType(msg_type);
            
            //!! Debugger tmp!!!
            //Debug.Log($"<color=white>[Socket>Chapter]</color> msg: " + msg);

            switch (fromServerType) {
                case SocketMsgType.FromServer.Update:
                    positionInfo.LoadFromJson(msg);
                    //Debug.Log("<color=white>[CoPlayer Socket]</color> Recv. Position Info:\n" + msg);

                    //!!Added: Debug for Player Count
                    //Debug.Log($"<color=white>[CoPlayer Socket]</color> {msg_type} > Player Count {positionInfo.player_count}");
                    
                    if (positionInfo.player_count > 0) {
                        //Debug.Log(">><color=cyan>[RecvMsgHandler]</color>: count = " + positionInfo.player_count);
                        positionInfo.player_position_info[0].PrintDebug();
                        
                        //Debug.Log("<color=white>[Socket>Chapter]</color> Before DealWithChapter!!"
                        //    + $"\nchapterManager = " + chapterManager
                        //    + $"\nchapterManager.ShowRedman = " + chapterManager.ShowRedman
                        //    + $"\nchapterManager.ShowOVRHand = " + chapterManager.ShowOVRHand
                        //    + $"\nchapterManager.RoomChapter = " + chapterManager.RoomChapter
                        //    + $"\nchapterManager.IsTimerActive = " + chapterManager.IsTimerActive
                        //    + $"\nchapterManager.Puzzle = " + chapterManager.Puzzle
                        //    + $"\nchapterManager.JankenRound = " + chapterManager.JankenRound
                        //    );
                        DealWithChapter();
                        //Debug.Log("<color=white>[Socket>Chapter]</color> Between DealWithChapter and AnimateOtherAvatars");
                        if (!IsDepthBasedMode)
                        {
                            ChapterManager.AnimateOtherAvatars(positionInfo, deviceShortID);
                        }
                        //Debug.Log("<color=white>[Socket>Chapter]</color> After AnimateOtherAvatars!!");
                    }
                    break;
                default:
                    Debug.Log($"<color=white>[CoPlayer Socket]</color> Type {msg_type} Not Yet Implement.\n{msg}");
                    break;
            }
        }

        void PassHeartbeat() {
            if (HMDObj == null)  //if (HMDObj == null || SpaceCalibrationMapper == null) {
            {
                Debug.Log("<color=red>[CoPlayer Socket]</color> Please Set HMD Object.");
                //    Debug.Log("<color=red>[CoPlayer Socket]</color> Please Set HMD Object or SpaceCalibrationMapper.");
                return;
            }
            //if (Actor == null)  //if (HMDObj == null || SpaceCalibrationMapper == null) {
            //{
            //    Debug.Log("<color=red>[CoPlayer Socket]</color> Please Set Actor Object.");
            //    //    Debug.Log("<color=red>[CoPlayer Socket]</color> Please Set HMD Object or SpaceCalibrationMapper.");
            //    return;
            //}

            ////!!++ Added
            //if (Actor.Bones == null || Actor.Bones.Length == 0)
            //{
            //    Debug.Log("<color=red>[CoPlayer Socket]</color> Actor.Bones is not initialized yet to send Heartbeat");
            //    return;R
            //}



            //heartbeatPayload.heartbeat.UpdateData(
            //    SpaceCalibrationMapper.GetPositionInCoordinate(HMDObj.transform.position),
            //    SpaceCalibrationMapper.GetPositionInCoordinate(RHandDector.HandWorldPosition),
            //    RHandDector.IsNowHandTracked,
            //    SpaceCalibrationMapper.GetPositionInCoordinate(LHandDector.HandWorldPosition),
            //    LHandDector.IsNowHandTracked
            //);
            //heartbeatPayload.heartbeat.UpdateData(
            //    HMDObj.transform.position,
            //    HMDObj.transform.rotation,
            //    RightControllerObj.transform.position, //SpaceCalibrationMapper.GetPositionInCoordinate(RHandDector.HandWorldPosition),
            //    RightControllerObj.transform.rotation,
            //    RightOVRHand.IsDataValid, //RHandDector.IsNowHandTracked,
            //    LeftControllerObj.transform.position, //SpaceCalibrationMapper.GetPositionInCoordinate(LHandDector.HandWorldPosition),
            //    LeftControllerObj.transform.rotation,
            //    LeftOVRHand.IsDataValid //LHandDector.IsNowHandTracked
            //);

            //Debug.LogWarning("<color=white>[CoPlayer Socket]</color> to UpdateData!!!!");
            heartbeatPayload.heartbeat.UpdateData(
                HMDObj.transform,
                //Actor.Bones, 
                (RightOVRHand.IsDataValid && RightOVRHand.IsTracked),
                (LeftOVRHand.IsDataValid && LeftOVRHand.IsTracked),
                1 //TS.GetScale()
            );
            
            heartbeatPayload.heartbeat.SetChapterCtrl( //SetChapterCtrl(int chapter, bool inCircle, bool doneJanken, int winCount)
                chapterManager.RoomChapter,
                chapterManager.IsInCircle,
                chapterManager.DoneJanken,
                chapterManager.JankenWin
            );
            //Debug.LogWarning("<color=yellow>[CoPlayer Socket]</color> Heartbeat Data Updated.");
            //heartbeatPayload.heartbeat.PrintDebug();

            //Debug.LogWarning("<color=white>[CoPlayer Socket]</color> to SendData!!!!");
            SendData(heartbeatPayload.ToJson());
            //Debug.LogWarning("<color=white>[CoPlayer Socket]</color> done SendData!!!!");
        }

        #endregion


        #region Display-related: Animate Other Avatars, DealWithChapter
        //void AnimateOtherAvatars() {
        //    foreach (var playerData in positionInfo.player_position_info) {
        //        if (playerData.device_id == deviceShortID) continue; // Skip Self
        //        // New Player: create new avatar for Dictionary 
        //        if (!otherPlayers.ContainsKey(playerData.device_id)) { 
        //            GameObject newPlayer = Instantiate(PlayerVisualPrefab);
        //            newPlayer.name = $"Player_{playerData.device_id}";
        //            otherPlayers[playerData.device_id] = newPlayer;
        //        }
        //        // Update Player Animation
        //        //GameObject playerObj = otherPlayers[playerData.device_id];
        //        GameObject scalerObj = otherPlayers[playerData.device_id];
        //        scalerObj.transform.localScale = Vector3.one * playerData.scale;

        //        //ColorChanger colorChanger = scalerObj.GetComponent<ColorChanger>();
        //        //colorChanger.ChangeAvatarColor();

        //        //GameObject playerObj = scalerObj.transform.GetChild(0).gameObject;
        //        //AI4Animation.Actor playerActor = playerObj.GetComponent<AI4Animation.Actor>();
        //        AI4Animation.Actor playerActor = scalerObj.GetComponentInChildren<AI4Animation.Actor>();
        //        //GameObject playerObj = playerActor.gameObject;
        //        if (playerActor != null) {
        //            //playerData.ApplyToActor(playerActor);
        //            // --- 套用骨骼資料 ---
        //            //// --- 建立骨名對應表 ---
        //            //var boneMap = new Dictionary<string, AI4Animation.Actor.Bone>();
        //            //foreach (var bone in playerActor.Bones)
        //            //{
        //            //    boneMap[bone.GetName()] = bone;
        //            //}
        //            // 套用 bone_transforms
        //            foreach (var boneData in playerData.bone_transforms)
        //            {
        //                //if (boneMap.TryGetValue(boneData.bone_name, out var bone))
        //                var bone = playerActor.FindBone(boneData.bone_name);
        //                if (bone != null)
        //                {
        //                    bone.SetPosition(boneData.position);
        //                    bone.SetRotation(boneData.rotation);
        //                }
        //                else
        //                {
        //                    Debug.LogWarning($"<color=red>[CoPlayer Socket]</color> Bone '{boneData.bone_name}' not found in Actor {playerData.device_id}.");
        //                }
        //            }
        //            // --- 更新最後收到時間 ---
        //            lastReceivedTimePerPlayer[playerData.device_id] = DateTime.Now;
        //            scalerObj.SetActive(true);

        //        } else {
        //            Debug.LogWarning("<color=red>[CoPlayer Socket]</color> Player Visual Prefab Missing Actor Component.");
        //        }
        //    }
        //}
        
        void DealWithChapter() {

            //Debug.LogWarning("<color=red>[Socket>Chapter]</color> In Chapter Manager.");
            if (chapterManager == null) {
                Debug.LogWarning("<color=red>[Socket>Chapter]</color> Please Set Chapter Manager.");
                return;
            }
            
            // ✅ 加入 Null 檢查
            if (positionInfo.chapter_info == null)
            {
                Debug.LogWarning("<color=red>[Socket>Chapter]</color> chapter_info is null, using default values.");
                return;
            }

            //Debug.LogWarning("<color=red>[Socket>Chapter]</color> Setting Chapter!!"
            //    + $"ShowRedman = {chapterManager.ShowRedman}" + $"ShowOVRHand = {chapterManager.ShowRedman}" );
            ////Debug.LogWarning("<color=red>[Socket>Chapter]</color> chapter_info.redman = "
            ////    + positionInfo.chapter_info.redman + " chapter_info.ovrhand = " + positionInfo.chapter_info.ovrhand);
            //Debug.LogWarning("<color=red>[Socket>Chapter]</color> chapter_info = " + positionInfo.chapter_info.ToJson());

            //Debug.LogWarning("<color=red>[Socket>Chapter]</color> Setting Chapter Info from Socket Data!!\n"
            //    + "\nchapter_info = " + positionInfo.chapter_info.ToJson()
            //    + "\nchapterManager = " + chapterManager);
            chapterManager.YourSeq = positionInfo.chapter_info.your_seq;
            chapterManager.ToReset = positionInfo.chapter_info.to_reset;
            chapterManager.ShowRedman = positionInfo.chapter_info.redman;
            chapterManager.ShowOVRHand = positionInfo.chapter_info.ovrhand;
            //Debug.LogWarning("<color=red>[Socket>Chapter]</color> Set Chapter Finished!!"
            //    + $"ShowRedman = {chapterManager.ShowRedman}" + $"ShowOVRHand = {chapterManager.ShowOVRHand}");
            chapterManager.RoomChapter = positionInfo.chapter_info.room_chapter;
            chapterManager.IsTimerActive = positionInfo.chapter_info.is_timer_active;
            chapterManager.Puzzle = positionInfo.chapter_info.puzzle;
            chapterManager.JankenRound = positionInfo.chapter_info.janken_round;
            //Debug.LogWarning("<color=red>[Socket>Chapter]</color> Puzzle Info Setted!!\n"
            //    + $"\nchapter_info.puzzle = " + positionInfo.chapter_info.puzzle.ToJson()
            //    + $"\nchapterManager.Puzzle = " + chapterManager.Puzzle);

        }
        #endregion

        #region Socket Functions
        public void StartClient() {
            if (IsConnected) {
                Debug.LogWarning("<color=white>[CoPlayer Socket]</color> Already Connected.");
                return;
            }

            cancellationTokenSource = new CancellationTokenSource();
            ConnectToServerAsync(cancellationTokenSource.Token);
        }

        private async void ConnectToServerAsync(CancellationToken cancellationToken) {
            if (wsClient != null &&
               (wsClient.State == WebSocketState.Open || wsClient.State == WebSocketState.Connecting)) {
                await DisconnectAsync();
            }

            try {
                wsClient = new ClientWebSocket();
                Debug.Log($"<color=white>[CoPlayer Socket]</color> Connect to {ServerUri}");
                await wsClient.ConnectAsync(new Uri(ServerUri), cancellationToken);

                IsConnected = true;
                OnConnectionStatusChanged?.Invoke(true);
                Debug.Log("<color=white>[CoPlayer Socket]</color> Connect Successfully.");

                lastHeartbeatSendTime = DateTime.Now.Ticks;
                lastHeartbeatReceiveTime = DateTime.Now.Ticks;

                await ReceiveLoop(cancellationToken);
            } catch (WebSocketException ex) {
                Debug.LogError($"<color=red>[CoPlayer Socket]</color> WebSocket Connect Error(msg): {ex.Message}");
                //Debug.LogError($"<color=red>[CoPlayer Socket]</color> WebSocket Connect Error: {ex.ToString()}\nServerUri: {ServerUri}");
                //if (ex.InnerException != null) {
                //    Debug.LogError($"<color=red>[CoPlayer Socket]</color> InnerException: {ex.InnerException}");
                //}
                HandleConnectionFailure();
            } catch (OperationCanceledException) {
                Debug.Log("<color=red>[CoPlayer Socket]</color> WebSocket Connect be Cannceled.");
                HandleConnectionFailure();
            } catch (Exception ex) {
                Debug.LogError($"<color=red>[CoPlayer Socket]</color> WebSocket Error(msg): {ex.Message}");
                //Debug.LogError($"<color=red>[CoPlayer Socket]</color> WebSocket Error: {ex.ToString()}\nServerUri: {ServerUri}");
                //if (ex.InnerException != null) {
                //    Debug.LogError($"<color=red>[CoPlayer Socket]</color> InnerException: {ex.InnerException}");
                //}
                HandleConnectionFailure();
            }


        }

        private async Task ReceiveLoop(CancellationToken cancellationToken) {
            //byte[] buffer = new byte[8192]; // 8 KB
            byte[] buffer = new byte[65536]; // 64 KB
            ArraySegment<byte> bufferSegment = new ArraySegment<byte>(buffer);
            const int maxMessageSize = 1024 * 512; // 512 KB

            while (wsClient != null && wsClient.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested) {
                try {
                    // !!** Adjusted: Support Fragmented WebSocket Frames
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult result = null;

                    // read until EndOfMessage to support fragmented WebSocket frames
                    do
                    {
                        //result = await wsClient.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        result = await wsClient.ReceiveAsync(bufferSegment, cancellationToken);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Debug.LogWarning($"<color=white>[CoPlayer Socket]</color> WebSocket Server Closed. Status: {result.CloseStatus} - {result.CloseStatusDescription}");
                            HandleConnectionFailure();
                            //return;
                            break;
                        }

                        // protect against runaway large messages (adjust max as needed): all in Bytes
                        if (ms.Length + result.Count > maxMessageSize)
                        {
                            Debug.LogError($"<color=red>[CoPlayer Socket]</color> Incoming message exceeded max allowed size ({ms.Length + result.Count} bytes). Dropping and closing.");
                            HandleConnectionFailure();
                            //return;
                            break;
                        }

                        if (result.Count > 0)
                        {
                            ms.Write(buffer, 0, result.Count);
                        }
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(ms.ToArray());
                        //string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        lock (messageQueueLock)
                        {
                            receivedMessages.Enqueue(message);
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        Debug.LogWarning("<color=white>[CoPlayer Socket]</color> Received Binary Data (not handled).");
                        // Optionally handle binary (e.g. length-prefixed JSON or MessagePack) here
                    }



                    //WebSocketReceiveResult result = await wsClient.ReceiveAsync(bufferSegment, cancellationToken);
                    //if (result.MessageType == WebSocketMessageType.Text) {
                    //    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    //    lock (messageQueueLock) {
                    //        receivedMessages.Enqueue(message);
                    //    }
                    //} else if (result.MessageType == WebSocketMessageType.Close) {
                    //    Debug.LogWarning($"<color=white>[CoPlayer Socket]</color> WebSocket Server Closed. Status: {result.CloseStatus} - {result.CloseStatusDescription}");
                    //    HandleConnectionFailure();
                    //    break;
                    //} else if (result.MessageType == WebSocketMessageType.Binary) {
                    //    Debug.LogWarning("<color=white>[CoPlayer Socket]</color> Recevied Binary Data.");
                    //}

                }
                catch (WebSocketException ex) {
                    Debug.LogError($"<color=red>[CoPlayer Socket]</color> WebSocket Recv. Error: {ex.Message}");
                    HandleConnectionFailure();
                    break;
                } catch (OperationCanceledException) {
                    Debug.Log("<color=red>[CoPlayer Socket]</color> WebSocket Recv. Op. Canceled.");
                    break;
                } catch (Exception ex) {
                    Debug.LogError($"<color=red>[CoPlayer Socket]</color> WebSocket Error: {ex.Message}");
                    HandleConnectionFailure();
                    break;
                }
            }
            Debug.Log("<color=white>[CoPlayer Socket]</color> WebSocket Stop Recv. Loop.");
        }

        // Replace ReceiveLoop with this implementation (ensure you added 'using System.IO;' at top)
        //private async Task ReceiveLoop(CancellationToken cancellationToken)
        //{
        //    byte[] buffer = new byte[8192];

        //    while (wsClient != null && wsClient.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        //    {
        //        try
        //        {
        //            using var ms = new MemoryStream();
        //            WebSocketReceiveResult result = null;

        //            // read until EndOfMessage to support fragmented WebSocket frames
        //            do
        //            {
        //                result = await wsClient.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

        //                if (result.MessageType == WebSocketMessageType.Close)
        //                {
        //                    Debug.LogWarning($"<color=white>[CoPlayer Socket]</color> WebSocket Server Closed. Status: {result.CloseStatus} - {result.CloseStatusDescription}");
        //                    HandleConnectionFailure();
        //                    return;
        //                }

        //                // protect against runaway large messages (adjust max as needed)
        //                const int maxMessageSize = 1024 * 1024; // 1 MB
        //                if (ms.Length + result.Count > maxMessageSize)
        //                {
        //                    Debug.LogError($"<color=red>[CoPlayer Socket]</color> Incoming message exceeded max allowed size ({ms.Length + result.Count} bytes). Dropping and closing.");
        //                    HandleConnectionFailure();
        //                    return;
        //                }

        //                if (result.Count > 0)
        //                {
        //                    ms.Write(buffer, 0, result.Count);
        //                }
        //            } while (!result.EndOfMessage);

        //            if (result.MessageType == WebSocketMessageType.Text)
        //            {
        //                string message = Encoding.UTF8.GetString(ms.ToArray());
        //                lock (messageQueueLock)
        //                {
        //                    receivedMessages.Enqueue(message);
        //                }
        //            }
        //            else if (result.MessageType == WebSocketMessageType.Binary)
        //            {
        //                Debug.LogWarning("<color=white>[CoPlayer Socket]</color> Received Binary Data (not handled).");
        //                // Optionally handle binary (e.g. length-prefixed JSON or MessagePack) here
        //            }
        //        }
        //        catch (WebSocketException ex)
        //        {
        //            Debug.LogError($"<color=red>[CoPlayer Socket]</color> WebSocket Recv. Error: {ex.Message}");
        //            HandleConnectionFailure();
        //            break;
        //        }
        //        catch (OperationCanceledException)
        //        {
        //            Debug.Log("<color=red>[CoPlayer Socket]</color> WebSocket Recv. Op. Canceled.");
        //            break;
        //        }
        //        catch (Exception ex)
        //        {
        //            Debug.LogError($"<color=red>[CoPlayer Socket]</color> WebSocket Error: {ex.Message}");
        //            HandleConnectionFailure();
        //            break;
        //        }
        //    }
        //    Debug.Log("<color=white>[CoPlayer Socket]</color> WebSocket Stop Recv. Loop.");
        //}


        private void HandleConnectionFailure() {
            if (IsConnected) {
                IsConnected = false;
                OnConnectionStatusChanged?.Invoke(false);
            }

            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;

            Invoke(nameof(Reconnect), reconnectInterval);
        }

        public async void SendData(string message) {
            Debug.LogWarning("<color=white>[CoPlayer Socket]</color> within SendData!!!!");
            if (!IsConnected || wsClient == null || wsClient.State != WebSocketState.Open) {
                Debug.LogWarning("<color=white>[CoPlayer Socket]</color> No Connection.");
                return;
            }

            try {
                byte[] bytesToSend = Encoding.UTF8.GetBytes(message);
                ArraySegment<byte> bufferSegment = new ArraySegment<byte>(bytesToSend);

                await wsClient.SendAsync(bufferSegment, WebSocketMessageType.Text, true, CancellationToken.None);
                 Debug.Log("<color=white>[CoPlayer Socket]</color> Send: " + message);
            } catch (WebSocketException ex) {
                Debug.LogError("<color=white>[CoPlayer Socket]</color> Send Error: " + ex.Message);
                HandleConnectionFailure();
            } catch (Exception ex) {
                Debug.LogError("<color=white>[CoPlayer Socket]</color> Error: " + ex.Message);
            }
        }

        public async void Disconnect() {
            await DisconnectAsync();
        }

        private async Task DisconnectAsync() {
            if (wsClient == null) return;

            IsConnected = false;
            OnConnectionStatusChanged?.Invoke(false);

            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;

            if (wsClient.State == WebSocketState.Open || wsClient.State == WebSocketState.Connecting) {
                try {
                    await wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client initiated close", CancellationToken.None);
                } catch (Exception ex) {
                    Debug.LogError($"<color=red>[CoPlayer Socket]</color> Close WebSocket Error: {ex.Message}");
                }
            }
            
            wsClient.Dispose();
            wsClient = null;

            Debug.Log("<color=white>[CoPlayer Socket]</color> WebSocket Disconnected.");
            CancelInvoke(nameof(Reconnect));
        }

        private void Reconnect() {
            if (!IsConnected) {
                Debug.Log("<color=white>[CoPlayer Socket]</color> Try to Reconnect...");
                CancelInvoke(nameof(Reconnect));
                StartClient();
            }
        }

#endregion

    }
}
