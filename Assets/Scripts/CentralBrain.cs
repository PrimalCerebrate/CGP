using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using System;

public class Event
{
    public string Command { get; set; }
    public string ChosenObject { get; set; }
    public Vector3 Position { get; set; }
}

public class CentralBrain : MonoBehaviour
{
    //Used Variables
    public List<Event> eventList = new List<Event>(); //List of events happening during the game
    public List<GameObject> existingObjects = new List<GameObject>(); // List of all current gameobjects
    public GameObject[] spritePrefabs; //Array of prefabs, which can be instantiated by the central brain to create objects and characters

    // Start is called before the first frame update
    void Start()
    {
        string filePath = Application.streamingAssetsPath;
        if (!Directory.Exists(filePath + "/SaveFiles"))
        {
            Directory.CreateDirectory(filePath + "/SaveFiles");
        }
        string saveFile = filePath + "/SaveFiles/gamesave.txt";
        string levelFile = filePath + "/Levels/leveloutline.txt";

        if (File.Exists(saveFile))
        {
            ReadTextFile(saveFile); //Read file containing level architecture from gamesave
        }
        else
        {
            ReadTextFile(levelFile); //Read file containing level architecture from initial level design
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        //Build a list containing the world state determined by the events in eventList
        List<Event> worldstateList = new List<Event>();

        foreach (var currentEvent in eventList)
        {

            if (currentEvent.Command == "spawn") //Add event to world state if it contains the SPAWN command
            {
                worldstateList.Add(currentEvent);
            }
            else if (currentEvent.Command == "destroy") //Remove event from world state if it contains the DELETE command
            {
                worldstateList.RemoveAll(world => world.ChosenObject == currentEvent.ChosenObject && world.Position == currentEvent.Position);
            }
        }

        //Change level based on newly built world state (unique objects have a unique name and position combination)

        // Check if each element in worldstatelist already exists in existingobjects - if NOT then spawn
        foreach (var currentEvent in worldstateList)
        {
            if (!existingObjects.Any(oneObject => oneObject.name == currentEvent.ChosenObject && oneObject.transform.position == currentEvent.Position)) // 
            {
                SpawnSprite(currentEvent);
            }
        }

        // Check if each element in existingobjects already exists in worldstatelist - if NOT then destroy
        foreach (var currentObject in existingObjects.ToList())
        {
            if (!worldstateList.Any(oneEvent => oneEvent.ChosenObject == currentObject.name && oneEvent.Position == currentObject.transform.position))
            {
                Destroy(currentObject);
                existingObjects.RemoveAll(obj => obj.name == currentObject.name && obj.transform.position == currentObject.transform.position);
            }
        }

    }

    //Read File to build level - transfer events from file to eventlist
    void ReadTextFile(string file_path)
    {
        string[] startLevel = File.ReadAllLines(file_path);

        foreach (var oneLine in startLevel)
        {
            string trimmedLine = oneLine.Trim(' ', '\r', '\n');
            string[] words = trimmedLine.Split(';');
            string loadedCommand = words[0];
            string loadedObject = words[1];
            string[] loadedPosition = words[2].Split(',');
            Vector3 loadedVector = new Vector3(Convert.ToSingle(loadedPosition[0]), Convert.ToSingle(loadedPosition[1]), Convert.ToSingle(loadedPosition[2]));
            eventList.Add(new Event {Command=loadedCommand, ChosenObject=loadedObject, Position=loadedVector});
        }
    }

    //Spawn a sprite based on the informations delivered by an event
    void SpawnSprite(Event oneEvent)
    {
        var spriteprefabNames = Array.ConvertAll(spritePrefabs, item => (string)item.name);
        int keyIndex = Array.IndexOf(spriteprefabNames, oneEvent.ChosenObject);
        var clone = Instantiate(spritePrefabs[keyIndex], oneEvent.Position, spritePrefabs[keyIndex].transform.rotation);
        clone.name = oneEvent.ChosenObject;
        existingObjects.Add(clone);
    }

}

//holding space for code
//