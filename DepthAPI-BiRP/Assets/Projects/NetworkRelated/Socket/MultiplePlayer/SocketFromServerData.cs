using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;

namespace MultiPlayer {
    [System.Serializable]
    public class FromServerBasic : GM.JsonFuncBase {
        public string device_id;

        public FromServerBasic() {
            device_id = "";
        }
    }

    [System.Serializable]
    public class PositionInfo : SocketPayloadBasic {
        // Basically using HeartbeatData to present All Users
        public HeartbeatData[] player_position_info;
        public ChapterInfo chapter_info;
        public int player_count = 0;

        public PositionInfo() {
            SetupMessageType(SocketMsgType.FromServer.Update);
            player_position_info = new HeartbeatData[0];
            player_count = 0;
            chapter_info = new ChapterInfo();
        }
    }

    [System.Serializable]
    public class ChapterInfo : GM.JsonFuncBase {
        #region varialbes
        // MISC: player's seq, used to decide if the puzzle.playerSeq is yourself
        public int your_seq;          // ChapterManager.YourSeq;
        public bool to_reset;         // ChapterManager.ToReset;
        // Display State
        public bool redman;             //ChapterManager.ShowRedman;
        public bool ovrhand;            //ChapterManager.ShowOVRHand;

        // Chapter Control
        public int room_chapter;        // ChapterManager.RoomChapter;
        //public bool is_in_circle;     // ChapterManager.IsInCircle: local;

        // Timer Control
        public bool is_timer_active;    // ChapterManager.IsTimerActive;
        public Puzzle puzzle;           // ChapterManager.CurrentPuzzle;
        public int janken_round;        // ChapterManager.JankenRound;
        // // Janken State
        //public int janken_win;          // ChapterManager.JankenWin: local;
        //public bool done_janken;        // ChapterManager.DoneJanken: local;

        #endregion

        public ChapterInfo()
        {
            // MISC: player's seq, used to decide if the puzzle.playerSeq is yourself
            your_seq = -1;
            to_reset = false;
            redman = false;
            ovrhand = false;
            // Chapter Control
            room_chapter = 0;
            // Timer Control
            is_timer_active = false;
            puzzle = Puzzle.None;
            janken_round = 0;
        }
    }



    [System.Serializable]
    public class Puzzle //: GM.JsonFuncBase
    {
        public int puzzle_subject;   // id of the puzzle subject
        //public int puzzle_player;    // seq -1 means no player assigned
        public string puzzle_player;    // device_id "" means no player assigned
        //public bool puzzle_foryou; // true means the puzzle is for you

        public static readonly Puzzle None = new Puzzle(0, ""); //new Puzzle(0, false);
        //public static readonly Puzzle None = new Puzzle(0, -1); //new Puzzle(0, false);
        public Puzzle()
        {
            this.puzzle_subject = 0;
            this.puzzle_player = "";
            //this.puzzle_player = -1;
            //this.puzzle_foryou = false;
        }
        //public Puzzle(int puzzleSubject, bool puzzleForyou)
        public Puzzle(int puzzleSubject, string puzzlePlayer)
        //public Puzzle(int puzzleSubject, int puzzlePlayer)
        {
            this.puzzle_subject = puzzleSubject;
            this.puzzle_player = puzzlePlayer;
            //this.puzzle_foryou = puzzleForyou;
        }

        public string GetPuzzleDescription()
        {
            return PuzzleDatabase.GetPuzzle(this.puzzle_subject);
        }

    }
    public static class PuzzleDatabase
    {
        private static readonly Dictionary<int, string> puzzleDict = new Dictionary<int, string>
        {
            {1, "(角色)孫悟空"},
            {2, "(角色)泰山"},
            {3, "(角色)魯夫"},
            {4, "(角色)綠巨人"},
            {5, "(物品)書"},
            {6, "(物品)牙刷"},
            {7, "(物品)水壺"},
            {8, "(物品)眼鏡"},
            {9,  "(物品)望遠鏡" },
            {10, "(物品)雨傘"},
            {11, "(物品)螺絲起子" },
            {12, "(運動)羽球"},
            {13, "(運動)籃球"},
            {14, "(運動)桌球"},
            {15, "(運動)撞球"},
            {16, "(運動)棒球"},
            {17, "(動作)開車"},
            {18, "(動作)拍照"},
            {19, "(動作)洗臉"},
            {20, "(動作)釣魚"},
            {21, "(動作)彈鋼琴"},
            {22, "(職業)警察"},
            {23, "(職業)軍人"},
            {24, "(職業)消防員"},
            {25, "(職業)忍者"}
        };

        public static string GetPuzzle(int id)
        {
            return puzzleDict.TryGetValue(id, out string value) ? value : null;
        }
    }

}