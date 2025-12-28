using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StandbyCollider : MonoBehaviour
{

    public MultiPlayer.ChapterManager chapterManager;
    public MeshRenderer meshRenderer;

    //public static Dictionary<int, string> circleDict = new Dictionary<int, string>
    //{
    //    { 1, "StandbyCircle1" },
    //    { 2, "StandbyCircle2" },
    //    { 3, "StandbyCircle3" },
    //    { 4, "StandbyCircle4" },
    //};
    public static Dictionary <string, int> circleDictIdxMap = new Dictionary<string, int>
    {
        { "StandbyCircle1", 1 },
        { "StandbyCircle2", 2 },
        { "StandbyCircle3", 3 },
        { "StandbyCircle4", 4 },
    };

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }



    //private void OnCollisionEnter(Collision collision)
    private void OnTriggerEnter(Collider other)
    {
        if (other.tag != "Player") return;
        Debug.Log("<color=red>[Collider > Standby]</color> Enter StandbyCollider by + " + other.name + $"who's tag is {other.tag}");
        
        //!! update the Circle Id for ChapterManager after DoneJanken
        if (true){ // 不需要條件限制也可以總是更新NeoCircleIdx，因為只有到新一輪、或DoneJanken而要計算分數...時用到
          //if (chapterManager.DoneJanken) { //條件不能太嚴苛(roomChapter==2之類的)，因為這是Enter瞬間、停留期間不算
            int circleIdx = 0;
            bool canFindIdx = circleDictIdxMap.TryGetValue(this.gameObject.name, out circleIdx);
            if (!canFindIdx || circleIdx < 1 || circleIdx > 4)
            {
                Debug.Log("<color=red>[Error]</color> StandbyCollider CircleIdx is out of range: " + circleIdx);
            }
            chapterManager.NeoCircleIdx = circleIdx;
        }

        chapterManager.IsInCircle = true; // Should be set after NeoCircleIdx is updated
        //meshRenderer.material.color = Color.red;
        // 將 #0DFF00 轉為 Color 結構的 RGB 值
        meshRenderer.material.color = new Color(0.051f, 1.0f, 0.0f); // #0DFF00
    }
    //private void OnCollisionExit(Collision collision)
    private void OnTriggerExit(Collider other)
    {
        if (other.tag != "Player") return;
        Debug.Log("<color=red>[Collider > Standby]</color> Exit StandbyCollider by + " + other.name + $"who's tag is {other.tag}");
        chapterManager.IsInCircle = false;
        meshRenderer.material.color = new Color(0.349f, 0.824f, 0.0f); // #59D200
    }

}
