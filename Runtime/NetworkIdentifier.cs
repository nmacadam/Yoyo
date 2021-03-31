// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System.Collections;
using UnityEngine;

namespace Yoyo.Runtime
{
	public class NetworkIdentifier : MonoBehaviour
	{
		public int Type;
        public int Owner = -10;
        public int NetId = -10;
        public bool IsInit;
        public bool IsLocalPlayer;
        public float UpdateFrequency = .1f;
        public YoyoSession Session;
        public string GameObjectMessages = "";
        public object _lock = new object();

        public bool IsClient => Session.Environment == YoyoEnvironment.Client;
        public bool IsServer => Session.Environment == YoyoEnvironment.Server;

        // Use this for initialization
        void Start()
        {
            Session = GameObject.FindObjectOfType<YoyoSession>();
            if(Session == null)
            {
                throw new System.Exception("There is no network core in the scene!");
            }
            StartCoroutine(SlowStart());
        }
        IEnumerator SlowStart()
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
                if (NetId == -10)
                {   
                    Debug.Log("We are destroying the non-server networked objects");
                    GameObject.Destroy(this.gameObject);
                }
            }
            if (IsServer && NetId == -10)
            {
                //We need to add ourselves to the networked object dictionary
                Type = -1;
                for (int i = 0; i < Session.SpawnPrefab.Length; i++)
                {
                    if (Session.SpawnPrefab[i].gameObject.name == this.gameObject.name.Split('(')[0].Trim())
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
                    lock (Session._objLock)
                    {
                        NetId = Session.ObjectCounter;
                        Session.ObjectCounter++;
                        Owner = -1;
                        Session.NetObjs.Add(NetId, this);
                    }
                }
            }

            yield return new WaitUntil(() => (Owner != -10 && NetId != -10));
            if(Owner == Session.LocalPlayerId)
            {
                IsLocalPlayer = true;
            }
            else
            {
                IsLocalPlayer = false;
            }
            IsInit = true;
            if (IsClient)
            {
                NotifyDirty();
            }
        }
        public void AddMsg(string msg)
        {
            //Debug.Log("Message WAS: " + gameObjectMessages);
            //May need to put race condition blocks here.
            lock (_lock)
            {
                GameObjectMessages += (msg + "\n");
                lock (Session._waitingLock)
                {
                    Session.MessageWaiting = true;
                }
            }
            //Debug.Log("Message IS NOW: " + gameObjectMessages);
        }


        public void Net_Update(string type, string var, string value)
        {
            //Get components for network behaviours
            //Destroy self if owner connection is done.
            try
            {
                if (Session.Environment == YoyoEnvironment.Server && Session.Connections.ContainsKey(Owner) == false && Owner != -1)
                {
                    Session.NetDestroyObject(NetId);
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
                if ((Session.Environment == YoyoEnvironment.Server && type == "COMMAND")
                    || (Session.Environment == YoyoEnvironment.Client && type == "UPDATE"))
                    {
                        NetworkBehaviour[] myNets = gameObject.GetComponents<NetworkBehaviour>();
                        for (int i = 0; i < myNets.Length; i++)
                        {
                            myNets[i].HandleMessage(var, value);
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
            this.AddMsg("DIRTY#" + NetId);
        }
	}
}