using UnityEngine;

public class CameraFollower : MonoBehaviour
{
    public Transform target; // The target for the camera to follow
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (target == null)
        {
            Debug.LogWarning("[CameraFollower]: No target assigned for the camera to follow.");
            return;
        }
        transform.position = new Vector3(target.position.x, transform.position.y, target.position.z);
        


    }
}
