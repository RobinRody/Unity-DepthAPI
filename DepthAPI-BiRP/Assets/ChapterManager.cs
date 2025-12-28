//using SharpDX.DirectSound;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MultiPlayer
{
    public class ChapterManager : MonoBehaviour
    {
        //[Header("CoPlayerSocketMgr")]
        //CoPlayerSocketMgr CoPlayerSocketMgr;
        //private string deviceShortID => CoPlayerSocketMgr.DeviceShortID;
        private string deviceshortID => CoPlayerSocketMgr.Instance?.DeviceShortID ?? "";

        [Header("General UI Setting")]
        [Tooltip("should be false, only be true if want to show differen hint for Even/Odd JankenRound")]
        public bool setJankenEvenOddHint = false;


        [Header("GameObject")]
        public GameObject redman;
        public GameObject ovrHandRight;
        public GameObject ovrHandLeft;
        private OVRMeshRenderer ovrHandMeshRight;
        private OVRMeshRenderer ovrHandMeshLeft;
        private SkinnedMeshRenderer ovrHandSkinnedMeshRight;
        private SkinnedMeshRenderer ovrHandSkinnedMeshLeft;
        [Header("AvatarSelf Color adjusted")]
        //public AvatarScaler avatarScalerForColorChanging;
        [Header("UICircle")]
        public GameObject standbyCircleAndRoute;
        public GameObject standbyRoute;
        public GameObject jankenAreaOdd;
        public GameObject jankenAreaEven;

        [Header("UIHint")]
        public GameObject[] puzzleUI;
        public GameObject[][] puzzleSubjectUI;
        public GameObject[] puzzleSubjectUI1;
        public GameObject[] puzzleSubjectUI2;
        public GameObject[] puzzleSubjectUI3;
        public GameObject[] puzzleSubjectUI4;
        public GameObject[] jankenWinUI;
        public GameObject[] jankenUIHintEven;
        public GameObject[] jankenUIHintOdd;
        //public GameObject puzzleUI;
        //public GameObject[] puzzleSubjectUI;
        //public GameObject jankenWinUI;
        //public GameObject jankenUIHintEven;
        //public GameObject jankenUIHintOdd;

        [Header("Runtime State (Read-Only)")]
        [SerializeField] private StateData state = new StateData();

        [Space]
        [Header("Other Players")]
        public GameObject PlayerVisualPrefab;
        public Color highlightColor = new Color(0.349f, 0.824f, 0.0f); // #59D200
        public Color normalColor = new Color(0.263f, 0.263f, 0.263f);  // #434343
        //!!++ Other Players Data: string "deviceShortID"
        private Dictionary<string, GameObject> otherPlayers = new Dictionary<string, GameObject>();
        private Dictionary<string, DateTime> lastReceivedTimePerPlayer = new Dictionary<string, DateTime>();

        [Serializable]
        private class StateData
        {
            [Header("Chapter Control")]
            [ReadOnly] public int roomChapter;
            //public int roomChapter;
            //[ReadOnly] public bool isInCircle;  // player(local-control) ⇒ server
            public bool isInCircle;  // player(local-control) ⇒ server

            [Header("Timer Control")]
            [ReadOnly] public bool isTimerActive;
            [ReadOnly] public Puzzle puzzle;
            [ReadOnly] public int jankenRound;
            //public bool isTimerActive;
            //public Puzzle puzzle;
            //public int jankenRound;

            [Header("Janken State")]
            //[ReadOnly] public int jankenWin;    // player(local-control) ⇒ server
            //[ReadOnly] public bool doneJanken;  // player(local-control) ⇒ server
            public int jankenWin;    // player(local-control) ⇒ server
            public bool doneJanken;  // player(local-control) ⇒ server

            public int oldJankenRound;          // player(local) only
            public int oldCircleIdx;            // player(local) only
            public int neoCircleIdx;            // player(local) only
            public bool hasAddWinThisRound; // player(local) only


            [Header("Display State")]
            [ReadOnly] public bool showRedman;
            [ReadOnly] public bool showOVRHand;
            //public bool showRedman;
            //public bool showOVRHand;

            [Header("MISC")]
            [ReadOnly] public int yourSeq;
            [ReadOnly] public bool toReset;
        }

        #region State-Public Accessors
        // Public accessors
        public int RoomChapter { get => state.roomChapter; set => state.roomChapter = value; }
        public bool IsInCircle { get => state.isInCircle; set => state.isInCircle = value; }
        public bool IsTimerActive { get => state.isTimerActive; set => state.isTimerActive = value; }
        public Puzzle Puzzle { get => state.puzzle; set => state.puzzle = value; }
        public int JankenRound { get => state.jankenRound; set => state.jankenRound = value; }
        public int JankenWin { get => state.jankenWin; set => state.jankenWin = value; }
        public bool DoneJanken { get => state.doneJanken; set => state.doneJanken = value; }
        public bool ShowRedman { get => state.showRedman; set => state.showRedman = value; }
        public bool ShowOVRHand { get => state.showOVRHand; set => state.showOVRHand = value; }
        public int YourSeq { get => state.yourSeq; set => state.yourSeq = value; }
        public bool ToReset { get => state.toReset; set => state.toReset = value; }

        // MISC: local-only Janken-related
        public int OldJankenRound { get => state.oldJankenRound; set => state.oldJankenRound = value; }
        public int OldCircleIdx { get => state.oldCircleIdx; set => state.oldCircleIdx = value; }
        public int NeoCircleIdx { get => state.neoCircleIdx; set => state.neoCircleIdx = value; }
        public bool HasAddWinThisRound { get => state.hasAddWinThisRound; set => state.hasAddWinThisRound = value; }
        #endregion

        void ResetLocalAttributes() // only Reset if ChapterSet from Server
        {
            // Initialization: Chapter Control
            //RoomChapter = 0;
            //IsInCircle = false;     // player(local-control) ⇒ server

            // Initialization: Timer Control
            //IsTimerActive = false;
            //Puzzle = new Puzzle();
            //JankenRound = 0;
            
            // Initialization: Janken
            JankenWin = 0;              // player(local-control) ⇒ server
            DoneJanken = true;          // player(local-control) ⇒ server
            OldJankenRound = 0;         //player(local) only
            OldCircleIdx = 0;           //player(local) only
            //NeoCircleIdx = 0;         //player(local) only
            HasAddWinThisRound = false; //player(local) only

            // Initialization: Display State
            //ShowRedman = false;
            //ShowOVRHand = false;

            // MISC
            //YourSeq = -1;
            //ToReset = false;
        }
        void Start()
        {
            // Initialization: Chapter Control
            RoomChapter = 0;
            IsInCircle = false;     // player(local-control) ⇒ server
            // Initialization: Timer Control
            IsTimerActive = false;
            Puzzle = new Puzzle();
            JankenRound = 0;
            // Initialization: Janken
            JankenWin = 0;          // player(local-control) ⇒ server
            DoneJanken = true;     // player(local-control) ⇒ server
            // Initialization: Display State
            ShowRedman = false;
            ShowOVRHand = false;
            // MISC
            YourSeq = -1;
            ToReset = false;

            puzzleSubjectUI = new GameObject[4][]
            {
                puzzleSubjectUI1,
                puzzleSubjectUI2,
                puzzleSubjectUI3,
                puzzleSubjectUI4,
            };

            // Cache OVRHand Mesh Renderer
            ovrHandMeshRight = ovrHandRight.GetComponent<OVRMeshRenderer>();
            ovrHandMeshLeft = ovrHandLeft.GetComponent<OVRMeshRenderer>();
            ovrHandSkinnedMeshRight = ovrHandRight.GetComponent<SkinnedMeshRenderer>();
            ovrHandSkinnedMeshLeft = ovrHandLeft.GetComponent<SkinnedMeshRenderer>();


            OldJankenRound = 0;
            OldCircleIdx = 0;
            NeoCircleIdx = 0;
            HasAddWinThisRound = false;

            if (puzzleSubjectUI.Length != 25)
            {
                Debug.LogWarning("<color=red>[ChapterMgr]</color> puzzleSubjectUI is not enough(25)");
            }
        }

        private void FixedUpdate()
        {
            // Check Other Players Timeout
            CheckOtherPlayersTimeout();
        }

        void Update()
        {
            // Display(Redman, OVRHand) Logic
            {
                if (redman == null)
                {
                    Debug.LogWarning("<color=yellow>[ChapterMgr]</color> Can't find Redman");
                    redman = GameObject.Find("Redman");
                    if (redman == null)
                    {
                        Debug.LogWarning("<color=red>[ChapterMgr]</color> Still can't find Redman");
                        return;
                    }
                }
                if (ShowRedman)
                    redman.SetActive(true);
                else
                    redman.SetActive(false);


                if (ovrHandRight == null)
                {
                    Debug.LogWarning("<color=yellow>[ChapterMgr]</color> Can't find OVRHandRight");
                    ovrHandRight = GameObject.Find("[BuildingBlock] Hand Tracking right");
                    if (ovrHandRight == null)
                    {
                        Debug.LogWarning("<color=red>[ChapterMgr]</color> Still can't find OVRHandRight");
                        return;
                    }
                }
                if (ovrHandLeft == null)
                {
                    Debug.LogWarning("<color=yellow>[ChapterMgr]</color> Can't find OVRHandLeft");
                    ovrHandLeft = GameObject.Find("[BuildingBlock] Hand Tracking left");
                    if (ovrHandLeft == null)
                    {
                        Debug.LogWarning("<color=red>[ChapterMgr]</color> Still can't find OVRHandLeft");
                        return;
                    }
                }
                if (ShowOVRHand)
                {
                    ovrHandMeshRight.enabled = true;
                    ovrHandMeshLeft.enabled = true;
                    ovrHandSkinnedMeshRight.enabled = true;
                    ovrHandSkinnedMeshLeft.enabled = true;
                }
                else
                {
                    //ovrHandRight.SetActive(false);
                    //ovrHandLeft.SetActive(false);
                    ovrHandMeshRight.enabled = false;
                    ovrHandMeshLeft.enabled = false;
                    ovrHandSkinnedMeshRight.enabled = false;
                    ovrHandSkinnedMeshLeft.enabled = false;
                }
            }
            // Chapter Logic
            {
                // Could be problematic 
                if (ToReset)
                {
                    ResetLocalAttributes();
                    //// Reset all Display UI
                    //ShowStandbyCircle(false);
                    //ResetJankenUI();
                    //ResetPuzzleUI();
                    //// Reset State Variables
                    ////RoomChapter = 0;          // Server-controlled
                    ////IsInCircle = false;       // player(local-control) ⇒ server; but should be set by Collider
                    ////IsTimerActive = false;    // Server-controlled
                    ////Puzzle = new Puzzle();    // Server-controlled
                    ////JankenRound = 0;          // Server-controlled
                    //JankenWin = 0;              // player(local-control) ⇒ server
                    //DoneJanken = true;          // player(local-control) ⇒ server
                    //oldJankenRound = 0;        // local purely
                    //oldCircleIdx = 0;           // local purely
                    ////neoCircleIdx = 0;         // local purely; but should be set by Collider
                    //hasAddWinThisRound = false; // local purely

                    //ToReset = false;            // Server-controlled; but this flag should clear immediately after reset
                    //return;
                }

                if (RoomChapter < 1 || RoomChapter > 2)
                {

                    ShowStandbyCircle(false);
                    ResetJankenUI();
                    ResetPuzzleUI();

                    //ResetLocalAttributes(); //* could be problematic
                    return;
                }

                //*** Display A. Chap 1 or 2：show StandbyCircles
                ShowStandbyCircle(true);
                if (YourSeq < 0)
                {
                    Debug.LogWarning("<color=red>[ChapterMgr]</color> YourSeq is not assigned yet!");
                    ResetJankenUI();
                    ResetPuzzleUI();
                    return;
                }

                if (!IsTimerActive)
                {
                    //ResetLocalAttributes();
                    ResetJankenUI();
                    ResetPuzzleUI();
                    return;
                }
                //*** Display B. Chap 1 w. Puzzle: show Puzzle UI if self is the player, or change other's color
                if (RoomChapter == 1)       // Chapter1(Puzzle) logic
                {
                    // 0. reset all JanKen-related(Ch.2) UI if it's displayed
                    ResetJankenUI();
                    // 1. make sure Puzzle/Timer is assigned, or reset all Display then return
                    if (Puzzle == null || (Puzzle.puzzle_subject == 0 || string.IsNullOrEmpty(Puzzle.puzzle_player)))
                    //if (Puzzle == null || (Puzzle.puzzle_subject == 0 || Puzzle.puzzle_player == ""))
                    //if (Puzzle == null || (Puzzle.puzzle_subject == 0 || Puzzle.puzzle_player == -1))
                    {
                        Debug.LogWarning("<color=red>[ChapterMgr]</color> Puzzle is not assigned yet!");
                        ResetPuzzleUI();
                        return;
                    }

                    Debug.LogWarning($"<color=green>[ChapterMgr]</color> Puzzle Subject: {Puzzle.puzzle_subject}, Puzzle Player: {Puzzle.puzzle_player}\ndeviceshortID = {deviceshortID}");
                    // 2. if puzzlePlayer is self, show the puzzle UI
                    if (Puzzle.puzzle_player == deviceshortID)
                    //if (Puzzle.puzzle_player == YourSeq)
                    {

                        ShowPuzzleUI(true);
                        ShowPuzzleSubjectUI(true, Puzzle.puzzle_subject);

                        ChangeOtherColor(false, "");
                        //ChangeOtherColor(false, -1);
                        //avatarScalerForColorChanging.ChangeSelfColor(true);
                    }
                    // 3. or if puzzlePlayer is others, change the Player's color
                    else
                    {
                        ChangeOtherColor(true, Puzzle.puzzle_player);
                        //avatarScalerForColorChanging.ChangeSelfColor(false);

                        ShowPuzzleSubjectUI(false, 0);
                        ShowPuzzleUI(false);
                    }



                    //***   Other Factors@ Collider: IsInCircle                                     ***//
                    //1. (check always @ Collider In/Out) update "IsInCircle" according to player's position

                    //***   Other Factors@ Server: IsTimerActive                                    ***//
                    //1. (Server) start Puzzle/Timer is All are InCircle

                }

                //*** Display C. Chap 2 w. Janken: show Janken UI according to JankenRound, or Standby UI if DoneJanken
                else if (RoomChapter == 2)  //Chapter2(Janken) logic
                {
                    //0. reset all Puzzle-related(Ch.1) UI if it's displayed
                    ResetPuzzleUI();
                    ShowJankenWinUI(true);
                    //(1. make sure (IsTimerActive), or reset all Display then return)
                    //2. (check once) New-Round started, if JankenRound is updated from Server:
                    if (OldJankenRound != JankenRound)
                    {
                        Debug.LogWarning($"<color=green>[ChapterMgr]</color> New Janken Round Started: \nOldJankenRound {OldJankenRound} -> JankenRound {JankenRound}");
                        // 2-1. reset DoneJanken (IsInCircle is not set manually)
                        OldJankenRound = JankenRound;
                        OldCircleIdx = NeoCircleIdx;
                        DoneJanken = false;
                        HasAddWinThisRound = false;

                        //ShowJankenEvenOdd(false, true);   // Hide Janken(Area, HintUI) Even
                        //ShowJankenEvenOdd(false, false);  // Hide Janken(Area, HintUI) Odd
                        
                        //ShowStandbyRoute(true);          // Show Standby Route again

                        // ✖️2-2. update JankenWin from last round (Collider 1-1)
                        //if (DoneJanken && IsInCircle)
                        //{
                        //    JankenWin = neoJankenWin;
                        //}
                    }
                    //3. display the corresponding UI for the JankenRound
                    // 3-1. if (DoneJanken && IsInCircle)
                    if ((DoneJanken && IsInCircle) && (!HasAddWinThisRound))
                    {
                        // update the JankenWin directly
                        // ✖️keep the toUpdated "neoJankenWin" for next round
                        JankenWin += CalculateWin();

                        // show standby UI: the player is ready for the next Janken
                        //ResetJankenUI();
                        //ShowStandbyCircle();

                        //ShowJankenWinUI(false);             // Hide JankenWin UI
                        ShowJankenEvenOdd(false, true);     // Hide Janken(Area, HintUI) Even
                        ShowJankenEvenOdd(false, false);    // Hide Janken(Area, HintUI) Odd

                        ShowStandbyRoute(true);             // Show Standby Route again
                        //ShowStandbyCircle(true);


                        HasAddWinThisRound = true;

                    }
                    // 3-2. or show the Janken UI(even/odd) 
                    else //!! not sure if this condition is considered correct
                    {
                        ShowJankenEvenOdd(true, JankenRound % 2 == 0);
                        ShowJankenEvenOdd(false, JankenRound % 2 != 0);

                        ShowStandbyRoute(false);          // Hide Standby Route
                        //ShowStandbyCircle(false);
                    }


                    //***   Other Factors@ Collider: IsInCircle & DoneJanken, JankenWin/JankenRound ***//
                    //1. (check always @ Collider In/Out) update "IsInCircle" according to player's position
                    // 1-1. if (DoneJanken && IsInCircle), keep the toUpdated "JankenWin" for next round
                    //2. (check always @ Collider In) update "DoneJanken" if he's in JankenCircle, only reset if new-round started

                    //***   Other Factors@ Server: JankenRound, IsTimerActive                       ***//
                    // 1. (Server) start Janken/Timer if All are InCircle
                    // 2. (Server) return JanKenRound according to IsInCircle & DoneJanken

                }
            }
        }


        //!!*** Helper Functions: ChapterDisplay -- Basic ***!!
        private void ShowStandbyCircle(bool toShow)
        {
            standbyCircleAndRoute.SetActive(toShow);
            //if (toShow)
            //{
            //    standbyCircleAndRoute.SetActive(true);
            //}
            //else
            //{
            //    standbyCircleAndRoute.SetActive(false);
            //}
        }
        private void ShowStandbyRoute(bool toShow)
        {
            standbyRoute.SetActive(toShow);
        }
        private void ResetPuzzleUI()
        {
            ChangeOtherColor(false, "");
            //ChangeOtherColor(false, -1);
            ShowPuzzleUI(false);
            ShowPuzzleSubjectUI(false, 0);

            //avatarScalerForColorChanging.ChangeSelfColor(false);
        }
        private void ResetJankenUI()
        {
            //jankenUIHintEven.SetActive(false);
            //jankenUIHintOdd.SetActive(false);
            ShowJankenWinUI(false);             // Hide JankenWin UI
            ShowJankenEvenOdd(false, true);     // Hide Janken(Area, HintUI) Even
            ShowJankenEvenOdd(false, false);    // Hide Janken(Area, HintUI) Odd

            ShowStandbyRoute(true);             // Show Standby Route again
        }



        //!!*** Helper Functions: ChapterDisplay -- Puzzle ***!!
        private void ShowPuzzleUI(bool toShow)
        {
            if (toShow)
            {
                foreach (GameObject puzzleUIelement in puzzleUI)
                {
                    puzzleUIelement.SetActive(true);
                }
            }
            else
            {
                foreach (GameObject puzzleUIelement in puzzleUI)
                {
                    puzzleUIelement.SetActive(false);
                }
                //puzzleUI.SetActive(false);
            }
        }
        private void ShowPuzzleSubjectUI(bool toShow, int puzzleSubject)
        {
            foreach (GameObject[] puzzleSubjectUIelement in puzzleSubjectUI) { 
                if (puzzleSubjectUIelement.Length != 25)
                {
                    Debug.LogWarning("<color=red>[ChapterMgr]</color> puzzleSubjectUI is not enough(25)");
                }
            }
            // Reset all the other puzzleSubjectUI first
            ResetPuzzleUIAll();
            if (puzzleSubject == 0) return;
            // Then show the corresponding puzzleSubjectUI
            foreach (GameObject[] puzzleSubjectUIelement in puzzleSubjectUI)
            {
                puzzleSubjectUIelement[puzzleSubject - 1].SetActive(toShow);
            }

        }
        private void ResetPuzzleUIAll()
        {
            foreach (GameObject[] puzzleSubjectUIelement  in puzzleSubjectUI)
            {
                if (puzzleSubjectUIelement.Length != 25)
                {
                    Debug.LogWarning("<color=red>[ChapterMgr]</color> puzzleSubjectUI is not enough(25)");
                }
                for (int i = 0; i < puzzleSubjectUIelement.Length; i++)
                {
                    puzzleSubjectUIelement[i].SetActive(false);
                }
            }
               
        }


        //!!*** Helper Functions: ChapterDisplay -- Puzzle: Avatar Color Changer ***!!
        private void ChangeOtherColor(bool toChange, string deviceID)
        {
            // Reset all other players' color first
            ResetOtherColorAll();
            if (string.IsNullOrEmpty(deviceID)) return;

            // Then change the corresponding player's color
            if (!otherPlayers.ContainsKey(deviceID))
            {
                Debug.LogWarning($"<color=red>[ChapterMgr]</color> Player {deviceID} not found in otherPlayers");
                return;
            }

            GameObject playerObj = otherPlayers[deviceID];
            if (playerObj == null)
            {
                Debug.LogWarning($"<color=red>[ChapterMgr]</color> Player {deviceID} GameObject is null");
                return;
            }

            SetPlayerColor(toChange, playerObj);
            
        }
        private void ResetOtherColorAll()
        {
            foreach (var kvp in otherPlayers)
            {
                string deviceId = kvp.Key;
                GameObject playerObj = kvp.Value;
                if (playerObj == null)
                {
                    Debug.LogWarning($"<color=red>[ChapterMgr]</color> Player {deviceId} GameObject is null");
                    continue;
                }
                SetPlayerColor(false, playerObj);
            }
        }
        private void SetPlayerColor(bool highlight, GameObject playerObj)
        {
            // 顏色定義
            //Color highlightColor = new Color(0.349f, 0.824f, 0.0f); // #59D200
            //Color normalColor = new Color(0.263f, 0.263f, 0.263f);  // #434343

            Color targetColor = highlight ? highlightColor : normalColor;

            // 方法 1：使用 SkinnedMeshRenderer（如果 Avatar 使用骨骼動畫）
            SkinnedMeshRenderer skinnedRenderer = playerObj.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skinnedRenderer != null)
            {
                // 產生材質實例（避免影響 Prefab）
                skinnedRenderer.material.color = targetColor;
                Debug.Log($"<color=green>[ChapterMgr]</color> Changed SkinnedMeshRenderer color to {(highlight ? "highlight" : "normal")}");
                return;
            }

            // 方法 2：使用一般 Renderer（如果沒有骨骼動畫）
            Renderer[] renderers = playerObj.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                foreach (var renderer in renderers)
                {
                    renderer.material.color = targetColor;
                }
                Debug.Log($"<color=green>[ChapterMgr]</color> Changed {renderers.Length} Renderer(s) color to {(highlight ? "highlight" : "normal")}");
                return;
            }

            //// 方法 3：使用 Transparency 腳本（如果已經掛載）
            //Transparency transparency = playerObj.GetComponent<Transparency>();
            //if (transparency != null)
            //{
            //    transparency.SetTransparency(highlight ? 1.0f : 0.5f);
            //    Debug.Log($"<color=green>[ChapterMgr]</color> Changed Transparency to {(highlight ? "1.0" : "0.5")}");
            //    return;
            //}

            Debug.LogWarning($"<color=red>[ChapterMgr]</color> No Renderer found on player {playerObj.name}");
        }


        //!!*** Helper Functions: ChapterDisplay -- Janken ***!!
        private void ShowJankenWinUI(bool toShow)
        {
            if (!toShow)
            {
                foreach (GameObject jankenWinUIelement in jankenWinUI)
                {
                    jankenWinUIelement.SetActive(false);
                }
                //jankenWinUI.SetActive(false);
            }
            else
            {
                foreach (GameObject jankenWinUIelement in jankenWinUI)
                {
                    jankenWinUIelement.SetActive(true);
                }
                //jankenWinUI.SetActive(true);
            }
        }
        private void ShowJankenEvenOdd(bool toShow, bool isEven) // Area and HintUI
        {

            if (isEven) // JankenRound = 0,2,4,...
            {
                jankenAreaEven.SetActive(toShow);

                if (!setJankenEvenOddHint) return;
                foreach (GameObject jankenUIHintEvenelement in jankenUIHintEven)
                {
                    jankenUIHintEvenelement.SetActive(toShow);
                }

                //if (toShow)
                //{
                //    jankenAreaEven.SetActive(true);

                //    if (!setJankenEvenOddHint) return;
                //    foreach (GameObject jankenUIHintEvenelement in jankenUIHintEven)
                //    {
                //        jankenUIHintEvenelement.SetActive(true);
                //    }
                //    //jankenUIHintEven.SetActive(true);
                //}
                //else
                {
                    jankenAreaEven.SetActive(false);

                    if (!setJankenEvenOddHint) return;
                    foreach (GameObject jankenUIHintEvenelement in jankenUIHintEven)
                    {
                        jankenUIHintEvenelement.SetActive(false);
                    }
                    //jankenUIHintEven.SetActive(false);
                }
                    
            }
            else        // JankenRound = 1,3,5,...
            {
                jankenAreaOdd.SetActive(toShow);

                if (!setJankenEvenOddHint) return;
                foreach (GameObject jankenUIHintOddelement in jankenUIHintOdd)
                {
                    jankenUIHintOddelement.SetActive(toShow);
                }
                //if (toShow)
                //{
                //    jankenAreaOdd.SetActive(true);

                //    if (!setJankenEvenOddHint) return;
                //    foreach (GameObject jankenUIHintOddelement in jankenUIHintOdd)
                //    {
                //        jankenUIHintOddelement.SetActive(true);
                //    }
                //    //jankenUIHintOdd.SetActive(true);
                //}

                //else
                //{
                //    jankenAreaOdd.SetActive(false);

                //    if (!setJankenEvenOddHint) return;
                //    foreach (GameObject jankenUIHintOddelement in jankenUIHintOdd)
                //    {
                //        jankenUIHintOddelement.SetActive(false);
                //    }
                //    //jankenUIHintOdd.SetActive(false);
                //}

            }
        }

        //!!*** Helper Functions: ChapterDisplay -- Janken Win Calculation ***!!
        private int CalculateWin()
        {
            int diff = NeoCircleIdx - OldCircleIdx;
            if (diff < -2) diff += 4;
            else if (diff > 2) diff -= 4;
            Debug.LogError($"<color=green>[ChapterMgr>CalculateWin]</color> oldCircleIdx: {OldCircleIdx}, neoCircleIdx: {NeoCircleIdx} => diff: {diff}");
            //if (oldCircleIdx == 0)
            //{
            //    Debug.LogWarning("<color=red>[ChapterMgr>Janken]</color> oldCircleIdx is not assigned yet!");
            //    return 0;
            //}

            //  Even Round
            if (JankenRound % 2 == 0) 
            {
                if (OldCircleIdx == 2 || OldCircleIdx == 4)
                {
                    if (diff == 1)//(neoCircleIdx - oldCircleIdx == 1 || neoCircleIdx - oldCircleIdx == -3)
                    {
                        return 1;   // win
                    }
                    else if (diff == 0)
                    {
                        return 0;   // lose
                    }
                }
                else // oldCircleIdx == 1 || oldCircleIdx == 3
                {
                    if (diff == -1)//(NeoCircleIdx - OldCircleIdx == -1 || NeoCircleIdx - OldCircleIdx == 3)
                    {
                        return 0;   // lose
                    }
                    else if (diff == 0)
                    {
                        return 1;   // win
                    }
                }
            }
            //  Odd Round
            else 
            {
                if (OldCircleIdx == 1 || OldCircleIdx == 3)
                {
                    if (diff == 1)
                    //if (neoCircleIdx - oldCircleIdx == 1 || neoCircleIdx - oldCircleIdx == -3)
                    {
                        return 1;   // win
                    }
                    else if (diff == 0)
                    {
                        return 0;   // lose
                    }
                }
                else // oldCircleIdx == 2 || oldCircleIdx == 4
                {
                    if (diff == -1)
                    //if (neoCircleIdx - oldCircleIdx == -1 || neoCircleIdx - oldCircleIdx == 3)
                    {
                        return 0;   // lose
                    }
                    else if (diff == 0)
                    {
                        return 1;   // win
                    }
                }
            }

            Debug.LogWarning($"<color=red>[ChapterMgr>CalculateWin]</color> Error: oldCircleIdx: {OldCircleIdx}, neoCircleIdx: {NeoCircleIdx} => diff: {diff}");
            return 0;
        }







        #region Display-related: Animate Other Avatars, DealWithChapter
        public void AnimateOtherAvatars(PositionInfo positionInfo, string deviceShortID)
        {
            Debug.LogWarning($"<color=red>[ChapterManager>AnimateOther]</color> Animate Other Avatars Called. deviceShortID self: {deviceShortID}");
            foreach (var playerData in positionInfo.player_position_info)
            {
                Debug.LogWarning($"<color=red>[ChapterManager>AnimateOther]</color> Animate Other Avatar: {playerData.device_id}\ndeviceShortID self: {deviceShortID}");
                if (playerData.device_id == deviceShortID) continue; // Skip Self
                // New Player: create new avatar for Dictionary 
                if (!otherPlayers.ContainsKey(playerData.device_id))
                {
                    GameObject newPlayer = Instantiate(PlayerVisualPrefab);
                    newPlayer.name = $"Player_{playerData.device_id}";
                    otherPlayers[playerData.device_id] = newPlayer;
                }
                // Update Player Animation
                //GameObject playerObj = otherPlayers[playerData.device_id];
                GameObject scalerObj = otherPlayers[playerData.device_id];
                scalerObj.transform.localScale = Vector3.one * playerData.scale;

                //ColorChanger colorChanger = scalerObj.GetComponent<ColorChanger>();
                //colorChanger.ChangeAvatarColor();

                //GameObject playerObj = scalerObj.transform.GetChild(0).gameObject;
                //AI4Animation.Actor playerActor = playerObj.GetComponent<AI4Animation.Actor>();
                AI4Animation.Actor playerActor = scalerObj.GetComponentInChildren<AI4Animation.Actor>();
                //GameObject playerObj = playerActor.gameObject;
                if (playerActor != null)
                {
                    //playerData.ApplyToActor(playerActor);
                    // --- 套用骨骼資料 ---
                    //// --- 建立骨名對應表 ---
                    //var boneMap = new Dictionary<string, AI4Animation.Actor.Bone>();
                    //foreach (var bone in playerActor.Bones)
                    //{
                    //    boneMap[bone.GetName()] = bone;
                    //}
                    // 套用 bone_transforms
                    foreach (var boneData in playerData.bone_transforms)
                    {
                        //if (boneMap.TryGetValue(boneData.bone_name, out var bone))
                        var bone = playerActor.FindBone(boneData.bone_name);
                        if (bone != null)
                        {
                            bone.SetPosition(boneData.position);
                            bone.SetRotation(boneData.rotation);
                        }
                        else
                        {
                            Debug.LogWarning($"<color=red>[CoPlayer Socket]</color> Bone '{boneData.bone_name}' not found in Actor {playerData.device_id}.");
                        }
                    }
                    // --- 更新最後收到時間 ---
                    lastReceivedTimePerPlayer[playerData.device_id] = DateTime.Now;
                    scalerObj.SetActive(true);

                }
                else
                {
                    Debug.LogWarning("<color=red>[CoPlayer Socket]</color> Player Visual Prefab Missing Actor Component.");
                }
            }
        }
        public void CheckOtherPlayersTimeout()
        {

            //!!++Added: Check if other players are timeout (no data for 5 seconds)
            List<string> toRemove = new List<string>();
            foreach (var kvp in otherPlayers)
            {
                string deviceId = kvp.Key;
                GameObject playerObj = kvp.Value;
                if (lastReceivedTimePerPlayer.TryGetValue(deviceId, out var lastTime))
                {
                    double secondsSinceLast = (DateTime.Now - lastTime).TotalSeconds;
                    if (secondsSinceLast > 5.0)
                    {
                        // 超過5秒沒收到，視同斷線
                        playerObj.SetActive(false);

                        // 超過10秒，釋放物件
                        if (secondsSinceLast > 10.0)
                        {
                            Destroy(playerObj);
                            toRemove.Add(deviceId);
                        }
                    }
                }
                else
                {
                    // 沒有收到過資料也隱藏
                    playerObj.SetActive(false);
                }
            }
            foreach (var deviceId in toRemove)
            {
                otherPlayers.Remove(deviceId);
                lastReceivedTimePerPlayer.Remove(deviceId);
            }

        }
        #endregion

    }
}