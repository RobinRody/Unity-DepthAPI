using AI4Animation;
using System.Collections.Generic;

namespace SIGGRAPH_2024 {
    public class Blueman {
        public const string RootName = "body_world";
        public const string HipsName = "b_root";
        public const string NeckName = "b_neck0";
        public const string HeadName = "b_head";
        public const string Spine0Name = "b_spine0";
        public const string Spine1Name = "b_spine1";
        public const string Spine2Name = "b_spine2";
        public const string SpineName = "b_spine3";
        public const string LeftShoulderName = "p_l_scap";
        public const string LeftArmName = "b_l_arm";
        public const string LeftElbowName = "b_l_forearm";
        public const string LeftWristTwistName = "b_l_wrist_twist";
        public const string LeftWristName = "b_l_wrist";
        public const string RightShoulderName = "p_r_scap";
        public const string RightArmName = "b_r_arm";
        public const string RightElbowName = "b_r_forearm";
        public const string RightWristTwistName = "b_r_wrist_twist";
        public const string RightWristName = "b_r_wrist";

        public const string LeftHipName = "b_l_upleg";
        public const string LeftKneeName = "b_l_leg";
        public const string LeftFootTwistName = "b_l_foot_twist";
        public const string LeftAnkleName = "b_l_talocrural";
        public const string LeftHeelName = "b_l_subtalar";
        public const string LeftToeName = "b_l_ball";

        public const string RightHipName = "b_r_upleg";
        public const string RightKneeName = "b_r_leg";
        public const string RightFootTwistName = "b_r_foot_twist";
        public const string RightAnkleName = "b_r_talocrural";
        public const string RightHeelName = "b_r_subtalar";
        public const string RightToeName = "b_r_ball";

        //!++ Added: Fingers Mapping
        public static Dictionary<string, BoneId> RightHandMapping = new Dictionary<string, BoneId> {
            // Thumb: 注意這邊是0不是1        
            {"b_r_thumb0", BoneId.Hand_Thumb0 }, 
            {"b_r_thumb1", BoneId.Hand_Thumb1 },
            {"b_r_thumb2", BoneId.Hand_Thumb2 },
            {"b_r_thumb3", BoneId.Hand_Thumb3 },
            {"b_r_thumb_null",  BoneId.Hand_ThumbTip },
            // Index: 注意這邊是1不是0
            {"b_r_index1", BoneId.Hand_Index1 }, 
            {"b_r_index2", BoneId.Hand_Index2 },
            {"b_r_index3", BoneId.Hand_Index3 },
            {"b_r_index_null", BoneId.Hand_IndexTip },
            // Middle
            {"b_r_middle1", BoneId.Hand_Middle1 }, 
            {"b_r_middle2", BoneId.Hand_Middle2 },
            {"b_r_middle3", BoneId.Hand_Middle3 },
            {"b_r_middle_null", BoneId.Hand_MiddleTip },
            // Ring
            {"b_r_ring1", BoneId.Hand_Ring1 }, 
            {"b_r_ring2", BoneId.Hand_Ring2 },
            {"b_r_ring3", BoneId.Hand_Ring3 },
            {"b_r_ring_null", BoneId.Hand_RingTip },
            //// Pinky： "Hand_Pinky0" 不對應            
            {"b_r_pinky1", BoneId.Hand_Pinky1 }, 
            {"b_r_pinky2", BoneId.Hand_Pinky2 },
            {"b_r_pinky3", BoneId.Hand_Pinky3 },
            {"b_r_pinky_null", BoneId.Hand_PinkyTip }
        };
        public static Dictionary<string, BoneId> LeftHandMapping = new Dictionary<string, BoneId> {
            // Thumb: 注意這邊是0不是1        
            {"b_l_thumb0", BoneId.Hand_Thumb0 }, 
            {"b_l_thumb1", BoneId.Hand_Thumb1 },
            {"b_l_thumb2", BoneId.Hand_Thumb2 },
            {"b_l_thumb3", BoneId.Hand_Thumb3 },
            {"b_l_thumb_null",  BoneId.Hand_ThumbTip },
            // Index: 注意這邊是1不是0
            {"b_l_index1",  BoneId.Hand_Index1 }, 
            {"b_l_index2", BoneId.Hand_Index2 },
            {"b_l_index3", BoneId.Hand_Index3 },
            {"b_l_index_null", BoneId.Hand_IndexTip },
            // Middle
            {"b_l_middle1", BoneId.Hand_Middle1 }, 
            {"b_l_middle2", BoneId.Hand_Middle2 },
            {"b_l_middle3", BoneId.Hand_Middle3 },
            {"b_l_middle_null", BoneId.Hand_MiddleTip },
            // Ring
            {"b_l_ring1", BoneId.Hand_Ring1 }, 
            {"b_l_ring2", BoneId.Hand_Ring2 },
            {"b_l_ring3", BoneId.Hand_Ring3 },
            {"b_l_ring_null", BoneId.Hand_RingTip },
            //// Pinky： "Hand_Pinky0" 不對應            
            {"b_l_pinky1", BoneId.Hand_Pinky1 }, 
            {"b_l_pinky2", BoneId.Hand_Pinky2 },
            {"b_l_pinky3", BoneId.Hand_Pinky3 },
            {"b_l_pinky_null", BoneId.Hand_PinkyTip }
        };
        public enum BoneId // part of OVRSkeleton.BoneId (from OVRPlugin.BoneId)
        {
            //Invalid = OVRPlugin.BoneId.Invalid,

            // Hand bones
            //Hand_Start = OVRPlugin.BoneId.Hand_Start,
            //Hand_WristRoot = OVRPlugin.BoneId.Hand_WristRoot, // root frame of the hand, where the wrist is located
            //Hand_ForearmStub = OVRPlugin.BoneId.Hand_ForearmStub, // frame for user's forearm
            Hand_Thumb0 = OVRPlugin.BoneId.Hand_Thumb0, // thumb trapezium bone
            Hand_Thumb1 = OVRPlugin.BoneId.Hand_Thumb1, // thumb metacarpal bone
            Hand_Thumb2 = OVRPlugin.BoneId.Hand_Thumb2, // thumb proximal phalange bone
            Hand_Thumb3 = OVRPlugin.BoneId.Hand_Thumb3, // thumb distal phalange bone
            Hand_Index1 = OVRPlugin.BoneId.Hand_Index1, // index proximal phalange bone
            Hand_Index2 = OVRPlugin.BoneId.Hand_Index2, // index intermediate phalange bone
            Hand_Index3 = OVRPlugin.BoneId.Hand_Index3, // index distal phalange bone
            Hand_Middle1 = OVRPlugin.BoneId.Hand_Middle1, // middle proximal phalange bone
            Hand_Middle2 = OVRPlugin.BoneId.Hand_Middle2, // middle intermediate phalange bone
            Hand_Middle3 = OVRPlugin.BoneId.Hand_Middle3, // middle distal phalange bone
            Hand_Ring1 = OVRPlugin.BoneId.Hand_Ring1, // ring proximal phalange bone
            Hand_Ring2 = OVRPlugin.BoneId.Hand_Ring2, // ring intermediate phalange bone
            Hand_Ring3 = OVRPlugin.BoneId.Hand_Ring3, // ring distal phalange bone
            Hand_Pinky0 = OVRPlugin.BoneId.Hand_Pinky0, // pinky metacarpal bone
            Hand_Pinky1 = OVRPlugin.BoneId.Hand_Pinky1, // pinky proximal phalange bone
            Hand_Pinky2 = OVRPlugin.BoneId.Hand_Pinky2, // pinky intermediate phalange bone
            Hand_Pinky3 = OVRPlugin.BoneId.Hand_Pinky3, // pinky distal phalange bone
            //Hand_MaxSkinnable = OVRPlugin.BoneId.Hand_MaxSkinnable,

            // Bone tips are position only. They are not used for skinning but are useful for hit-testing.
            // NOTE: Hand_ThumbTip == Hand_MaxSkinnable since the extended tips need to be contiguous
            Hand_ThumbTip = OVRPlugin.BoneId.Hand_ThumbTip, // tip of the thumb
            Hand_IndexTip = OVRPlugin.BoneId.Hand_IndexTip, // tip of the index finger
            Hand_MiddleTip = OVRPlugin.BoneId.Hand_MiddleTip, // tip of the middle finger
            Hand_RingTip = OVRPlugin.BoneId.Hand_RingTip, // tip of the ring finger
            Hand_PinkyTip = OVRPlugin.BoneId.Hand_PinkyTip, // tip of the pinky
            //Hand_End = OVRPlugin.BoneId.Hand_End
        }

        //public static Dictionary<string, string> RightHandMapping = new Dictionary<string, string> {
        //    {"b_r_thumb0", "Hand_Thumb0" }, // Thumb: 注意這邊是0不是1
        //    {"b_r_thumb1", "Hand_Thumb1" },
        //    {"b_r_thumb2", "Hand_Thumb2" },
        //    {"b_r_thumb3", "Hand_Thumb3" },
        //    {"b_r_thumb_null",  "Hand_ThumbTip"},
        //    {"b_r_index1", "Hand_Index1" }, // Index: 注意這邊是1不是0
        //    {"b_r_index2", "Hand_Index2" },
        //    {"b_r_index3", "Hand_Index3" },
        //    {"b_r_index_null", "Hand_IndexTip" },
        //    {"b_r_middle1", "Hand_Middle1" }, // Middle
        //    {"b_r_middle2", "Hand_Middle2" },
        //    {"b_r_middle3", "Hand_Middle3" },
        //    {"b_r_middle_null", "Hand_MiddleTip" },
        //    {"b_r_ring1", "Hand_Ring1" }, // Ring
        //    {"b_r_ring2", "Hand_Ring2" },
        //    {"b_r_ring3", "Hand_Ring3" },
        //    {"b_r_ring_null", "Hand_RingTip" },
        //    //// "Hand_Pinky0" 不對應            
        //    {"b_r_pinky1", "Hand_Pinky1" }, // Pinky
        //    {"b_r_pinky2", "Hand_Pinky2" },
        //    {"b_r_pinky3", "Hand_Pinky3" },
        //    {"b_r_pinky_null", "Hand_PinkyTip" }
        //};
        //public static Dictionary<string, string> LeftHandMapping = new Dictionary<string, string> {
        //    {"b_l_thumb0", "Hand_Thumb0" }, // Thumb: 注意這邊是0不是1
        //    {"b_l_thumb1", "Hand_Thumb1" },
        //    {"b_l_thumb2", "Hand_Thumb2" },
        //    {"b_l_thumb3", "Hand_Thumb3" },
        //    {"b_l_thumb_null",  "Hand_ThumbTip"},
        //    {"b_l_index1", "Hand_Index1" }, // Index: 注意這邊是1不是0
        //    {"b_l_index2", "Hand_Index2" },
        //    {"b_l_index3", "Hand_Index3" },
        //    {"b_l_index_null", "Hand_IndexTip" },
        //    {"b_l_middle1", "Hand_Middle1" }, // Middle
        //    {"b_l_middle2", "Hand_Middle2" },
        //    {"b_l_middle3", "Hand_Middle3" },
        //    {"b_l_middle_null", "Hand_MiddleTip" },
        //    {"b_l_ring1", "Hand_Ring1" }, // Ring
        //    {"b_l_ring2", "Hand_Ring2" },
        //    {"b_l_ring3", "Hand_Ring3" },
        //    {"b_l_ring_null", "Hand_RingTip" },
        //    //// "Hand_Pinky0" 不對應            
        //    {"b_l_pinky1", "Hand_Pinky1" }, // Pinky
        //    {"b_l_pinky2", "Hand_Pinky2" },
        //    {"b_l_pinky3", "Hand_Pinky3" },
        //    {"b_l_pinky_null", "Hand_PinkyTip" }
        //};

            //!++ Added: Fingers Names
        public static string[] RightHandNames = new string[] {
            "b_r_wrist",
            "b_r_thumb0",
            "b_r_thumb1",
            "b_r_thumb2",
            "b_r_thumb3",
            "b_r_thumb_null",
            "b_r_index1",
            "b_r_index2",
            "b_r_index3",
            "b_r_index_null",
            "b_r_middle1",
            "b_r_middle2",
            "b_r_middle3",
            "b_r_middle_null",
            "b_r_ring1",
            "b_r_ring2",
            "b_r_ring3",
            "b_r_ring_null",
            "b_r_pinky1",
            "b_r_pinky2",
            "b_r_pinky3",
            "b_r_pinky_null"
        };
        public static string[] LeftHandNames = new string[] {
            "b_l_wrist",
            "b_l_thumb0",
            "b_l_thumb1",
            "b_l_thumb2",
            "b_l_thumb3",
            "b_l_thumb_null",
            "b_l_index1",
            "b_l_index2",
            "b_l_index3",
            "b_l_index_null",
            "b_l_middle1",
            "b_l_middle2",
            "b_l_middle3",
            "b_l_middle_null",
            "b_l_ring1",
            "b_l_ring2",
            "b_l_ring3",
            "b_l_ring_null",
            "b_l_pinky1",
            "b_l_pinky2",
            "b_l_pinky3",
            "b_l_pinky_null"
        };

        public static string[] FullBodyNames = new string[] {
            "b_root",
            "b_l_upleg",
            "b_l_leg",
            "b_l_talocrural",
            "b_l_ball",
            "b_r_upleg",
            "b_r_leg",
            "b_r_talocrural",
            "b_r_ball",
            "b_spine0",
            "b_spine1",
            "b_spine2",
            "b_spine3",
            "b_neck0",
            "b_head",
            "b_l_shoulder",
            "p_l_scap",
            "b_l_arm",
            "b_l_forearm",
            "b_l_wrist_twist",
            "b_l_wrist",
            "b_r_shoulder",
            "p_r_scap",
            "b_r_arm",
            "b_r_forearm",
            "b_r_wrist_twist",
            "b_r_wrist"
        };
        public static string[] LowerBodyNames = new string[] {
            "b_root",
            "b_l_upleg",
            "b_l_leg",
            "b_l_talocrural",
            "b_l_ball",
            "b_r_upleg",
            "b_r_leg",
            "b_r_talocrural",
            "b_r_ball"
        };
        public static string[] UpperBodyNames = new string[] {
            "b_root",
            "b_spine0",
            "b_spine1",
            "b_spine2",
            "b_spine3",
            "b_neck0",
            "b_head",
            "b_l_shoulder",
            "p_l_scap",
            "b_l_arm",
            "b_l_forearm",
            "b_l_wrist_twist",
            "b_l_wrist",
            "b_r_shoulder",
            "p_r_scap",
            "b_r_arm",
            "b_r_forearm",
            "b_r_wrist_twist",
            "b_r_wrist"
        };
        public static string[] TrackerNames = new string[] {
            "b_head",
            "b_l_wrist",
            "b_r_wrist"
        };

        public static int[] FullBodyIndices = null;
        public static int[] LowerBodyIndices = null;
        public static int[] UpperBodyIndices = null;
        public static int[] TrackerIndices = null;
        public static int HipsIndex = -1;
        public static int LeftHipIndex = -1;
        public static int RightHipIndex = -1;
        public static int HeadIndex = -1;
        public static int LeftWristIndex = -1;
        public static int RightWristIndex = -1;
        public static int LeftKneeIndex = -1;
        public static int RightKneeIndex = -1;
        public static int LeftAnkleIndex = -1;
        public static int RightAnkleIndex = -1;
        public static int LeftToeIndex = -1;
        public static int RightToeIndex = -1;

        #if UNITY_EDITOR
        public static void RegisterIndices(MotionAsset asset) {
            FullBodyIndices = asset.Source.GetBoneIndices(FullBodyNames);
            LowerBodyIndices = asset.Source.GetBoneIndices(LowerBodyNames);
            UpperBodyIndices = asset.Source.GetBoneIndices(UpperBodyNames);
            TrackerIndices = asset.Source.GetBoneIndices(TrackerNames);
            HipsIndex = asset.Source.GetBoneIndex(HipsName);
            LeftHipIndex = asset.Source.GetBoneIndex(LeftHipName);
            RightHipIndex = asset.Source.GetBoneIndex(RightHipName);
            HeadIndex = asset.Source.GetBoneIndex(HeadName);
            LeftWristIndex = asset.Source.GetBoneIndex(LeftWristName);
            RightWristIndex = asset.Source.GetBoneIndex(RightWristName);
            LeftKneeIndex = asset.Source.GetBoneIndex(LeftKneeName);
            RightKneeIndex = asset.Source.GetBoneIndex(RightKneeName);
            LeftAnkleIndex = asset.Source.GetBoneIndex(LeftAnkleName);
            RightAnkleIndex = asset.Source.GetBoneIndex(RightAnkleName);
            LeftToeIndex = asset.Source.GetBoneIndex(LeftToeName);
            RightToeIndex = asset.Source.GetBoneIndex(RightToeName);
        }
        #endif
    }
}
