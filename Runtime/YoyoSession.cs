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
        [Tooltip("What is the IP address a client should connect to?")]
		[SerializeField, DisplayAs("IP Address")] private string _ipAddressString = default;
        [Tooltip("What port will the server be using?")]
        [SerializeField] private int _port = 0;
        [Tooltip("How many clients can connect at once?")]
        [SerializeField, Range(1, 128)] 
		private int _maxConnections = 32;
        [Tooltip("How often will the server netcode update?")]
        public float MasterTimer = .05f;

        [Header("Socket Options")]
        [Tooltip("What is the packet buffer size for the socket?")]
        [SerializeField, NumberDropdown(512, 1024, 2048, 4096, 8192)] private int _bufferSize = 1024;
        [Tooltip("Should the socket use Nagle's Algorithm?")]
        [SerializeField] private bool _noDelay = false;
        
        [Header("Session State")]
        [Tooltip("Is this Yoyo Session representing a client or server?")]
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

        [Header("Session Contract")]
        public GameObject[] _contractPrefabs;
        public GameObject _networkPlayerManager;

        private IPAddress _ipAddress;
        private Dictionary<int, TcpConnection> _connections;
        private Dictionary<int, NetworkIdentifier> _netObjects;
        private int _netObjectCount = 0;
        private int _connectionCount = 0;

        private SocketParameters _tcpParameters;

        public GameObject[] ContractPrefabs => _contractPrefabs;
        public GameObject NetworkPlayerManager => _networkPlayerManager;

        public IPAddress Address => _ipAddress;
        public YoyoEnvironment Environment => _environment;
        public bool IsConnected { get => _isConnected; private set => _isConnected = value; }
        public bool CanJoin { get => _canJoin; private set => _canJoin = value; }
        public bool CurrentlyConnecting { get => _currentlyConnecting; private set => _currentlyConnecting = value; }
        public int LocalPlayerId { get => _localPlayerId; set => _localPlayerId = value; }
        public Dictionary<int, TcpConnection> Connections { get => _connections; private set => _connections = value; }
        public Dictionary<int, NetworkIdentifier> NetObjects { get => _netObjects; private set => _netObjects = value; }
        public int NetObjectCount { get => _netObjectCount; set => _netObjectCount = value; }
        public int ConnectionCount { get => _connectionCount; set => _connectionCount = value; }

        public SocketParameters TcpParameters => _tcpParameters;

        //WE are going to push a variable to notify the master an ID has a message.
        public bool MessageWaiting { get; set; }
        public Queue<Packet> MasterPacket = new Queue<Packet>();

        // Locks
        public object ObjLock = new object();
        public object WaitingLock = new object();
        private object _conLock = new object();
        private object _masterMessage = new object();

        private void Start()
        {
            _environment = YoyoEnvironment.None;

            _tcpParameters = new SocketParameters() 
            {
                BufferSize = _bufferSize,
                NoDelay = _noDelay
            };
            
            IsConnected = false;
            CurrentlyConnecting = false;
            //ipAddress = "127.0.0.1";//Local host
            if (_ipAddressString == "")
            {
                _ipAddressString = "127.0.0.1"; //Local host
            }
            _ipAddress = IPAddress.Parse(_ipAddressString);

            if (_port == 0)
            {
                _port = 9001;
            }
            Connections = new Dictionary<int, TcpConnection>();
            NetObjects = new Dictionary<int, NetworkIdentifier>();
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
                        badCon.Socket.Shutdown(SocketShutdown.Both);
                    }
                    catch
                    { }                 
                    try
                    {badCon.Socket.Close();}
                    catch
                    {}
                }
                _environment = YoyoEnvironment.None;
                this.IsConnected = false;
                this.LocalPlayerId = -10;
                foreach (KeyValuePair<int, NetworkIdentifier> obj in NetObjects)
                {
                    Destroy(obj.Value.gameObject);
                }
                NetObjects.Clear();
                Connections.Clear();              
            }
            if (Environment == YoyoEnvironment.Server)
            {
                try
                {
                    if (Connections.ContainsKey(badConnection))
                    {
                        TcpConnection badCon = Connections[badConnection];
                        badCon.Socket.Shutdown(SocketShutdown.Both);
                        badCon.Socket.Close();  
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
                foreach (KeyValuePair<int, NetworkIdentifier> obj in NetObjects)
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

                        Packet disconnectPacket = new Packet(0, (uint)PacketType.Disconnect);
                        disconnectPacket.Write(Connections[0].PlayerId);

                        Connections[0].Send(disconnectPacket);

                        // Connections[0].Send(Encoding.ASCII.
                        //                     GetBytes(
                        //                     "DISCON#" + Connections[0].PlayerId.ToString() + "\n")
                        //                     );

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
                            Packet disconnectPacket = new Packet(0, (uint)PacketType.Disconnect);
                            disconnectPacket.Write(-1);
                            
                            Connections[obj.Key].Send(disconnectPacket);

                            // Connections[obj.Key].Send(Encoding.ASCII.
                            //                  GetBytes(
                            //                  "DISCON#-1\n")
                            //                  );
                            Connections[obj.Key].IsDisconnecting = true;
                        }
                    }
                }
                catch { }
                _environment = YoyoEnvironment.None;
                try
                {
                    foreach (KeyValuePair<int, NetworkIdentifier> obj in NetObjects)
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
                    NetObjects.Clear();
                    Connections.Clear();
                    StopListening();
                    _tcpListener.Close();
                    
                }
                catch (System.NullReferenceException)
                {
                    Debug.Log("Inside error.");
                    NetObjects = new Dictionary<int, NetworkIdentifier>();
                    Connections = new Dictionary<int, TcpConnection>();
                }              
            }
        }
        private IEnumerator WaitForDisc()
        {
            if (Environment == YoyoEnvironment.Client)
            {
                yield return new WaitUntil(() => Connections[0].DidDisconnect);
                Disconnect(0);
            }
            yield return new WaitForSeconds(.1f);
        }

        private void OnApplicationQuit()
        {
            LeaveGame();
        }

        private void Update()
        //public void LateUpdate()
        {
            ThreadManager.UpdateMain();
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

                foreach(KeyValuePair<int, NetworkIdentifier> id in NetObjects)
                {
                    lock (_masterMessage)
                    {
                        //Add their message to the masterMessage (the one we send)
                        lock (id.Value._lock)
                        {
                            //MasterMessage += id.Value.GameObjectMessages + "\n";
                            foreach (var packet in id.Value.GameObjectPackets)
                            {
                                MasterPacket.Enqueue(packet);
                            }
                            //Clear Game Objects messages.
                            //id.Value.GameObjectMessages = "";
                            id.Value.GameObjectPackets.Clear();
                        }

                    }

                }

                //Send Master Message
                List<int> bad = new List<int>();
                //if(MasterMessage != "")
                if(MasterPacket.Count != 0)
                {
                    foreach(KeyValuePair<int,TcpConnection> item in Connections)
                    {
                        try
                        {
                            //This will send all of the information to the client (or to the server if on a client).
                            //item.Value.Send(Encoding.ASCII.GetBytes(MasterMessage));
                            foreach (var packet in MasterPacket)
                            {
                                Debug.Log("yoyo - sent packet in master packet");
                                //item.Value.Send(packet);
                                item.Value.Send(new Packet(packet));
                            }
                        }
                        catch
                        {
                            bad.Add(item.Key);
                        }
                    }
                    lock(_masterMessage)
                    {
                        //MasterMessage = "";//delete old values.
                        MasterPacket.Clear();
                        
                    }
                    lock (_conLock)
                    {
                        foreach (int i in bad)
                        {
                            this.Disconnect(i);
                        }
                    }
                }
                lock (WaitingLock)
                {
                    MessageWaiting = false;
                }
                //while(!MessageWaiting && MasterMessage == "")
                while(!MessageWaiting && MasterPacket.Count == 0)
                {
                    yield return new WaitForSeconds(MasterTimer);//
                }
                //yield return new WaitUntil(() => (MessageWaiting || MasterMessage != ""));
                //yield return new WaitForSeconds(MasterTimer);
            }
        }
	}
}