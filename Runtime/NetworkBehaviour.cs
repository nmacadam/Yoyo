// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System.Collections;
using UnityEngine;

namespace Yoyo.Runtime
{
	[RequireComponent(typeof(NetworkIdentifier))]
    public abstract class NetworkBehaviour : MonoBehaviour
    {
        public bool IsDirty = false;
        public bool IsLocalPlayer;
        public int Owner;
        public int Type;
        public int NetId;
        public YoyoSession Session;
        public NetworkIdentifier MyId;

        public bool IsClient => Session.Environment == YoyoEnvironment.Client;
        public bool IsServer => Session.Environment == YoyoEnvironment.Server;
        
        // Start is called before the first frame update
        public void Awake()
        {
            MyId = gameObject.GetComponent<NetworkIdentifier>();
            Session = GameObject.FindObjectOfType<YoyoSession>();
            if(Session == null)
            {
                throw new System.Exception("ERROR: There is no network core on the scene.");
            }
            if(MyId == null)
            {
                throw new System.Exception("ERROR: There is no network ID on this object");
            }
            StartCoroutine(SlowStart());
        }
        void Start()
        {
         
        }

        IEnumerator SlowStart()
        {
            yield return new WaitUntil(() => MyId.IsInit);
            IsLocalPlayer = MyId.IsLocalPlayer;
            Owner = MyId.Owner;
            Type = MyId.Type;
            NetId = MyId.NetId;
            StartCoroutine(SlowUpdate());
        }

        public abstract IEnumerator SlowUpdate();
        public abstract void HandleMessage(string flag, string value);

        public void SendCommand(string var, string value)
        {
            var = var.Replace('#', ' ');
            var = var.Replace('\n', ' ');
            value = value.Replace('#', ' ');
            value = value.Replace('\n', ' ');
            if (Session != null && Session.Environment == YoyoEnvironment.Client && IsLocalPlayer && MyId.GameObjectMessages.Contains(var) == false)
            {
                string msg = "COMMAND#" + MyId.NetId + "#" + var + "#" + value;
                MyId.AddMsg(msg);
            }
        }
        public void SendUpdate(string var, string value)
        {
            var = var.Replace('#', ' ');
            var = var.Replace('\n', ' ');
            value = value.Replace('#', ' ');
            value = value.Replace('\n', ' ');
            if (Session != null && Session.Environment == YoyoEnvironment.Server && MyId.GameObjectMessages.Contains(var)==false)
            {
                string msg = "UPDATE#" + MyId.NetId + "#" + var + "#" + value;
                MyId.AddMsg(msg);
            }
        }
    }
}