// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
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
        [SerializeField] private SocketParameters _tcpParameters = default;
        
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
        [SerializeField, Expandable]
        private NetworkContract _networkContract = default;

        private IPAddress _ipAddress;
        private Dictionary<int, TcpConnection> _connections = new Dictionary<int, TcpConnection>();
        private Dictionary<int, NetworkEntity> _netEntities = new Dictionary<int, NetworkEntity>();
        private int _netEntityCount = 0;
        private int _connectionCount = 0;

        public NetworkContract NetworkContract => _networkContract;

        public IPAddress Address => _ipAddress;
        public YoyoEnvironment Environment => _environment;
        public bool IsConnected { get => _isConnected; private set => _isConnected = value; }
        public bool CanJoin { get => _canJoin; private set => _canJoin = value; }
        public bool CurrentlyConnecting { get => _currentlyConnecting; private set => _currentlyConnecting = value; }
        public int LocalPlayerId { get => _localPlayerId; set => _localPlayerId = value; }
        public Dictionary<int, TcpConnection> Connections { get => _connections; private set => _connections = value; }
        public Dictionary<int, NetworkEntity> NetEntities { get => _netEntities; private set => _netEntities = value; }
        public int NetEntityCount { get => _netEntityCount; set => _netEntityCount = value; }
        public int ConnectionCount { get => _connectionCount; set => _connectionCount = value; }

        public SocketParameters TcpParameters => _tcpParameters;

        //WE are going to push a variable to notify the master an ID has a message.
        public bool MessageWaiting { get; set; }
        public Queue<Packet> MasterPacket = new Queue<Packet>();

        // Locks
        public object ObjLock = new object();
        public object WaitingLock = new object();
        private object _socketOperationLock = new object();
        private object _masterPacketLock = new object();

        private void Initialize() 
        {
            foreach (var cli in GetComponents<ICommandLineInterface>())
            {
                cli.ProcessArguments(System.Environment.GetCommandLineArgs());
            }

            _environment = YoyoEnvironment.None;
            
            IsConnected = false;
            CurrentlyConnecting = false;

            if (_ipAddressString == "")
            {
                _ipAddressString = "127.0.0.1"; //Local host
            }
            _ipAddress = IPAddress.Parse(_ipAddressString);

            if (_port == 0)
            {
                _port = 9001;
            }
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
                foreach (KeyValuePair<int, NetworkEntity> obj in NetEntities)
                {
                    Destroy(obj.Value.gameObject);
                }
                NetEntities.Clear();
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
                foreach (KeyValuePair<int, NetworkEntity> obj in NetEntities)
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
                    NetDestroy(badObjs[i]);
                }
            }
        }

        public void LeaveGame()
        {
            if (Environment == YoyoEnvironment.Client && IsConnected)
            {
                try
                {
                    lock (_socketOperationLock)
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
                        lock (_socketOperationLock)
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
                    foreach (KeyValuePair<int, NetworkEntity> obj in NetEntities)
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
                    NetEntities.Clear();
                    Connections.Clear();
                    StopListening();
                    _tcpListener.Close();
                    
                }
                catch (System.NullReferenceException)
                {
                    Debug.Log("Inside error.");
                    NetEntities = new Dictionary<int, NetworkEntity>();
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
            yield return new WaitForSecondsRealtime(.1f);
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

                foreach(KeyValuePair<int, NetworkEntity> id in NetEntities)
                {
                    lock (_masterPacketLock)
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
                    // ! THIS NEEDS A LOCK; IF SOMETHING JOINS OR IS REMOVED IT MESSES THINGS UP
                    foreach(KeyValuePair<int,TcpConnection> item in Connections)
                    {
                        try
                        {
                            //This will send all of the information to the client (or to the server if on a client).
                            //item.Value.Send(Encoding.ASCII.GetBytes(MasterMessage));
                            foreach (var packet in MasterPacket)
                            {
                                //Debug.Log("yoyo - sent packet in master packet");
                                //item.Value.Send(packet);
                                item.Value.Send(new Packet(packet));
                            }
                        }
                        catch
                        {
                            bad.Add(item.Key);
                        }
                    }
                    lock(_masterPacketLock)
                    {
                        //MasterMessage = "";//delete old values.
                        MasterPacket.Clear();
                        
                    }
                    lock (_socketOperationLock)
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
                    yield return new WaitForSecondsRealtime(MasterTimer);//
                }
                //yield return new WaitUntil(() => (MessageWaiting || MasterMessage != ""));
                //yield return new WaitForSecondsRealtime(MasterTimer);
            }
        }

        public void SetIP(string ipAddress)
        {
            _ipAddressString = ipAddress;
            _ipAddress = IPAddress.Parse(ipAddress);
        }

        public void SetIP(IPAddress ipAddress)
        {
            _ipAddressString = ipAddress.ToString();
            _ipAddress = ipAddress;
        }

        public void SetPort(int port)
        {
            _port = port;
        }
	}
}