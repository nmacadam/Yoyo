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

        public Dictionary<int, TcpConnection> Connections;
        public Dictionary<int, NetworkIdentifier> NetObjects;

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
        public object _objLock = new object();
        public object _waitingLock = new object();

        private object _conLock = new object();
        private object _masterMessage = new object();

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
                    StopCoroutine(ListeningThread);  
                    TCP_Listener.Close();
                    
                }
                catch (System.NullReferenceException)
                {
                    Debug.Log("Inside error.");
                    NetObjects = new Dictionary<int, NetworkIdentifier>();
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
	}
}