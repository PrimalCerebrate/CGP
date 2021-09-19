using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProfessorBrain : MonoBehaviour
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
        if (!CentralBrain.inConversation)
        {
            CentralBrain.eventList.Add(new Event { Command = "startConversation", ChosenObject = "professor", Position = gameObject.transform.position });
        }   
    }
}
