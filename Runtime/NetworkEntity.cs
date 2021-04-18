// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Yoyo.Attributes;

namespace Yoyo.Runtime
{
    // rename to gameobject id
	public class NetworkEntity : MonoBehaviour
	{
        [Header("Network Info")]
        [SerializeField, DisableEditing] private int _owner = -10;
        [SerializeField, DisableEditing] private int _identifier = -10;
        [SerializeField, DisableEditing] private bool _isInitialized;
        [SerializeField, DisableEditing] private bool _isLocalPlayer;

        [Header("GameObject Info")]
		public int Type;
        [SerializeField] private List<NetworkBehaviour> _networkBehaviours = new List<NetworkBehaviour>();

        public Queue<Packet> GameObjectPackets = new Queue<Packet>();

        private YoyoSession _session;
        private List<INetworkSubscriber> _networkSubscribers = new List<INetworkSubscriber>();

        public YoyoSession Session 
        { 
            get
            {
                if (_session == null)
                {
                    _session = GameObject.FindObjectOfType<YoyoSession>();
                    if(_session == null)
                    {
                        throw new System.Exception("There is no network core in the scene!");
                    }
                }

                return _session;
            }
            private set => _session = value; 
        }
        
        public bool IsClient => Session.Environment == YoyoEnvironment.Client;
        public bool IsServer => Session.Environment == YoyoEnvironment.Server;
        public bool IsLocalPlayer => Owner == Session.LocalPlayerId;

        public bool IsInitialized { get => _isInitialized; set => _isInitialized = value; }
        public int Owner { get => _owner; set => _owner = value; }
        public int Identifier { get => _identifier; set => _identifier = value; }

        public object _lock = new object();


        public void RegisterBehaviour(NetworkBehaviour behaviour)
        {
            _networkBehaviours.Add(behaviour);
        }

        public bool UnregisterBehaviour(NetworkBehaviour behaviour)
        {
            return _networkBehaviours.Remove(behaviour);
        }

        #if UNITY_EDITOR
        private void OnValidate() 
        {
            List<NetworkBehaviour> behaviours = new List<NetworkBehaviour>();
            behaviours.AddRange(GetComponentsInChildren<NetworkBehaviour>());

            _networkBehaviours.Clear();

            for (int i = 0; i < behaviours.Count; i++)
            {
                behaviours[i].Entity = this;
                behaviours[i].BehaviourId = i;
                _networkBehaviours.Add(behaviours[i]);
            }
        }
        #endif

        // Use this for initialization
        private void Start()
        {
            Session = GameObject.FindObjectOfType<YoyoSession>();
            if(Session == null)
            {
                throw new System.Exception("There is no network core in the scene!");
            }
            StartCoroutine(SlowStart());
        }

        private IEnumerator SlowStart()
        {
            if (!IsServer && !IsClient)
            {
                //This will ONLY be true if the object was in the scene before the connection
                yield return new WaitUntil(() => (Session.Environment != YoyoEnvironment.None));
                yield return new WaitForSecondsRealtime(.1f);
            }
            yield return new WaitForSecondsRealtime(.1f);  //This should be here.
            if (IsClient)
            {
                //Then we know we need to destroy this object and wait for it to be re-created by the server
                if (Identifier == -10)
                {   
                    Debug.Log("We are destroying the non-server networked objects");
                    GameObject.Destroy(this.gameObject);
                }
            }
            if (IsServer && Identifier == -10)
            {
                //We need to add ourselves to the networked object dictionary
                Type = -1;
                for (int i = 0; i < Session.ContractPrefabs.Length; i++)
                {
                    if (Session.ContractPrefabs[i].gameObject.name == this.gameObject.name.Split('(')[0].Trim())
                    {
                        Type = i;              
                        break;
                    }
                }
                if (Type == -1)
                {
                    Debug.LogError("Game object not found in prefab list! Game Object name - " + this.gameObject.name.Split('(')[0].Trim());
                    throw new System.Exception("FATAL - Game Object not found in prefab list!");
                }
                else
                {
                    lock (Session.ObjLock)
                    {
                        Identifier = Session.NetEntityCount;
                        Session.NetEntityCount++;
                        Owner = -1;
                        Session.NetEntities.Add(Identifier, this);
                    }
                }
            }

            yield return new WaitUntil(() => (Owner != -10 && Identifier != -10));
            _isLocalPlayer = IsLocalPlayer;
            IsInitialized = true;
            if (IsClient)
            {
                NotifyDirty();
            }

            foreach (var subscriber in GetComponentsInChildren<INetworkSubscriber>())
            {
                subscriber.OnEntityInitialized(this);
            }
        }

        //public void AddMsg(string msg)
        public void AddMsg(Packet packet)
        {
            //Debug.Log("Message WAS: " + gameObjectMessages);
            //May need to put race condition blocks here.

            //Debug.Log($"yoyo: adding packet of length {packet.Length()} to the waiting queue");

            lock (_lock)
            {
                //GameObjectMessages += (msg + "\n");
                GameObjectPackets.Enqueue(packet);
                lock (Session.WaitingLock)
                {
                    Session.MessageWaiting = true;
                }
            }
            //Debug.Log("Message IS NOW: " + gameObjectMessages);
        }

        //public void Net_Update(PacketType type, string var, string value)
        public void Net_Update(PacketType type, Packet packet)
        {
            //Get components for network behaviours
            //Destroy self if owner connection is done.
            //Debug.Log("In net update");
            try
            {
                if (Session.Environment == YoyoEnvironment.Server && Session.Connections.ContainsKey(Owner) == false && Owner != -1)
                {
                    Session.NetDestroy(Identifier);
                }
            }
            catch (System.NullReferenceException)
            {
                //Has not been initialized yet.  Ignore.
            }
            try
            {
                if (Session == null)
                {
                    Session = GameObject.FindObjectOfType<YoyoSession>();
                }

                int behaviourId = packet.ReadInt();

                if ((Session.Environment == YoyoEnvironment.Server && type == PacketType.Command)
                    || (Session.Environment == YoyoEnvironment.Client && type == PacketType.Update))
                    {
                        //NetworkBehaviour[] myNets = gameObject.GetComponents<NetworkBehaviour>();
                        for (int i = 0; i < _networkBehaviours.Count; i++)
                        {
                            if (_networkBehaviours[i].BehaviourId == behaviourId)
                            {
                                //Debug.Log("Passing to network behaviour #" + i, _networkBehaviours[i]);
                                //_networkBehaviours[i].HandleMessage(var, value);
                                _networkBehaviours[i].HandleMessage(new Packet(packet));
                            }
                        }
                    }
            }
            catch (System.Exception e)
            {
                Debug.Log("Game Object "+name+" Caught Exception: " + e.ToString(), gameObject);
                //This can happen if myCore has not been set.  
                //I am not sure how this is possible, but it should be good for the next round.
            }
        }

        public void NotifyDirty()
        {
            Packet packet = new Packet(0, (uint)PacketType.Dirty);
            packet.Write(Identifier);
            //this.AddMsg("DIRTY#" + Identifier);
            this.AddMsg(packet);
        }
	}
}