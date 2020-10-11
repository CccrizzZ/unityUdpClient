using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using System.Net.Sockets;
using System.Net;




/// A class that takes care of talking to our server
public class NetworkMan : MonoBehaviour
{
    public UdpClient udp; // an instance of the UDP client
    public GameObject playerGO; // our player object

    public string myAddress; // my address = (IP, PORT)
    public Dictionary<string,GameObject> currentPlayers; // A list of currently connected players
    public List<string> newPlayers, droppedPlayers; // a list of new players, and a list of dropped players
    public GameState lastestGameState; // the last game state received from server
    public ListOfPlayers initialSetofPlayers; // initial set of players to spawn
    
    public MessageType latestMessage; // the last message received from the server

    public float currX;
    public float currY;
    public float currZ;



    // Start is called before the first frame update
    void Start()
    {
        // Initialize variables
        newPlayers = new List<string>();
        droppedPlayers = new List<string>();
        currentPlayers = new Dictionary<string, GameObject>();
        initialSetofPlayers = new ListOfPlayers();


        // Connect to the client.
        udp = new UdpClient();
        Debug.Log("Connecting...");
        udp.Connect("3.96.203.122",12345);

        // sent "connect" key to server
        Byte[] sendBytes = Encoding.ASCII.GetBytes("connect");
        udp.Send(sendBytes, sendBytes.Length);

        // receive message from the server
        udp.BeginReceive(new AsyncCallback(OnReceived), udp);

        // Repeat calling (Method, time, repeatRate)
        // 
        InvokeRepeating("HeartBeat", 1, 1);
        // send position
        InvokeRepeating("SendPosition", 1, 0.04f);
    }

    void OnDestroy()
    {
        udp.Dispose();
    }










    /// A structure that replicates our server color dictionary
    [Serializable]
    public struct receivedColor
    {
        public float R;
        public float G;
        public float B;
    }


    //  for sending position of THIS client to the server
    [Serializable]
    public struct currentPosition
    {
        public Vector3 position;
    }

        


    /// A structure that replicates our player dictionary on server
    [Serializable]
    public class Player
    {
        public string id;
        public receivedColor color;
        public currentPosition position;
    }

    /// A structure that replicates our player list on server
    [Serializable]
    public class ListOfPlayers
    {
        public Player[] players;

        public ListOfPlayers()
        {
            players = new Player[0];
        }
    }

    /// A structure that replicates dropped player name on server
    [Serializable]
    public class ListOfDroppedPlayers
    {
        public string[] droppedPlayers;
    }


    /// A structure that replicates our game state dictionary on server
    [Serializable]
    public class GameState
    {
        public int pktID;
        public Player[] players;
    }


    /// A structure that replicates the mesage dictionary on our server
    [Serializable]
    public class MessageType
    {
        public commands cmd;
    }


    /// Ordererd enums for our cmd values
    public enum commands
    {
        PLAYER_CONNECTED,       //0
        GAME_UPDATE,            // 1
        PLAYER_DISCONNECTED,    // 2
        CONNECTION_APPROVED,    // 3
        LIST_OF_PLAYERS,        // 4
    };
    

    // callback function called every time the server updates
    void OnReceived(IAsyncResult result){
        // this is what had been passed into BeginReceive as the second parameter:
        UdpClient socket = result.AsyncState as UdpClient;
        
        // points towards whoever had sent the message:
        IPEndPoint source = new IPEndPoint(0, 0);

        // get the actual message and fill out the source:
        byte[] message = socket.EndReceive(result, ref source);
        
        // do what you'd like with `message` here:
        string returnData = Encoding.ASCII.GetString(message);
     
     
        ///////////////////////////////////////////
        // add location of all clients in this  ///
        // receive data from server              ///
        ///////////////////////////////////////////
        latestMessage = JsonUtility.FromJson<MessageType>(returnData);
        Debug.Log(returnData);
    

        try{
            switch(latestMessage.cmd){
                // cmd: 0
                case commands.PLAYER_CONNECTED:
                    ListOfPlayers latestPlayer = JsonUtility.FromJson<ListOfPlayers>(returnData);
                    Debug.Log(returnData);
                    foreach (Player player in latestPlayer.players)
                    {
                        newPlayers.Add(player.id);
                    }
                    break;
                // cmd: 1
                case commands.GAME_UPDATE:
                    lastestGameState = JsonUtility.FromJson<GameState>(returnData);
                    break;
                // cmd: 2
                case commands.PLAYER_DISCONNECTED:
                    ListOfDroppedPlayers latestDroppedPlayer = JsonUtility.FromJson<ListOfDroppedPlayers>(returnData);
                    foreach (string player in latestDroppedPlayer.droppedPlayers)
                    {
                        droppedPlayers.Add(player);
                    }
                    break;
                // cmd: 3
                case commands.CONNECTION_APPROVED:
                    // print all player in the server
                    ListOfPlayers myPlayer = JsonUtility.FromJson<ListOfPlayers>(returnData);
                    Debug.Log(returnData);

                    // set ID from server to this client
                    foreach (Player player in myPlayer.players)
                    {
                        newPlayers.Add(player.id);
                        myAddress = player.id;
                    }
                    break;
                // cmd: 4
                case commands.LIST_OF_PLAYERS:
                    initialSetofPlayers = JsonUtility.FromJson<ListOfPlayers>(returnData);
                    Debug.Log(initialSetofPlayers.players);
                    break; 
                default:
                    Debug.Log("Error: " + returnData);
                    break;
            }
        }
        catch (Exception e){
            Debug.Log(e.ToString());
        }
        
        // schedule the next receive operation once reading is done:
        socket.BeginReceive(new AsyncCallback(OnReceived), socket);
    }

    void SpawnPlayers()
    {
        if (newPlayers.Count > 0)
        {
            foreach (string playerID in newPlayers)
            {
                currentPlayers.Add(playerID,Instantiate(playerGO, new Vector3(0,0,0),Quaternion.identity));
                currentPlayers[playerID].name = playerID;
            }
            newPlayers.Clear();
        }
        if (initialSetofPlayers.players.Length > 0)
        {
            Debug.Log(initialSetofPlayers);
            
            foreach (Player player in initialSetofPlayers.players)
            {
                if (player.id == myAddress)
                {
                    continue;
                }
                
                currentPlayers.Add(player.id, Instantiate(playerGO, new Vector3(0,0,0), Quaternion.identity));
                currentPlayers[player.id].GetComponent<Renderer>().material.color = new Color(player.color.R, player.color.G, player.color.B);
                currentPlayers[player.id].name = player.id;
            }
            initialSetofPlayers.players = new Player[0];
        }
    }


    // update all coneected player
    void UpdatePlayers()
    {
        if (lastestGameState.players.Length >0)
        {
            foreach (NetworkMan.Player player in lastestGameState.players)
            {
                // set player id
                string playerID = player.id;

                // set player color
                currentPlayers[player.id].GetComponent<Renderer>().material.color = new Color(player.color.R,player.color.G,player.color.B);

                // set player position to NWM
                currX = currentPlayers[player.id].GetComponent<Transform>().position.x;
                currY = currentPlayers[player.id].GetComponent<Transform>().position.y;
                currZ = currentPlayers[player.id].GetComponent<Transform>().position.z;



            }
            lastestGameState.players = new Player[0];
        }
    }

    // if the player dropped, destroy it
    void DestroyPlayers()
    {
        if (droppedPlayers.Count > 0)
        {
            foreach (string playerID in droppedPlayers)
            {
                Debug.Log(playerID);
                Debug.Log(currentPlayers[playerID]);
                Destroy(currentPlayers[playerID].gameObject);
                currentPlayers.Remove(playerID);
            }
            droppedPlayers.Clear();
        }
    }
    
    ///////////////////////////////////////////
    // sending data to server               ///
    ///////////////////////////////////////////
    void HeartBeat()
    {
        Byte[] sendBytes = Encoding.ASCII.GetBytes("heartbeat");
        udp.Send(sendBytes, sendBytes.Length);
    }

    // send player position on this client to server
    void SendPosition()
    {
        // create pos object
        currentPosition currPos = new currentPosition();

        // sync with player movement
        currPos.position.x = currX;
        currPos.position.y = currY;
        currPos.position.z = currZ;


        // Debug.Log($"{currX},{currY}, {currZ}");


        // send to server
        Byte[] senddata = Encoding.ASCII.GetBytes(JsonUtility.ToJson(currPos));
        udp.Send(senddata, senddata.Length);

    }


    void Update()
    {
        SpawnPlayers();
        UpdatePlayers();
        DestroyPlayers();
    }
}