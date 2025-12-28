//using Accord.Math.Geometry;
using AI4Animation;
//using SIGGRAPH_2024;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MultiPlayer {
    [System.Serializable]
    public class ToServerBasic : GM.JsonFuncBase {
        public long timestamp;
        public string device_id;

        public ToServerBasic() {
            SetDeviceID(string.Empty);
            UpdateTimestamp();
        }

        public void SetDeviceID(string id) {
            device_id = id;
        }

        public void UpdateTimestamp() {
            timestamp = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
        }
    }

    [System.Serializable]
    public class BoneTransformData
    {
        public string bone_name;
        public Vector3 position;
        public Quaternion rotation;

        public BoneTransformData(string name, Vector3 pos, Quaternion rot)
        {
            bone_name = name;
            position = pos;
            rotation = rot;
        }
    }


    //!!** Modified: Needed to be modified
    [System.Serializable]
    public class HeartbeatData : ToServerBasic {

        //*** StageControl-related ***//
        public int chapter;
        public bool in_circle;      //!!++Added: ReadyToMove/IsInCircle
        public bool done_janken;    //!!++Added: Janken_done
        public int win;             //!!++Added: Janken_win ocunt

        //*** Avatar-related ***//
        //public Vector3 head_position;
        //public Quaternion head_rotation;
        //public Vector3 left_hand_position;
        //public Quaternion left_hand_rotation;
        //public Vector3 right_hand_position;
        //public Quaternion right_hand_rotation;
        public float scale; // avatar_scale
        public bool left_hand_available;
        public bool right_hand_available;
        public List<BoneTransformData> bone_transforms = new List<BoneTransformData>();
        
        //!!++ Added: Stage-control


        public HeartbeatData(string id = "") {
            SetDeviceID(id);
            //SetChapter(-1);
            SetChapterCtrl(0, false, false, 0); //SetChapterCtrl(int chapter, bool inCircle, bool doneJanken, int winCount)
            //UpdateData(Vector3.zero, Vector3.zero, false, Vector3.zero, false);
            //UpdateData(Vector3.zero, Quaternion.identity, Vector3.zero, Quaternion.identity, false, Vector3.zero, Quaternion.identity, false);
            bone_transforms = new List<BoneTransformData>();
            //Transform[] transforms = new Transform[Blueman.FullBodyNames.Length];
            //UpdateData(Blueman.FullBodyNames, transforms, false, false);
        }

        public void SetChapter(int val) {
            chapter = val;
        }
        public void SetInCircle(bool val) {
            in_circle = val;
        }
        public void SetDoneJanken(bool val) {
            done_janken = val;
        }
        public void SetWinCount(int val) {
            win = val;
        }

        public void SetChapterCtrl(int chapter, bool inCircle, bool doneJanken, int winCount) {
            SetChapter(chapter);
            SetInCircle(inCircle);
            SetDoneJanken(doneJanken);
            SetWinCount(winCount);
        }

        //public void UpdateData(Vector3 hPos, Quaternion hRot, Vector3 rhPos, Quaternion rhRot, bool rhValid, Vector3 lhPos, Quaternion lhRot, bool lhValid)
        //{
        //    head_position = hPos;
        //    head_rotation = hRot;           // �s�W
        //    left_hand_position = lhPos;
        //    left_hand_rotation = lhRot;     // �s�W
        //    right_hand_position = rhPos;
        //    right_hand_rotation = rhRot;    // �s�W
        //    left_hand_available = lhValid;
        //    right_hand_available = rhValid;
        //    //UpdateBoneTransforms(boneNames, transforms);

        //    UpdateTimestamp();
        //}

        public void UpdateData(Transform headTransform, bool rhValid, bool lhValid, float avatarScale)
        {
            //UpdateBoneTransforms(bones);
            bone_transforms.Add(new BoneTransformData(
                "b_head",
                headTransform.position,
                headTransform.rotation
            ));
            left_hand_available = lhValid;
            right_hand_available = rhValid;
            scale = avatarScale;

            UpdateTimestamp();
        }
        //public void UpdateData(Actor.Bone[] bones, bool rhValid, bool lhValid, float avatarScale)
        //{
        //    UpdateBoneTransforms(bones);
        //    left_hand_available = lhValid;
        //    right_hand_available = rhValid;
        //    scale = avatarScale;

        //    UpdateTimestamp();
        //}

        //public void UpdateBoneTransforms(Actor.Bone[] bones)
        //{
        //    bone_transforms.Clear();
        //    foreach (var bone in bones)
        //    {
        //        var t = bone.GetTransform();
        //        bone_transforms.Add(new BoneTransformData(
        //            bone.GetName(),
        //            t.position,
        //            t.rotation
        //        ));
        //    }
        //}

        public void UpdateBoneTransforms(string[] boneNames, Transform[] transforms)
        {
            if (boneNames.Length != transforms.Length)
            {
                Debug.LogWarning($"[HeartbeatData] Bone names count ({boneNames.Length}) does not match transforms count ({transforms.Length}).");
                return;
            }

            bone_transforms.Clear();
            for (int i = 0; i < boneNames.Length && i < transforms.Length; i++)
            {
                bone_transforms.Add(new BoneTransformData(
                    boneNames[i],
                    transforms[i].position,
                    transforms[i].rotation
                ));
            }
        }
        public void UpdateBoneTransform(string boneName, Transform transform)
        {
            if (boneName == null || transform == null)
            {
                Debug.LogWarning($"[HeartbeatData] Bone name or transform is null.");
                return;
            }

            // Find existing bone transform and update it
            for (int i = 0; i < bone_transforms.Count; i++)
            {
                if (bone_transforms[i].bone_name == boneName)
                {
                    bone_transforms[i].position = transform.position;
                    bone_transforms[i].rotation = transform.rotation;
                    return;
                }
            }

            // If not found, add a new one
            bone_transforms.Add(new BoneTransformData(
                 boneName,
                 transform.position,
                 transform.rotation
             ));
            Debug.LogWarning($"[HeartbeatData] Bone added: {boneName}");
        }

        public void PrintDebug() {
            //Debug.Log($">>> <color=cyan>[Player] {device_id}</color> | left_hand_available = {left_hand_available}: ");
            //Debug.Log($">>> <color=cyan>[Player] {device_id}</color> | right_hand_available = {right_hand_available}: ");
            //Debug.Log($">>> <color=cyan>[Player] {device_id}</color> | Bone list: ");
            //var boneCount = bone_transforms.Count;
            ////foreach (var bone in bone_transforms)
            //for (var i = 0; i < boneCount/7; i++)
            //{
            //    var bone = bone_transforms[i];
            //    Debug.Log($"> <color=cyan>[Player] {device_id}</color> | {bone.bone_name} Pos=({bone.position.x}, {bone.position.y}, {bone.position.z}) Rot=({bone.rotation.x}, {bone.rotation.y}, {bone.rotation.z}, {bone.rotation.w})");
            //}

            if (bone_transforms.Count == 0) {
                Debug.LogWarning($">>> <color=cyan>[Player] {device_id}</color> | No bone transforms available yet.");
                return;
            }
            else
            {
                var boneHead = bone_transforms.Find(b => b.bone_name == "b_head");
                //Debug.Log($"> <color=cyan>[Player] {device_id}</color> | {boneHead.bone_name} Pos=({boneHead.position.x}, {boneHead.position.y}, {boneHead.position.z}) Rot=({boneHead.rotation.x}, {boneHead.rotation.y}, {boneHead.rotation.z}, {boneHead.rotation.w})");
                Debug.Log($"> <color=cyan>[Player] {device_id}</color> | scale={scale}, {boneHead.bone_name} Pos=({boneHead.position.x}, {boneHead.position.y}, {boneHead.position.z}) Rot=({boneHead.rotation.x}, {boneHead.rotation.y}, {boneHead.rotation.z}, {boneHead.rotation.w})");
                //Debug.Log($"> <color=cyan>[Player] {device_id}</color> | Head Pos=({head_position.x}, {head_position.y}, {head_position.z}) Rot=({head_rotation.x}, {head_rotation.y}, {head_rotation.z}, {head_rotation.w}) | LH Pos=({left_hand_position.x}, {left_hand_position.y}, {left_hand_position.z}) Rot=({left_hand_rotation.x}, {left_hand_rotation.y}, {left_hand_rotation.z}, {left_hand_rotation.w}) | RH Pos=({right_hand_position.x}, {right_hand_position.y}, {right_hand_position.z}) Rot=({right_hand_rotation.x}, {right_hand_rotation.y}, {right_hand_rotation.z}, {right_hand_rotation.w})");
            }
        }        
    }

    [System.Serializable]
    public class HeartbeatPayload : SocketPayloadBasic {

        public HeartbeatData heartbeat;

        public HeartbeatPayload(string id = "") {
            SetupMessageType(SocketMsgType.ToServer.Heartbeat);
            heartbeat = new HeartbeatData(id);
        }

        public void UpdateHeartbeatData(HeartbeatData val) {
            heartbeat = val;
        }
    }

    [System.Serializable]
    public class ReadyToMoveData : ToServerBasic {
        public int chapter;

        public ReadyToMoveData(string id = "") {
            SetDeviceID(id);
            SetChapter(-1);
        }

        public void SetChapter(int val) {
            chapter = val;
        }

        public void UpdateData(int chapterVal) {
            SetChapter(chapterVal);
            UpdateTimestamp();
        }
    }

    [System.Serializable]
    public class ReadyToMovePayload : SocketPayloadBasic {
        public ReadyToMoveData ready_to_move;

        public ReadyToMovePayload(string id = "") {
            SetupMessageType(SocketMsgType.ToServer.ReadyToMove);
            ready_to_move = new ReadyToMoveData(id);
        }
    }

    [System.Serializable]
    public class ShotData : ToServerBasic {
        public Vector3 position;
        public Vector3 direction;

        public ShotData(string id) {
            SetDeviceID(id);
            UpdateTimestamp();
            UpdateShoot(Vector3.zero, Vector3.forward);
        }

        public void UpdateShoot(Vector3 pos, Vector3 dir) {
            position = pos;
            direction = dir;
        }
    }

    [System.Serializable]
    public class ShotPayload : SocketPayloadBasic {
        public ShotData shot_event;

        public ShotPayload(string id) {
            SetupMessageType(SocketMsgType.ToServer.ShotEvent);
            shot_event = new ShotData(id);
        }
    }

    [System.Serializable]
    public class LanternData : ToServerBasic {
        public int lantern_id = 0;
        public Vector3[] postions;

        public LanternData(string id) {
            SetDeviceID(id);
            UpdateTimestamp();
            UpdateLantern(0, new Vector3[0]);
        }

        public void UpdateLantern(int lID, Vector3[] pts) {
            lantern_id = lID;
            postions = pts;
        }
    }

    [System.Serializable]
    public class LanternPayload : SocketPayloadBasic {
        public LanternData lantern;

        public LanternPayload(string id) {
            SetupMessageType(SocketMsgType.ToServer.Lantern);
            lantern = new LanternData(id);
        }
    }

    [System.Serializable]
    public class QAData : ToServerBasic {
        public int question_id = 0;
        public int state_int = 0;
        public bool state_bool = false;

        public QAData(string id) {
            SetDeviceID(id);
            UpdateTimestamp();
            UpdateQA(0, 0, false);
        }

        public void UpdateQA(int qaID, int status, bool val) {
            question_id = qaID;
            state_int = status;
            state_bool = val;
        }
    }

    [System.Serializable]
    public class QAPayload : SocketPayloadBasic {
        public QAData qa;

        public QAPayload(string id) {
            SetupMessageType(SocketMsgType.ToServer.ShotEvent);
            qa = new QAData(id);
        }
    }

    [System.Serializable]
    public class ResumeQAPayload : SocketPayloadBasic {
        public bool resume_qa = false;

        public ResumeQAPayload(string id, bool val = false) {
            SetupMessageType(SocketMsgType.ToServer.ResumeQA);
            resume_qa = val;
        }
    }
}