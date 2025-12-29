using AI4Animation;
using SIGGRAPH_2024;
using System;
using UnityEngine;

public class TrackingSpaceRotator : MonoBehaviour
{
    [Header("References")]
    public Transform trackingSpace; // OVRCameraRig 的 TrackingSpace
    public Transform centerEyeAnchor; // OVRCameraRig 的 CenterEyeAnchor (相機)
    public RoomAligner roomAligner; // Reference to RoomAligner


    public float ViewDistance = 2.5f;
    private bool hasAligned = false; // 確保只對齊一次
    
    void Start()
    {
        if (roomAligner == null)
        {
            roomAligner = FindObjectOfType<RoomAligner>();
            if (roomAligner == null)
            {
                Debug.LogError("RoomAligner not found in scene!");
            }
        }

        // 如果沒有手動設定,嘗試自動找到 OVRCameraRig 相關物件
        if (trackingSpace == null || centerEyeAnchor == null)
        {
            OVRCameraRig rig = FindObjectOfType<OVRCameraRig>();
            if (rig != null)
            {
                trackingSpace = rig.trackingSpace;
                centerEyeAnchor = rig.centerEyeAnchor;
                Debug.Log($"Auto-found TrackingSpace: {trackingSpace.name}");
                Debug.Log($"Auto-found CenterEyeAnchor: {centerEyeAnchor.name}");
            }
            else
            {
                Debug.LogError("OVRCameraRig not found in scene!");
            }
        }
    }

    void Update()
    {
        //// 等待 RoomAligner 初始化完成且尚未對齊過
        //if (!hasAligned && roomAligner != null && roomAligner.IsAnchorsInitialized())
        //{
        //    AlignTrackingSpace();
        //}
        if (roomAligner != null && roomAligner.IsAnchorsInitialized())
        {
            //SetCameraPosition(trackingSpace, Camera.main, CalculateViewPoint(ViewDistance));

            Debug.LogWarning("<color=red>[TrackingSpaceRotator]</color> <<Before Update SetCameraPosition\n" +
                $"trackingSpace.positon = {trackingSpace.position}\ntrackingSpace.rotation = {trackingSpace.rotation}");
            Matrix4x4 matrixCenterEye = Matrix4x4.TRS(centerEyeAnchor.position, centerEyeAnchor.rotation, Vector3.one);
            Matrix4x4 rotatedCenterEye = roomAligner.AlignTransformation2Room(matrixCenterEye);
            Vector3 positionDelta = rotatedCenterEye.GetPosition() - matrixCenterEye.GetPosition() + matrixCenterEye.GetPosition();

            trackingSpace.position += positionDelta;
            //trackingSpace.position = rotatedCenterEye.GetPosition() - matrixCenterEye.GetPosition() + CalculateViewPoint(ViewDistance) - centerEyeAnchor.transform.localPosition;
            trackingSpace.rotation = Quaternion.identity;
            Debug.LogWarning("<color=red>[TrackingSpaceRotator]</color> >>After Update SetCameraPosition\n" + 
                $"trackingSpace.positon = {trackingSpace.position}\ntrackingSpace.rotation = {trackingSpace.rotation}");
        }
    }

    void AlignTrackingSpace()
    {
        if (trackingSpace == null || centerEyeAnchor == null)
        {
            Debug.LogError("TrackingSpace or CenterEyeAnchor reference is null!");
            return;
        }

        // 1. 取得當前 TrackingSpace 的世界位置(這代表 VR 玩家的"虛擬 Avatar"當前位置)
        Vector3 currentAvatarPosition = trackingSpace.position;
        Quaternion currentAvatarRotation = trackingSpace.rotation;

        // 2. 計算 Avatar 經過 RoomAlign 後應該要在的目標位置
        Matrix4x4 currentMatrix = Matrix4x4.TRS(currentAvatarPosition, currentAvatarRotation, Vector3.one);
        Matrix4x4 alignedMatrix = roomAligner.AlignTransformation2Room(currentMatrix);
        
        Vector3 targetAvatarPosition = alignedMatrix.GetColumn(3);
        Quaternion targetAvatarRotation = alignedMatrix.rotation;

        // 3. 計算相機相對於 TrackingSpace 的 local offset
        Vector3 cameraLocalOffset = centerEyeAnchor.localPosition;

        // 4. 設定新的 TrackingSpace 位置
        //    目標: 讓 TrackingSpace 移動到 targetAvatarPosition
        //    但要補償相機的 local offset,確保玩家看到的世界位置正確
        trackingSpace.position = targetAvatarPosition - trackingSpace.rotation * cameraLocalOffset + currentAvatarRotation * cameraLocalOffset;
        trackingSpace.rotation = targetAvatarRotation;

        Debug.Log($"[TrackingSpaceRotator] TrackingSpace aligned!");
        Debug.Log($"  Current Avatar Position: {currentAvatarPosition}");
        Debug.Log($"  Target Avatar Position: {targetAvatarPosition}");
        Debug.Log($"  TrackingSpace New Position: {trackingSpace.position}");
        Debug.Log($"  TrackingSpace New Rotation: {trackingSpace.rotation.eulerAngles}");
        
        hasAligned = true; // 標記為已對齊,避免重複執行
    }

    // 提供手動觸發對齊的方法(用於調試)
    public void ManualAlign()
    {
        hasAligned = false;
        AlignTrackingSpace();
    }



    //void SetCameraPosition(Transform space, Camera camera)
    //{
    //    Matrix4x4 matrixCenterEye = Matrix4x4.TRS(centerEyeAnchor.position, centerEyeAnchor.rotation, Vector3.one);
    //    Matrix4x4 rotatedCenterEye = roomAligner.AlignTransformation2Room(matrixCenterEye);
    //    space.position = rotatedCenterEye.GetPosition() - matrixCenterEye.GetPosition() - camera.transform.localPosition;
    //    space.rotation = Quaternion.identity;

    //    //string CameraName = (space == CameraTrackingSpace) ? "CameraTrackingSpace" : "OVRTrackingSpace";
    //    //Debug.Log("!!"+CameraName  + " => SetCameraPosition: " + space.name + " position: " + space.position);
    //}
    //void SetCameraPosition(Transform space, Camera camera, Vector3 position)
    //{
    //    Matrix4x4 matrixCenterEye = Matrix4x4.TRS(centerEyeAnchor.position, centerEyeAnchor.rotation, Vector3.one);
    //    Matrix4x4 rotatedCenterEye = roomAligner.AlignTransformation2Room(matrixCenterEye);
    //    space.position = rotatedCenterEye.GetPosition() - matrixCenterEye.GetPosition() + position - camera.transform.localPosition;
    //    space.rotation = Quaternion.identity;

    //    //string CameraName = (space == CameraTrackingSpace) ? "CameraTrackingSpace" : "OVRTrackingSpace";
    //    //Debug.Log("!!"+CameraName  + " => SetCameraPosition: " + space.name + " position: " + space.position);
    //}

    //public Vector3 CalculateViewPoint(float distanceToHead)
    //{
    //    Vector3 centerEyePosition = 0.5f * (Actor.FindTransform("b_l_eye").position + Actor.FindTransform("b_r_eye").position);
    //    Vector3 headForward = Actor.GetBoneTransformation(Blueman.HeadName).GetUp();
    //    return centerEyePosition - distanceToHead * headForward;
    //}


}
