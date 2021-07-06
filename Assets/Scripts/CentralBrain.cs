using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using UnityEngine.UI;
using static System.Math;
using sy = System;
using CE = Conversation.Editor;
using Conversation.Editor;
using System.Collections.Immutable;

public class Event
{
    public string Command { get; set; }
    public string ChosenObject { get; set; }
    public Vector3 Position { get; set; }
}

public static class Revolver
{

    static CE.Conversation conversation;
    static CE.ContentNode currentNode;
    static IEnumerable<CE.ContentNode> nodes = new List<CE.ContentNode>();

    static IEnumerable<string> subNodeTexts => nodes
        .Where(PrecondsFulfilled)
        .Select(node => node.conversationText);

    static bool PrecondsFulfilled(CE.ContentNode node) => true;
    // {
    //      string? preconds = GetPreconds(node);
    //     return string.IsNullOrWhiteSpace(preconds) || Brain.Get().HasEventId(preconds);
    // }

    //static string? GetPreconds(CE.ContentNode node)
    // => node.additionalData.Where(x => "preconds" == x.variableName).Select(x => x.variableValue).FirstOrDefault();

    //static IEnumerable<string> subNodeTexts => conversation?.subNodes.AsEnumerable().Select(node => node.conversationText) ?? new List<string>();
    //static IEnumerable<string> subNodeTexts2 => nodes.

    static int subIndex = 0;


    public static void LoadAConversation(string filename)
    {
        if (uiText is null) return;
        uiText.text = "Loading conversation ...";

        string pathToConversation = Application.streamingAssetsPath + "/Conversations/" + filename;
        if (pathToConversation is null) return;

        conversation = Conversation.Editor.Conversation.GetConversation(pathToConversation);
        nodes = conversation.subNodes;
    }

    public static void Up()
    {
        subIndex--;
    }

    public static void Down()
    {
        subIndex++;
    }

    public static void Left()
    {

    }


    public static void Right()
    {
        List<ContentNode> subNodes = GetCurrentSubNodes();
        if (subNodes.Any())
        {
            nodes = subNodes;
            subIndex = 0;
        }
    }

    static List<ContentNode> GetCurrentSubNodes()
    => GetCurrentNode()?.subNodes ?? Empty;

    static List<CE.ContentNode> Empty => new List<ContentNode>();

    static ContentNode GetCurrentNode()
    {
        List<CE.ContentNode> theNodes = nodes.ToList();
        int count = theNodes.Count;
        int index = Constrain(subIndex, count);
        return theNodes.ElementAtOrDefault(index);
    }

    // public static void Left()
    // {
    //     List<CE.ContentNode> theNodes = nodes.ToList();
    //     if (!theNodes.Any())
    //     {
    //         return;
    //     }
    //     int count = theNodes.Count;
    //     int index = Constrain(subIndex, count);
    //     //theNodes[index].
    // }

    static int Constrain(int index, int count)
    => index < 0 ? Constrain(index + count, count) : index % count;

    public static void Display()
    {
        List<string> changedMessages = subNodeTexts.ToList();
        if (changedMessages.Any())
        {
            int index = Constrain(subIndex, changedMessages.Count);
            changedMessages[index] = "->" + changedMessages[index];
            Display(changedMessages);
        }
    }

    public static void Display(IEnumerable<string> messages)
    {
        if (null != uiText) uiText.text = string.Join(sy.Environment.NewLine, messages);
    }

    static Text uiText => Object.FindObjectsOfType<Text>().FirstOrDefault();


    static ImmutableList<string> Flatten(ImmutableList<ImmutableList<string>> split)
    => 0 == split.Count() ? ImmutableList<string>.Empty : Flatten(split[0], split.RemoveAt(0));

    static ImmutableList<string> Flatten(ImmutableList<string> acc, ImmutableList<ImmutableList<string>> split)
     => 0 == split.Count()
        ? acc
        : Flatten(acc.Join(split[0], unit, unit, (a, b) => a + b).ToImmutableList(), split.RemoveAt(0));

    static sy.Func<string, string> id => x => x;
    static sy.Func<string, bool> unit => x => true;

    static ImmutableList<ImmutableList<string>> SplitInsert(ImmutableList<string> nodeTexts)
    => SplitInsertMutable(nodeTexts).Aggregate(ImmutableList<ImmutableList<string>>.Empty, (acc, next) => acc.Add(next.ToImmutableList()));

    static List<List<string>> SplitInsertMutable(ImmutableList<string> nodeTexts)
    {
        return nodeTexts.Aggregate(new List<List<string>>(), (s, next) =>
        {
            string[] splitText = next.Split('/');
            while (s.Count() < splitText.Length) s.Add(new List<string>());

            for (int k = 0; k < splitText.Length; k++)
            {
                string fragment = splitText[k];
                List<string> stack = s[k];
                if (!stack.Contains(fragment))
                {
                    stack.Add(fragment);
                }
            }
            return s;
        });
    }
}

public class CentralBrain : MonoBehaviour
{
    //Used Variables
    public List<Event> eventList = new List<Event>(); //List of events happening during the game
    public List<GameObject> existingObjects = new List<GameObject>(); // List of all current gameobjects
    public GameObject[] spritePrefabs; //Array of prefabs, which can be instantiated by the central brain to create objects and characters
    public bool inConversation = false; //State variable determining, if player in conversation

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
        //State variable determining, if player is in a conversation
        Event currentConversation = new Event();

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
            else if (currentEvent.Command == "startConversation")
            {
                currentConversation = currentEvent;
            }
            else if (currentEvent.Command == "stopConversation")
            {
                currentConversation = new Event();
            }

        }

        //Change level based on newly built world state (unique objects have a unique name and position combination)

        //Check if each element in worldstatelist already exists in existingobjects - if NOT then spawn
        foreach (var currentEvent in worldstateList)
        {
            if (!existingObjects.Any(oneObject => oneObject.name == currentEvent.ChosenObject && oneObject.transform.position == currentEvent.Position)) // 
            {
                SpawnSprite(currentEvent);
            }
        }

        //Check if each element in existingobjects already exists in worldstatelist - if NOT then destroy
        foreach (var currentObject in existingObjects.ToList())
        {
            if (!worldstateList.Any(oneEvent => oneEvent.ChosenObject == currentObject.name && oneEvent.Position == currentObject.transform.position))
            {
                Destroy(currentObject);
                existingObjects.RemoveAll(obj => obj.name == currentObject.name && obj.transform.position == currentObject.transform.position);
            }
        }

        //Start conversation if an event is saved in currentConversation and player isn't in a conversation already (to avoid restarting the conversation)
        if (currentConversation.Command != null && !inConversation)
        {
            Revolver.LoadAConversation(currentConversation.ChosenObject + "Conversation.xml");
            inConversation = true;
        }

        //Display conversation if player in conversation
        if (inConversation)
        {
            Revolver.Display();
            //Control during conversation
            if (Input.GetKeyUp(KeyCode.UpArrow))
            {
                Revolver.Up();
            }

            if (Input.GetKeyUp(KeyCode.DownArrow))
            {
                Revolver.Down();
            }
            if (Input.GetKeyUp(KeyCode.RightArrow))
            {
                Revolver.Right();
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
            Vector3 loadedVector = new Vector3(sy.Convert.ToSingle(loadedPosition[0]), sy.Convert.ToSingle(loadedPosition[1]), sy.Convert.ToSingle(loadedPosition[2]));
            eventList.Add(new Event { Command = loadedCommand, ChosenObject = loadedObject, Position = loadedVector });
        }
    }

    //Spawn a sprite based on the informations delivered by an event
    void SpawnSprite(Event oneEvent)
    {
        var spriteprefabNames = sy.Array.ConvertAll(spritePrefabs, item => (string)item.name);
        int keyIndex = sy.Array.IndexOf(spriteprefabNames, oneEvent.ChosenObject);
        var clone = Instantiate(spritePrefabs[keyIndex], oneEvent.Position, spritePrefabs[keyIndex].transform.rotation);
        clone.name = oneEvent.ChosenObject;
        existingObjects.Add(clone);
    }

}

//holding space for code
//