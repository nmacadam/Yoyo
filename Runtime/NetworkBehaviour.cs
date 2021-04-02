// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System.Collections;
using UnityEngine;

namespace Yoyo.Runtime
{
	[RequireComponent(typeof(NetworkIdentifier))]
    public abstract class NetworkBehaviour : MonoBehaviour
    {
        [SerializeField] private NetworkIdentifier _netId;
        private bool _dirty = false;

        public NetworkIdentifier NetId { get => _netId; private set => _netId = value; }
        public bool IsDirty { get => _dirty; set => _dirty = value; }

        public bool IsClient => NetId.Session.Environment == YoyoEnvironment.Client;
        public bool IsServer => NetId.Session.Environment == YoyoEnvironment.Server;
        public bool IsLocalPlayer => NetId.IsLocalPlayer;

        public abstract void HandleMessage(string flag, string value);
        protected virtual IEnumerator SlowUpdate()
        {
            while (true)
            {
                NetUpdate();
                yield return null;
            }
        }

        protected virtual void NetStart() {}
        protected virtual void NetUpdate() {}

        public void SendCommand(string var, string value)
        {
            var = var.Replace('#', ' ');
            var = var.Replace('\n', ' ');
            value = value.Replace('#', ' ');
            value = value.Replace('\n', ' ');
            if (NetId.Session != null && IsClient && IsLocalPlayer && NetId.GameObjectMessages.Contains(var) == false)
            {
                string msg = "COMMAND#" + NetId.Identifier + "#" + var + "#" + value;
                NetId.AddMsg(msg);
            }
        }

        public void SendUpdate(string var, string value)
        {
            var = var.Replace('#', ' ');
            var = var.Replace('\n', ' ');
            value = value.Replace('#', ' ');
            value = value.Replace('\n', ' ');
            if (NetId.Session != null && IsServer && NetId.GameObjectMessages.Contains(var)==false)
            {
                string msg = "UPDATE#" + NetId.Identifier + "#" + var + "#" + value;
                NetId.AddMsg(msg);
            }
        }

        private void OnEnable() 
        {
            NetId.RegisterBehaviour(this);
        }

        private void OnDisable() 
        {
            NetId.UnregisterBehaviour(this);
        }

        private IEnumerator Start() 
        {
            if(NetId == null)
            {
                throw new System.Exception("ERROR: There is no network ID on this object");
            }

            yield return new WaitUntil(() => NetId.IsInitialized);
            
            NetStart();
            StartCoroutine(SlowUpdate());
        }
    }
}