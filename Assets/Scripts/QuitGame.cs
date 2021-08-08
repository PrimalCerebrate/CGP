using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class QuitGame : MonoBehaviour
{

    public GameObject centralbrainObject;

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
        string filePath = Application.streamingAssetsPath;
        string saveFile = filePath + "/SaveFiles/gamesave.txt";

        FileStream fileCreate = File.Open(saveFile, FileMode.Create);
        StreamWriter writer = new StreamWriter(fileCreate);

        //Write events to gamesave.txt
        foreach (var currentEvent in CentralBrain.eventList)
        {
            string saveLine = currentEvent.Command + ";" + currentEvent.ChosenObject + ";" + currentEvent.Position.x + "," + currentEvent.Position.y + "," + currentEvent.Position.z;
            writer.WriteLine(saveLine);
        }

        writer.Close();

        //Quit Game
        Application.Quit();
    }

}
