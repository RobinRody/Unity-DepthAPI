using MultiPlayer;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JankenTrigger : MonoBehaviour
{
    public MultiPlayer.ChapterManager chapterManager;
    //public JankenFloorChanger jankenFloorChanger;
    public MeshRenderer meshRenderer;

    //private void OnCollisionEnter(Collision collision)
    private void OnTriggerEnter(Collider other)
    {
        if (other.tag != "Player") return;
        Debug.Log("<color=red>[Collider > Janken]</color> Enter JankenTrigger by + " + other.name + $"who's tag is {other.tag}");
        //chapterManager.IsInCircle = true;
        chapterManager.DoneJanken = true;

        //jankenFloorChanger.FloorStateChange(true);
        meshRenderer.material.color = new Color(1.0f, 0.812f, 0.0f);   // #FFCF00
        //meshRenderer.material.color = Color.red;
    }
    //private void OnCollisionExit(Collision collision)
    private void OnTriggerExit(Collider other)
    {
        if (other.tag != "Player") return;
        Debug.Log("<color=red>[Collider > Janken]</color> Exit JankenTrigger by + " + other.name + $"who's tag is {other.tag}");
        //chapterManager.IsInCircle = false;
        //chapterManager.DoneJanken = false;

        meshRenderer.material.color = new Color(1.0f, 0.941f, 0.0f);   // #FFF000
        //meshRenderer.material.color = new Color(0.349f, 0.824f, 0.0f); // #59D200
    }

}
