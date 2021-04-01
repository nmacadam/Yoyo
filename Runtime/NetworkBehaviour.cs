// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System.Collections;
using UnityEngine;

namespace Yoyo.Runtime
{
	[RequireComponent(typeof(NetworkIdentifier))]
    public abstract class NetworkBehaviour : MonoBehaviour
    {
        private NetworkIdentifier _netId;
        private bool _dirty = false;

        public NetworkIdentifier NetId { get => _netId; private set => _netId = value; }
        public bool IsDirty { get => _dirty; set => _dirty = value; }

        public bool IsClient => NetId.Session.Environment == YoyoEnvironment.Client;
        public bool IsServer => NetId.Session.Environment == YoyoEnvironment.Server;
        public bool IsLocalPlayer => NetId.IsLocalPlayer;

        private void Awake()
        {
            NetId = gameObject.GetComponent<NetworkIdentifier>();
            if(NetId == null)
            {
                throw new System.Exception("ERROR: There is no network ID on this object");
            }
            StartCoroutine(SlowStart());
        }

        private IEnumerator SlowStart()
        {
            yield return new WaitUntil(() => NetId.IsInitialized);
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
    }
}