// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using Yoyo.Attributes;

namespace Yoyo.Runtime
{
    public enum YoyoEnvironment
    {
        None,
        Client,
        Server,
    }

	public partial class YoyoSession : MonoBehaviour
	{
        [Header("Session Options")]
		[SerializeField] private string _ipAddress = default;
        [SerializeField] private int _port = 0;
        [SerializeField, Range(1, 128)] 
		private int _maxConnections = 32;
        public float MasterTimer = .05f;
        
        [Header("Session State")]
        [SerializeField, DisableEditing]
        private YoyoEnvironment _environment = YoyoEnvironment.None;
        [SerializeField, DisableEditing]
        private int _localPlayerId = -1;
        [SerializeField, DisableEditing]
        private bool _isConnected = false;
        [SerializeField, DisableEditing]
        private bool _canJoin = true;
        [SerializeField, DisableEditing]
        private bool _currentlyConnecting = false;

        public Socket TCP_Listener;
        public Dictionary<int, TcpConnection> Connections;
        public Dictionary<int, NetworkIdentifier> NetObjs;

        //Game Object variables
        public int ObjectCounter = 0;
        public int ConCounter = 0;
        public GameObject[] SpawnPrefab;

          
        //WE are going to push a variable to notify the master an ID has a message.
        public bool MessageWaiting = false;
        Coroutine ListeningThread;
        public string MasterMessage;
        public GameObject NetworkPlayerManager;//This will be the first thing that is spawned!

        //Locks
        public object _conLock = new object();
        public object _objLock = new object();
        public object _masterMessage = new object();
        public object _waitingLock = new object();
        public DateTime StartConnection;


        public YoyoEnvironment Environment => _environment;

        public bool IsConnected { get => _isConnected; private set => _isConnected = value; }
        public bool CanJoin { get => _canJoin; private set => _canJoin = value; }
        public bool CurrentlyConnecting { get => _currentlyConnecting; private set => _currentlyConnecting = value; }
        public int LocalPlayerId { get => _localPlayerId; set => _localPlayerId = value; }

        // Use this for initialization
        void Start()
        {
            _environment = YoyoEnvironment.None;
            IsConnected = false;
            CurrentlyConnecting = false;
            //ipAddress = "127.0.0.1";//Local host
            if (_ipAddress == "")
            {
                _ipAddress = "127.0.0.1";//Local host
            }
            if (_port == 0)
            {
                _port = 9001;
            }
            Connections = new Dictionary<int, TcpConnection>();
            NetObjs = new Dictionary<int, NetworkIdentifier>();
        }


        /// <summary>
        /// Server Functions
        /// StartServer -> Initialize Listener and Slow Update
        ///     - WIll spawn the first prefab as a "NetworkPlayerManager"
        /// Listen -> Will bind to a port and allow clients to join.
        /// </summary>
        public void StartServer()
        {
            if (!IsConnected)
            {
                ListeningThread = StartCoroutine(Listen());
                StartCoroutine(SlowUpdate());
            }
        }

        public void StopListening()
        {
            if(Environment == YoyoEnvironment.Server && CanJoin)
            {   
                CurrentlyConnecting = false;
                StopCoroutine(ListeningThread);
                TCP_Listener.Close();
            }
        }

        public IEnumerator Listen()
        {
            //If we are listening then we are the server.
            _environment = YoyoEnvironment.Server;
            IsConnected = true;
            LocalPlayerId = -1; //For server the localplayer id will be -1.
                                //Initialize port to listen to
                                
            IPAddress ip = (IPAddress.Any);
            IPEndPoint endP = new IPEndPoint(ip, _port);
            //We could do UDP in some cases but for now we will do TCP
            TCP_Listener = new Socket(ip.AddressFamily,SocketType.Stream, ProtocolType.Tcp);

            //Now I have a socket listener.
            TCP_Listener.Bind(endP);
            TCP_Listener.Listen(_maxConnections);

            while(CanJoin)
            {
                CurrentlyConnecting = false;
                
                TCP_Listener.BeginAccept(new System.AsyncCallback(this.ListenCallBack), TCP_Listener);               
                yield return new WaitUntil(() => CurrentlyConnecting);
                DateTime time2 = DateTime.Now;
                TimeSpan timeS = time2 - StartConnection;

                CurrentlyConnecting = false;
                if (Connections.ContainsKey(ConCounter - 1) == false)
                {
                    //Connection was not fully established.
                    continue;
                }
                yield return new WaitForSeconds(2*(float)timeS.TotalSeconds);
                Connections[ConCounter - 1].Send(Encoding.ASCII.GetBytes("PLAYERID#" + Connections[ConCounter - 1].PlayerId + "\n"));
                //Start Server side listening for client messages.
                StartCoroutine(Connections[ConCounter - 1].TCPRecv());

                //Udpate all current network objects
                foreach (KeyValuePair<int,NetworkIdentifier> entry in NetObjs)
                {//This will create a custom create string for each existing object in the game.
                    string tempRot = entry.Value.transform.rotation.ToString();
                    tempRot = tempRot.Replace(',', '#');
                    tempRot = tempRot.Replace('(', '#');
                    tempRot = tempRot.Replace(')', '\0');            

                    string MSG = "CREATE#" + entry.Value.Type + "#" + entry.Value.Owner +
                   "#" + entry.Value.NetId + "#" + entry.Value.transform.position.x.ToString("n2") + 
                   "#" + entry.Value.transform.position.y.ToString("n2") + "#" 
                   + entry.Value.transform.position.z.ToString("n2") + tempRot+"\n";
                    Connections[ConCounter - 1].Send(Encoding.ASCII.GetBytes(MSG));
                }
                //Create NetworkPlayerManager
                NetCreateObject(-1, ConCounter - 1, new Vector3(Connections[ConCounter -1].PlayerId*2-3,0,0));
                yield return new WaitForSeconds(.1f);
            }
        }
        public void ListenCallBack(System.IAsyncResult ar)
        {
            StartConnection = DateTime.Now;
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);
            TcpConnection temp = new TcpConnection();
            temp.TCPCon = handler;
            temp.PlayerId = ConCounter;
            ConCounter++;
            temp.Session = this;
            lock (_conLock)
            {
                Connections.Add(temp.PlayerId, temp);
            }
            CurrentlyConnecting = true;
        }

        public void CloseGame()
        {
            if (Environment == YoyoEnvironment.Server && IsConnected && CanJoin)
            {
                CanJoin = false;
                StopCoroutine(ListeningThread);
            }
        }

        /// <summary>
        /// Client Functions 
        /// Start Client - Will join with a server specified
        /// at IpAddress and Port.
        /// </summary>

        public void StartClient()
        {
            if (!IsConnected)
            {
                StartCoroutine(ConnectingClient());
            }
        }
        public IEnumerator ConnectingClient()
        {
            _environment = YoyoEnvironment.None;
            IsConnected = false;
            CurrentlyConnecting = false;
            //Setup our socket
            IPAddress ip = (IPAddress.Parse(_ipAddress));
            IPEndPoint endP = new IPEndPoint(ip, _port);
            Socket clientSocket = new Socket(ip.AddressFamily, SocketType.Stream,
                ProtocolType.Tcp);
            //Connect client
            clientSocket.BeginConnect(endP, ConnectingCallback, clientSocket);
            Debug.Log("Trying to wait for server...");
            //Wait for the client to connect
            while(!CurrentlyConnecting)
            {
                yield return new WaitForSeconds(MasterTimer);
            }
            //yield return new WaitUntil(() => CurrentlyConnecting);
            StartCoroutine(Connections[0].TCPRecv());  //It is 0 on the client because we only have 1 socket.
            StartCoroutine(SlowUpdate());  //This will allow the client to send messages to the server.
        }

        public void ConnectingCallback(System.IAsyncResult ar)
        {
            //Client will use the con list (but only have one entry).
            _environment = YoyoEnvironment.Client;
            TcpConnection temp = new TcpConnection();
            temp.TCPCon = (Socket)ar.AsyncState;
            temp.TCPCon.EndConnect(ar);//This finishes the TCP connection (DOES NOT DISCONNECT)    
            IsConnected = true;   
            temp.Session = this;
            Connections.Add(0, temp);
            CurrentlyConnecting = true;
        }
        /// <summary>
        /// Disconnect functions
        /// Leave game 
        /// Disconnect
        /// OnClientDisconnect -> is virtual so you can override it
        /// </summary>
        public void Disconnect(int badConnection)
        {
            if (Environment == YoyoEnvironment.Client)
            {
                if (Connections.ContainsKey(badConnection))
                {
                    TcpConnection badCon = Connections[badConnection];
                    try
                    {
                        badCon.TCPCon.Shutdown(SocketShutdown.Both);
                    }
                    catch
                    { }                 
                    try
                    {badCon.TCPCon.Close();}
                    catch
                    {}
                }
                _environment = YoyoEnvironment.None;
                this.IsConnected = false;
                this.LocalPlayerId = -10;
                foreach (KeyValuePair<int, NetworkIdentifier> obj in NetObjs)
                {
                    Destroy(obj.Value.gameObject);
                }
                NetObjs.Clear();
                Connections.Clear();              
            }
            if (Environment == YoyoEnvironment.Server)
            {
                try
                {
                    if (Connections.ContainsKey(badConnection))
                    {
                        TcpConnection badCon = Connections[badConnection];
                        badCon.TCPCon.Shutdown(SocketShutdown.Both);
                        badCon.TCPCon.Close();  
                    }
                }
                catch (System.Net.Sockets.SocketException)
                {
                    //Bad Connection already closed.
                }
                catch (System.ObjectDisposedException)
                {
                    //Socket already shutdown
                }
                catch (Exception e)
                {
                    //In case anything else goes wrong.
                    Debug.Log("Warning - Error caught in the generic catch!\nINFO: "+e.ToString());
                }
                //Delete All other players objects....
                OnClientDisc(badConnection);
                Connections.Remove(badConnection);
            }
        }

        public virtual void OnClientDisc(int badConnection)
        {
            if (Environment == YoyoEnvironment.Server)
            { 
                //Remove Connection from server
                List<int> badObjs = new List<int>();
                foreach (KeyValuePair<int, NetworkIdentifier> obj in NetObjs)
                {
                    if (obj.Value.Owner == badConnection)
                    {
                        badObjs.Add(obj.Key);
                        //I have to add the key to a temp list and delete
                        //it outside of this for loop
                    }
                }
                //Now I can remove the netObjs from the dictionary.
                for (int i = 0; i < badObjs.Count; i++)
                {
                    NetDestroyObject(badObjs[i]);
                }
            }
        }

        public void LeaveGame()
        {
            if (Environment == YoyoEnvironment.Client && IsConnected)
            {
                try
                {
                    lock (_conLock)
                    {
                        Debug.Log("Sending Disconnect!");
                        Connections[0].IsDisconnecting = true;

                        Connections[0].Send(Encoding.ASCII.
                                            GetBytes(
                                            "DISCON#" + Connections[0].PlayerId.ToString() + "\n")
                                            );

                    }
                }
                catch (System.NullReferenceException)
                {
                    //Client double-tapped disconnect.
                    //Ignore.
                }
                StartCoroutine(WaitForDisc());
                
            }
            if (Environment == YoyoEnvironment.Server && IsConnected)
            {
                try
                {
                    foreach (KeyValuePair<int, TcpConnection> obj in Connections)
                    {
                        lock (_conLock)
                        {
                            Connections[obj.Key].Send(Encoding.ASCII.
                                             GetBytes(
                                             "DISCON#-1\n")
                                             );
                            Connections[obj.Key].IsDisconnecting = true;
                        }
                    }
                }
                catch { }
                _environment = YoyoEnvironment.None;
                try
                {
                    foreach (KeyValuePair<int, NetworkIdentifier> obj in NetObjs)
                    {
                        Destroy(obj.Value.gameObject);
                    }
                }
                catch (System.NullReferenceException)
                {
                    //Objects already destroyed.
                }
                try
                {
                    foreach (KeyValuePair<int, TcpConnection> entry in Connections)
                    {
                        Disconnect(entry.Key);
                    }
                }
                catch (System.NullReferenceException)
                {
                    Debug.Log("Inside Disonnect error!");
                    //connections already destroyed.
                }
                IsConnected = false;
                _environment = YoyoEnvironment.None;
                CurrentlyConnecting = false;
                CanJoin = true;
                try
                {
                    NetObjs.Clear();
                    Connections.Clear();
                    StopCoroutine(ListeningThread);  
                    TCP_Listener.Close();
                    
                }
                catch (System.NullReferenceException)
                {
                    Debug.Log("Inside error.");
                    NetObjs = new Dictionary<int, NetworkIdentifier>();
                    Connections = new Dictionary<int, TcpConnection>();
                }              
            }
        }
        IEnumerator WaitForDisc()
        {
            if (Environment == YoyoEnvironment.Client)
            {
                yield return new WaitUntil(() => Connections[0].DidDisconnect);
                Disconnect(0);
            }
            yield return new WaitForSeconds(.1f);
        }

        public void OnApplicationQuit()
        {
            LeaveGame();
        }

        /// <summary>
        /// Object functions
        /// NetCreateObject -> creates an object across the network
        /// NetDestroyObject -> Destroys an object across the network
        /// </summary>
        public GameObject NetCreateObject(int type, int ownMe, Vector3 initPos = new Vector3() , Quaternion rotation = new Quaternion())
        {
            if (Environment == YoyoEnvironment.Server)
            {
                GameObject temp;
                lock(_objLock)
                {
                    if (type != -1)
                    {
                        temp = GameObject.Instantiate(SpawnPrefab[type], initPos, rotation);
                    }
                    else
                    {
                        temp = GameObject.Instantiate(NetworkPlayerManager, initPos, rotation);
                    }
                    temp.GetComponent<NetworkIdentifier>().Owner = ownMe;
                    temp.GetComponent<NetworkIdentifier>().NetId = ObjectCounter;
                    temp.GetComponent<NetworkIdentifier>().Type = type;
                    NetObjs[ObjectCounter] = temp.GetComponent<NetworkIdentifier>();
                    ObjectCounter++;
                    string MSG = "CREATE#" + type + "#" + ownMe +
                    "#" + (ObjectCounter - 1) + "#" + initPos.x.ToString("n2") + "#" +
                    initPos.y.ToString("n2") + "#" + initPos.z.ToString("n2")+"#"+
                    rotation.x.ToString("n2")+"#" + rotation.y.ToString("n2") + "#" + rotation.z.ToString("n2") + "#" + rotation.w.ToString("n2")+ "\n";
                    lock(_masterMessage)
                    {
                        MasterMessage += MSG;
                    }
                    foreach(NetworkBehaviour n in temp.GetComponents<NetworkBehaviour>())
                    {
                        //Force update to all clients.
                        n.IsDirty = true;
                    }
                }
                return temp;
            }
            else
            {
                return null;
            }

        }

        public void NetDestroyObject(int netIDBad)
        {
            try
            {
                if (NetObjs.ContainsKey(netIDBad))
                {
                    Destroy(NetObjs[netIDBad].gameObject);
                    NetObjs.Remove(netIDBad);
                }
            }
            catch
            {
                //Already been destroyed.
            }
            string msg = "DELETE#" + netIDBad+"\n";
            lock(_masterMessage)
            {
                MasterMessage += msg;
            }
            
        }


        /// <summary>
        /// Support functions
        /// Slow Update()
        /// SetIP address
        /// SetPort
        /// </summary>
        public IEnumerator SlowUpdate()
        {
            while (true)
            {
                //Compose Master Message

                foreach(KeyValuePair<int, NetworkIdentifier> id in NetObjs)
                {
                    lock (_masterMessage)
                    {
                        //Add their message to the masterMessage (the one we send)
                        lock (id.Value._lock)
                        {
                            MasterMessage += id.Value.GameObjectMessages + "\n";
                            //Clear Game Objects messages.
                            id.Value.GameObjectMessages = "";
                        }

                    }

                }

                //Send Master Message
                List<int> bad = new List<int>();
                if(MasterMessage != "")
                {
                    foreach(KeyValuePair<int,TcpConnection> item in Connections)
                    {
                        try
                        {
                            //This will send all of the information to the client (or to the server if on a client).
                            item.Value.Send(Encoding.ASCII.GetBytes(MasterMessage));
                        }
                        catch
                        {
                            bad.Add(item.Key);
                        }
                    }
                    lock(_masterMessage)
                    {
                        MasterMessage = "";//delete old values.
                        
                    }
                    lock (_conLock)
                    {
                        foreach (int i in bad)
                        {
                            this.Disconnect(i);
                        }
                    }
                }
                lock (_waitingLock)
                {
                    MessageWaiting = false;
                }
                while(!MessageWaiting && MasterMessage == "")
                {
                    yield return new WaitForSeconds(MasterTimer);//
                }
                //yield return new WaitUntil(() => (MessageWaiting || MasterMessage != ""));
                //yield return new WaitForSeconds(MasterTimer);
            }
        }

        public void SetIp(string ip)
        {
            _ipAddress = ip;
        }

        public void SetPort(string p)
        {
            _port = int.Parse(p);
        }
	}
}