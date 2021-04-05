// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Yoyo.Attributes;

namespace Yoyo.Runtime
{
    // rename to gameobject id
	public class NetworkIdentifier : MonoBehaviour
	{
        [Header("Network Info")]
        [SerializeField, DisableEditing] private int _owner = -10;
        [SerializeField, DisableEditing] private int _identifier = -10;
        [SerializeField, DisableEditing] private bool _isInitialized;
        [SerializeField, DisableEditing] private bool _isLocalPlayer;

        [Header("GameObject Info")]
		public int Type;

        //public string GameObjectMessages = "";
        public Queue<Packet> GameObjectPackets = new Queue<Packet>();

        private YoyoSession _session;
        private List<NetworkBehaviour> _networkBehaviours = new List<NetworkBehaviour>();

        public YoyoSession Session { get => _session; private set => _session = value; }
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
                yield return new WaitForSeconds(.1f);
            }
            yield return new WaitForSeconds(.1f);  //This should be here.
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
                        Identifier = Session.NetObjectCount;
                        Session.NetObjectCount++;
                        Owner = -1;
                        Session.NetObjects.Add(Identifier, this);
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
        }

        //public void AddMsg(string msg)
        public void AddMsg(Packet packet)
        {
            //Debug.Log("Message WAS: " + gameObjectMessages);
            //May need to put race condition blocks here.
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
            try
            {
                if (Session.Environment == YoyoEnvironment.Server && Session.Connections.ContainsKey(Owner) == false && Owner != -1)
                {
                    Session.NetDestroyObject(Identifier);
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
                if ((Session.Environment == YoyoEnvironment.Server && type == PacketType.Command)
                    || (Session.Environment == YoyoEnvironment.Client && type == PacketType.Update))
                    {
                        //NetworkBehaviour[] myNets = gameObject.GetComponents<NetworkBehaviour>();
                        for (int i = 0; i < _networkBehaviours.Count; i++)
                        {
                            // ! this will break for more than one read
                            //_networkBehaviours[i].HandleMessage(var, value);
                            _networkBehaviours[i].HandleMessage(new Packet(packet));
                        }
                    }
            }
            catch (System.Exception e)
            {
                Debug.Log("Game Object "+name+" Caught Exception: " + e.ToString());
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