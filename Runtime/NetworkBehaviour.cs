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

        public abstract void HandleMessage(Packet packet);
        protected virtual IEnumerator SlowUpdate()
        {
            while (true)
            {
                NetUpdate();
                yield return null;
            }
        }

        protected virtual void NetAwake() {}
        protected virtual void NetStart() {}
        protected virtual void NetUpdate() {}

        public Packet GetCommandPacket()
        {
            Packet packet = new Packet(0, (uint)PacketType.Command);
            packet.Write(NetId.Identifier);
            return packet;
        }

        public Packet GetUpdatePacket()
        {
            Packet packet = new Packet(0, (uint)PacketType.Update);
            packet.Write(NetId.Identifier);
            return packet;
        }

        public void SendCommand(Packet packet)
        {
            if (NetId.Session != null && IsClient && IsLocalPlayer)
            {
                NetId.AddMsg(packet);
            }
        }

        public void SendUpdate(Packet packet)
        {
            if (NetId.Session != null && IsServer)
            {
                NetId.AddMsg(packet);
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
            
            NetAwake();
            NetStart();
            StartCoroutine(SlowUpdate());
        }
    }
}