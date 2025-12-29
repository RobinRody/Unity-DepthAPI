using Meta.XR.MRUtilityKit;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class RoomAligner : MonoBehaviour
{
    [Header("Scene Parent GameObject")] //!!++ Added
    public GameObject RoomObject; // The parent object of the whole room
    
    [Header("RoomAlignmentGoal")]//!!++ Added
    public float frontZ = 6f; // Z of the wall with WindowFrame
    public float rightX = 7f; // X of the right-sied wall with WallArt
    public float leftX = -7f; // X of the left-side wall with WallArt
    public bool Zforward = false; // if the forward of WindowFrame is Z-axis forward positive
    public bool useLeftWall = false; // 是否使用左側牆壁的WallArt來對齊房間

    private Matrix4x4 alignmentMatrix = Matrix4x4.identity; // Initialize with identity matrix
    private bool anchorsInitialized = false;
    private bool isRoomAligned = false;

    [Header("Action")]
    public bool toAlignRoom = false; // 執行過程中手動Align Room
    // Start is called before the first frame update
    void Start()
    {
       
    }

    // Update is called once per frame
    void Update()
    {
        if (!anchorsInitialized)
        {
            AnchorInit();
        }
        else if (!isRoomAligned && toAlignRoom) // For Debug: is Anchor is initialized, but room is not aligned yet
        {
            AlignRoom();
        }
    }

    void AnchorInit()
    {
        //!! 1. Find MRUKAnchor objects in the scene
        var anchors = FindObjectsOfType<MRUKAnchor>();
        Debug.Log($"Found {anchors.Length} MRUKAnchors in the scene.");

        if (anchors.Length == 0) { Debug.LogError("No MRUKAnchors found in the scene."); return; }
        foreach (var anchor in anchors)
        {
            // Replace 'anchor.Type' with 'anchor.Label' or another valid property of MRUKAnchor.  
            Debug.LogWarning($"++ Scene! Label: {anchor.Label}, Name: {anchor.name}, Position: {anchor.transform.position}");
        }

        // Find Plant (Z=6)
        var plant = anchors.FirstOrDefault(a => a.name == "PLANT");
        if (plant == null) { Debug.LogError("Can't find PLANT"); return; }
        else Debug.LogWarning($"Found PLANT at {plant.transform.position}");
        // Find all WindowFrame
        var windowFrames = anchors.Where(a => a.name == "WINDOW_FRAME").ToList();
        if (windowFrames.Count == 0) { Debug.LogError("Can't find WINDOW_FRAME"); return; }
        else { Debug.LogWarning($"Found {windowFrames.Count} WINDOW_FRAMEs in the scene."); }
        // Choose the WindowFrame which is closest to the Plant
        var windowFrame = windowFrames.OrderBy(a => Vector3.Distance(a.transform.position, plant.transform.position)).First();

        // Find WallArt (X=10.5)
        var wallArt = anchors.FirstOrDefault(a => a.name == "WALL_ART");
        if (wallArt == null) { Debug.LogError("Can't find WALL_ART"); return; }
        else { Debug.LogWarning($"Found WALL_ART at {wallArt.transform.position}"); }


        // 2. get the current positions and forward vectors of WindowFrame and WallArt
        Vector3 windowPos = windowFrame.transform.position;
        Vector3 wallArtPos = wallArt.transform.position;
        Vector3 windowForward = windowFrame.transform.forward;
        Vector3 wallArtForward = wallArt.transform.forward;

        // 3. set the goal positions and forward vectors
        //Vector3 targetWindowPos = new Vector3(windowPos.x, windowPos.y, 6f);
        Vector3 targetWindowPos = new Vector3(windowPos.x, windowPos.y, frontZ);
        //Vector3 targetWallArtPos = new Vector3(10.5f, wallArtPos.y, wallArtPos.z);
        //Vector3 targetWallArtPos = new Vector3(7f, wallArtPos.y, wallArtPos.z);
        Vector3 targetWallArtPos = new Vector3((useLeftWall) ? leftX : rightX, wallArtPos.y, wallArtPos.z);
        Vector3 targetWindowForward = Vector3.forward * ((Zforward) ? 1 : (-1)); // Z軸負向
        Vector3 targetWallArtForward = -Vector3.right;  // X軸負向

        // 4. Calculate Rotation (for WindowFrame, let its forward aligned to targetWindowForward)
        Quaternion rotToTarget_original = Quaternion.FromToRotation(windowForward, targetWindowForward);
        Quaternion rotToTarget = Quaternion.Euler(0f, rotToTarget_original.eulerAngles.y, 0f); //!!Modfied: Only rotate around Y-axis，避免地板歪掉
        Debug.Log($"rotToTarget_original eulerAngles: {rotToTarget_original.eulerAngles}");
        Debug.Log($"rotToTarget (Y only) eulerAngles: {rotToTarget.eulerAngles}");

        // 5. Calculate translation (Rotation then Translation)
        Vector3 windowPosAfterRot = rotToTarget * windowPos;
        Vector3 translationZ = targetWindowPos - windowPosAfterRot;
        Vector3 wallArtPosAfterRot = rotToTarget * wallArtPos + translationZ;
        Vector3 translationX = targetWallArtPos - wallArtPosAfterRot;

        Vector3 translationXZ = new Vector3(translationX.x + translationZ.x, 0f, translationZ.z); // only X and Z translation  
        alignmentMatrix = Matrix4x4.TRS(translationXZ, Quaternion.identity, Vector3.one) * Matrix4x4.TRS(Vector3.zero, rotToTarget, Vector3.one);

        Debug.Log("@@@windowPos = " + windowPos);
        Debug.Log("@@@windowPos-New = " + (rotToTarget * windowPos + translationXZ));


        Debug.Log("@@@wallArtPos = " + wallArtPos);
        Debug.Log("@@@wallArtPos-New = " + (rotToTarget*wallArtPos+translationXZ));
        
        AlignRoom();


        //foreach (var anchor in anchors)
        //{
        //    Matrix4x4 original = anchor.transform.localToWorldMatrix;
        //    Matrix4x4 transformed = alignmentMatrix * original;
        //    anchor.transform.SetPositionAndRotation(transformed.GetColumn(3), transformed.rotation);
        //}

        anchorsInitialized = true;
    }
    private void AlignRoom()
    {
        //// Align the MRUK room
        //var Room = FindObjectsOfType<MRUKRoom>();
        //if (Room.Length == 0) { Debug.LogError("No MRUKRoom found in the scene."); return; }
        //Matrix4x4 original = Room[0].transform.localToWorldMatrix;
        //Matrix4x4 transformed = alignmentMatrix * original;
        //Room[0].transform.SetPositionAndRotation(transformed.GetColumn(3), transformed.rotation);

        // Align the whole Room(Garden) GameObject
        Matrix4x4 originalRoom = RoomObject.transform.localToWorldMatrix;
        Matrix4x4 neoRoom = alignmentMatrix.inverse * originalRoom; // 讓Room(Garden)的position和rotation回到原點
        RoomObject.transform.SetPositionAndRotation(neoRoom.GetColumn(3), neoRoom.rotation);
        Debug.Log($"Room original pos: {originalRoom.GetColumn(3)}, rot: {originalRoom.rotation.eulerAngles}");
        Debug.Log($"Room new pos: {neoRoom.GetColumn(3)}, rot: {neoRoom.rotation.eulerAngles}");

        Debug.Log("[][][] Room Aligning!!");
        isRoomAligned = true;
    }
    public Matrix4x4 AlignTransformation2Room(Matrix4x4 original)
    {
        //Debug.Log("[AlignTransformation2Room]anchorsInitialized = " + anchorsInitialized);
        if(anchorsInitialized) return alignmentMatrix * original;
        return original;
    }

    public bool IsAnchorsInitialized()
    {
        return anchorsInitialized;
    }
    public bool IsRoomAligned()
    {
        return isRoomAligned;
    }
}