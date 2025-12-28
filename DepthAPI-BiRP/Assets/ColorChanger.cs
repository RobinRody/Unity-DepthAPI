using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColorChanger : MonoBehaviour
{
    public SkinnedMeshRenderer AvatarMeshRenderer;
    public Color AvatarOtherColor = Color.gray;
    bool IsColorChanged = false;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (!IsColorChanged) ChangeAvatarColor();
    }
    public void ChangeAvatarColor()
    {
        if (!IsColorChanged)
        {
            AvatarMeshRenderer.material.color = AvatarOtherColor; // 這裡會產生一份新的材質實例，不會影響 prefab
            IsColorChanged = true; // 設置為 true，避免重複改變顏色
            Debug.LogWarning("AvatarScaler: AvatarMeshRenderer is green now!");

            //Transform bodyPLY = AvatarMeshRenderer.transform.Find("body_PLY_s0"); // obtain body_PLY_s0 and change the color
            //if (bodyPLY != null)
            ////if (AvatarMeshRenderer != null)
            //{
            //    Renderer renderer = bodyPLY.GetComponent<Renderer>();
            //    if (renderer != null)
            //    {
            //        renderer.material.color = Color.green; // 這裡會產生一份新的材質實例，不會影響 prefab
            //        IsColorChanged = true; // 設置為 true，避免重複改變顏色
            //        Debug.LogWarning("AvatarScaler: body_PLY_s0 is green now!");
            //    }
            //    else
            //    {
            //        Debug.LogWarning("AvatarScaler: body_PLY_s0 has no Renderer Components");
            //    }
            //}
            //else
            //{
            //    Debug.LogWarning("AvatarScaler: Can't find body_PLY_s0!");
            //}
        }
    }
}
