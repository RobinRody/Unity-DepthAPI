using MultiPlayer;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JankenFloorChanger : MonoBehaviour
{

    public GameObject[] routesGoForth;
    public GameObject[] routesGoBack;
    public ChapterManager chapterManager;
    private bool doneJanken = false;

    // Start is called before the first frame update
    void Start()
    {
        FloorStateChange(doneJanken);
    }

    // Update is called once per frame
    void Update()
    {
        if (chapterManager.DoneJanken != doneJanken) { 
            doneJanken = chapterManager.DoneJanken;
            FloorStateChange(doneJanken);
        }
    }
    public void FloorStateChange(bool canGoBack) { 
        if (canGoBack) { 
            foreach (GameObject go in routesGoForth) { 
                go.SetActive(true);
            }
            foreach (GameObject go in routesGoBack) { 
                go.SetActive(true);
            }
        }
        else { 
            foreach (GameObject go in routesGoForth)
            {
                go.SetActive(true);
            }
            foreach (GameObject go in routesGoBack)
            {
                go.SetActive(false);
            }
        }
    }

}
