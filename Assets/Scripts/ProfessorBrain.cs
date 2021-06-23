using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProfessorBrain : MonoBehaviour
{
    public GameObject centralbrainObject;

    // Start is called before the first frame update
    void Start()
    {
        centralbrainObject = GameObject.Find("CentralBrainObject");
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnMouseDown()
    {
        centralbrainObject.GetComponent<CentralBrain>().eventList.Add(new Event { Command = "startConversation", ChosenObject = "professor", Position = gameObject.transform.position });
    }
}
