#nullable enable
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using UnityEngine.UI;
using static System.Math;
using Sy = System;
using CE = Conversation.Editor;
using Conversation.Editor;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

public class Event
{
    public string? Command { get; set; }
    public string? ChosenObject { get; set; }
    public Vector3 Position { get; set; }
}

public static class Revolver
{
    const string revolverTextTag = "RevolverText";
    const string revolverTimerTextTag = "RevolverTimerText";

    static CE.Conversation? conversation;
    static CE.ContentNode? currentNode;
    static ImmutableList<ImmutableList<LinePrinter>> printers = ImmutableList<ImmutableList<LinePrinter>>.Empty;
    static ImmutableList<int> selectedLines = ImmutableList<int>.Empty;

    static int SelectedRoll = 0;
    static int SelectedLine(int rollIx) => selectedLines.ElementAtOrDefault(rollIx);

    static CancellationTokenSource? cancellationTokenSource;

    const int numberOfTimeoutSteps = 10;
    const int timeoutStep_ms = 1_000;

    // Unity does not want GameObjects to be modified in the thread pool.
    static ConcurrentQueue<Sy.Action> updates = new ConcurrentQueue<Sy.Action>();

    internal static void Update()
    {
        if (updates.TryDequeue(out Sy.Action result))
        {
            result.Invoke();
        }
    }

    static IEnumerable<string> fulfilledSubTexts
        => SubNodes
        .Where(PrecondsFulfilled)
        .Select(node => node.conversationText);

    static IEnumerable<ContentNode> SubNodes
    => currentNode?.subNodes ?? conversation?.subNodes ?? new List<ContentNode>();

    static bool PrecondsFulfilled(CE.ContentNode node)
    {
        string? preconds = node.GetPreconds();
        return string.IsNullOrWhiteSpace(preconds) || CentralBrain.eventList.Select(x => x.ChosenObject).Contains(preconds);
    }

    static string? GetPreconds(this CE.ContentNode node)
    => node.additionalData.GetPreconds();

    static string? GetPreconds(this CE.Conversation root)
    => root.additionalData.GetPreconds();

    static string? GetPreconds(this List<Info> additionalData)
    => additionalData.Where(x => "preconds" == x.variableName).Select(x => x.variableValue).FirstOrDefault();

    static bool HasTimerPrecond(this CE.ContentNode node)
    => node.GetPreconds()?.Contains("timer") ?? false;

    static bool IsTimerNode()
    => SubNodes.Aggregate(false, (s, n) => s || n.HasTimerPrecond());

    static CE.ContentNode? GetTimerSubNode()
    => SubNodes.Where(n => n.HasTimerPrecond()).FirstOrDefault();

    internal static void LoadAConversation(string filename)
    {
        string pathToConversation = Application.streamingAssetsPath + "/Conversations/" + filename;
        if (pathToConversation is null) return;
        conversation = Conversation.Editor.Conversation.GetConversation(pathToConversation);
        SetCurrentNodeAndPrint(null);
    }

    delegate void SetTimeoutText(string text);

    static void SetCurrentNodeAndPrint(ContentNode? node)
    {
        currentNode = node;
        ResetRollPrinters();
        PrintRolls();

        cancellationTokenSource?.Cancel();
        if (IsTimerNode())
        {
            cancellationTokenSource = new CancellationTokenSource();
            SetTimeoutText setText = AddTimeoutTextField();
            Task.Run(() => TimeoutTask(setText, cancellationTokenSource.Token), cancellationTokenSource.Token);
        }
    }

    static async Task TimeoutTask(SetTimeoutText setTimeoutText, CancellationToken token)
    {
        int k = numberOfTimeoutSteps;
        while (0 < k && !token.IsCancellationRequested)
        {
            updates.Enqueue(() => setTimeoutText(k.ToString()));
            try
            {
                await Task.Delay(timeoutStep_ms, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            k--;
        }
        if (!token.IsCancellationRequested)
        {
            // Timeout reached because the player has not clicked space.
            updates.Enqueue(() => SetCurrentNodeAndPrint(GetTimerSubNode()));
        }
        updates.Enqueue(DestroyOldTimeoutTextField);
    }

    static void DestroyOldTimeoutTextField()
    {
        foreach (GameObject go in GameObject.FindGameObjectsWithTag(revolverTimerTextTag))
        {
            GameObject.Destroy(go);
        }
    }

    static SetTimeoutText AddTimeoutTextField()
    {
        GameObject textGo = new GameObject();
        textGo.transform.parent = canvasGo?.transform;
        textGo.name = revolverTimerTextTag;
        textGo.tag = revolverTimerTextTag;

        Text text = textGo.AddComponent<Text>();
        text.font = arial;
        text.fontSize = 50;
        text.fontStyle = FontStyle.Italic;
        text.color = Color.gray;
        text.alignment = TextAnchor.UpperCenter;

        Rect? subRect = canvasGo?.GetComponent<RectTransform>()?.rect
           .LowerThird()
           .SubRect(1, 3, 0, 1);

        if (subRect.HasValue)
        {
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.localPosition = subRect.Value.position;
            textRect.sizeDelta = subRect.Value.size;
        }
        return x => text.text = x;
    }

    delegate void LinePrinter(bool activeRoll, bool activeLine);

    internal static void PrintRolls()
    {
        DestroyOldText();
        for (int rollIx = 0; rollIx < printers.Count; rollIx++)
        {
            ImmutableList<LinePrinter> roll = printers[rollIx];
            for (int lineIx = 0; lineIx < roll.Count; lineIx++)
            {
                LinePrinter linePrinter = roll[lineIx];
                bool isSelectedRoll = SelectedRoll == rollIx;
                bool isSelectedLine = SelectedLine(rollIx) == lineIx; ;
                linePrinter(isSelectedRoll, isSelectedLine);
            }
        }
    }


    static void ResetRollPrinters()
    {
        ImmutableList<ImmutableList<string>> rolls = GetRolls();
        printers = GetLinePrinters(rolls);
        selectedLines = printers.Select(x => 0).ToImmutableList();
    }

    static ImmutableList<ImmutableList<LinePrinter>> GetLinePrinters(ImmutableList<ImmutableList<string>> rolls)
    => rolls.Select((lines, rollIx) => GetLinePrinters(rolls.Count, rollIx, lines)).ToImmutableList();

    static ImmutableList<LinePrinter> GetLinePrinters(int rollCount, int rollIx, ImmutableList<string> lines)
     => lines.Select<string, LinePrinter>((line, lineIx)
     => (bool activeRoll, bool activeLine)
     =>
     {
         int offset = lineIx - SelectedLine(rollIx);
         int linecountThree = Min(lines.Count, 3);

         if (1 < Abs(offset))
         {
             return;
         }
         GameObject textGo = new GameObject();
         textGo.transform.parent = canvasGo?.transform;
         textGo.name = revolverTextTag;
         textGo.tag = revolverTextTag;

         Text text = textGo.AddComponent<Text>();
         text.font = arial;
         text.fontSize = 50;
         text.fontStyle = activeRoll ? FontStyle.Bold : FontStyle.Normal;
         text.color = activeLine ? Color.yellow : Color.gray;
         text.text = line;
         text.alignment = TextAnchor.UpperCenter;


         Rect? subRect = canvasGo?.GetComponent<RectTransform>()?.rect
            .LowerThird()
            .SubRect(rollCount, linecountThree, rollIx, (offset + 3) % linecountThree);

         if (subRect.HasValue)
         {
             RectTransform textRect = textGo.GetComponent<RectTransform>();
             textRect.localPosition = subRect.Value.position;
             textRect.sizeDelta = subRect.Value.size;
         }
     }).ToImmutableList();

    static Rect LowerHalf(this Rect rect) => LowerSubRect(rect, 2);
    static Rect LowerThird(this Rect rect) => LowerSubRect(rect, 3);
    static Rect UpperThird(this Rect rect) => UpperSubRect(rect, 3);

    static Rect LowerSubRect(this Rect rect, float fraction)
    => new Rect(rect.x, rect.y, rect.width, rect.height / fraction);

    static Rect UpperSubRect(this Rect rect, float fraction)
    => new Rect(rect.x, rect.y + rect.height / fraction, rect.width, rect.height / fraction);

    static Rect SubRect(this Rect rect, int rollCount, int lineCount, int rollIx, int lineIx)
    {
        float superWidth = rect.width;
        float superHeight = rect.height;
        float width = superWidth / rollCount;
        float height = superHeight / lineCount;
        float x = rect.x + width / 2f + width * rollIx;
        float y = rect.y + height / 2f + lineIx * height;
        return new Rect(x, y, width, height);
    }

    public static void Up()
    {
        selectedLines = selectedLines.Select((ix, k) => SelectedRoll != k ? ix : Constrain(++ix, GetRollCount(k))).ToImmutableList();
        PrintRolls();
    }

    public static void Down()
    {
        selectedLines = selectedLines.Select((ix, k) => SelectedRoll != k ? ix : Constrain(--ix, GetRollCount(k))).ToImmutableList();
        PrintRolls();
    }

    static int GetRollCount(int k) => GetRolls().Select(r => r.Count).ElementAtOrDefault(k);

    public static void Left()
    {
        SelectedRoll = Constrain(--SelectedRoll, selectedLines.Count);
        PrintRolls();
    }

    public static void Right()
    {
        SelectedRoll = Constrain(++SelectedRoll, selectedLines.Count);
        PrintRolls();
    }

    public static void Space()
    {
        Sy.Func<ContentNode, bool> FitsPhrase = node => node.conversationText.Replace("/", "").Equals(Phrase);

        ContentNode? node = SubNodes.Where(FitsPhrase).FirstOrDefault();
        if (null != node)
        {
            SetCurrentNodeAndPrint(node);
        }
    }


    /// <summary>
    /// The selected phrase of the revolver without slashes.
    /// </summary>
    /// <returns>The selected phrase. Empty in case of error.</returns>
    public static string Phrase => GetPhrase();

    static string GetPhrase()
    {
        ImmutableList<string> selection = GetRolls().Zip(selectedLines, (roll, ix) => roll[ix]).ToImmutableList();
        string phrase = string.Join("", selection);
        return phrase;
    }


    /// <summary>
    /// Whether the conversation has reached a leaf of the conversation tree.
    /// If so, it cannot continue
    /// </summary>
    /// <returns>False if the conversation could continue.</returns>
    public static bool CannotContinue => !SubNodes.Any();

    static int Constrain(int index, int count)
    => count <= 0 ? Constrain(index, 1)
        : index < 0 ? Constrain(index + count, count) : index % count;

    static ImmutableList<ImmutableList<string>> GetRolls()
    => SplitInsert(fulfilledSubTexts.ToImmutableList());


    static void DestroyOldText()
    {
        foreach (GameObject go in oldRevolverTexts)
        {
            GameObject.Destroy(go);
        }
    }

    static ImmutableList<GameObject> oldRevolverTexts
    => GameObject.FindGameObjectsWithTag(revolverTextTag).ToImmutableList();


    static Font arial => (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");

    static GameObject? canvasGo => GameObject.FindGameObjectsWithTag("Canvas").FirstOrDefault();

    static Text uiText => Object.FindObjectsOfType<Text>().FirstOrDefault();

    static ImmutableList<string> Flatten(ImmutableList<ImmutableList<string>> split)
    => 0 == split.Count() ? ImmutableList<string>.Empty : Flatten(split[0], split.RemoveAt(0));

    static ImmutableList<string> Flatten(ImmutableList<string> acc, ImmutableList<ImmutableList<string>> split)
     => 0 == split.Count()
        ? acc
        : Flatten(acc.Join(split[0], unit, unit, (a, b) => a + b).ToImmutableList(), split.RemoveAt(0));

    static Sy.Func<string, string> id => x => x;
    static Sy.Func<string, bool> unit => x => true;

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
    public static List<Event> eventList = new List<Event>(); //List of events happening during the game
    public List<GameObject> existingObjects = new List<GameObject>(); // List of all current gameobjects
    public GameObject[] spritePrefabs = new GameObject[0]; //Array of prefabs, which can be instantiated by the central brain to create objects and characters
    public static bool inConversation = false; //State variable determining, if player in conversation
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
        //Variable determining in which conversation the player is right now
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
        else if (currentConversation.Command == null && inConversation)
        {
            inConversation = false;
        }

        //Display conversation if player in conversation
        if (inConversation)
        {

            //Control during conversation
            if (Input.GetKeyUp(KeyCode.UpArrow))
            {
                Revolver.Up();
            }
            if (Input.GetKeyUp(KeyCode.DownArrow))
            {
                Revolver.Down();
            }
            if (Input.GetKeyUp(KeyCode.LeftArrow))
            {
                Revolver.Left();
            }
            if (Input.GetKeyUp(KeyCode.RightArrow))
            {
                Revolver.Right();
            }
            if (Input.GetKeyUp(KeyCode.Space))
            {
                Revolver.Space();

                if (Revolver.CannotContinue)
                {
                    eventList.Add(new Event { Command = "stopConversation", ChosenObject = "TheEnd", Position = new Vector3(0, 0, 0) });
                }
                else
                {
                    string chosenMessage = Revolver.Phrase;
                    eventList.Add(new Event { Command = "chosenAnswer", ChosenObject = chosenMessage, Position = new Vector3(0, 0, 0) });
                }
            }

            Revolver.Update();
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
            Vector3 loadedVector = new Vector3(Sy.Convert.ToSingle(loadedPosition[0]), Sy.Convert.ToSingle(loadedPosition[1]), Sy.Convert.ToSingle(loadedPosition[2]));
            eventList.Add(new Event { Command = loadedCommand, ChosenObject = loadedObject, Position = loadedVector });
        }
    }

    //Spawn a sprite based on the informations delivered by an event
    void SpawnSprite(Event oneEvent)
    {
        var spriteprefabNames = Sy.Array.ConvertAll(spritePrefabs, item => (string)item.name);
        int keyIndex = Sy.Array.IndexOf(spriteprefabNames, oneEvent.ChosenObject);
        var clone = Instantiate(spritePrefabs[keyIndex], oneEvent.Position, spritePrefabs[keyIndex].transform.rotation);
        clone.name = oneEvent.ChosenObject;
        existingObjects.Add(clone);
    }

}

#nullable disable