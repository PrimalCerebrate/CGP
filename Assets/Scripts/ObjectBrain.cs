using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectBrain : MonoBehaviour
{

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
        CentralBrain.eventList.Add(new Event {Command = "destroy", ChosenObject = "filter", Position = gameObject.transform.position});
    }
}
