using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GeneratorBrain : MonoBehaviour
{
    public GameObject centralbrainObject;
    private float spawnRangeX = 8;
    private float spawnRangeY = 4;
    private float spawnPosZ = 0;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnMouseDown()
    {
        centralbrainObject = GameObject.Find("CentralBrainObject");
        Vector3 spawnPos = new Vector3(Random.Range(-spawnRangeX, spawnRangeX), Random.Range(-spawnRangeY, spawnRangeY), spawnPosZ);
        centralbrainObject.GetComponent<CentralBrain>().eventList.Add(new Event { Command = "spawn", ChosenObject = "filter", Position = spawnPos});
    }
}
