// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System.Collections;
using UnityEngine;
using Yoyo.Attributes;

namespace Yoyo.Runtime
{
    public abstract class NetworkBehaviour : MonoBehaviour
    {
        [SerializeField, DisableEditing] private NetworkEntity _entity;

        [SerializeField, DisableEditing] private int _behaviourId = -1;
        private bool _dirty = false;

        public NetworkEntity Entity { get => _entity; set => _entity = value; }
        public int BehaviourId { get => _behaviourId; set => _behaviourId = value; }
        public bool IsDirty { get => _dirty; set => _dirty = value; }

        public bool IsClient => Entity.Session.Environment == YoyoEnvironment.Client;
        public bool IsServer => Entity.Session.Environment == YoyoEnvironment.Server;
        public bool IsLocalPlayer => Entity.IsLocalPlayer;


        public abstract void HandleMessage(Packet packet);
        protected virtual IEnumerator SlowUpdate()
        {
            while (true)
            {
                NetUpdate();
                //yield return null;
                yield return new WaitForSecondsRealtime(Entity.Session.MasterTimer);
            }
        }

        protected virtual void NetAwake() {}
        protected virtual void NetStart() {}
        protected virtual void NetUpdate() {}

        public Packet GetCommandPacket()
        {
            Packet packet = new Packet(0, (uint)PacketType.Command);
            packet.Write(Entity.Identifier);
            packet.Write(BehaviourId);
            return packet;
        }

        public Packet GetUpdatePacket()
        {
            Packet packet = new Packet(0, (uint)PacketType.Update);
            packet.Write(Entity.Identifier);
            packet.Write(BehaviourId);
            return packet;
        }

        public void SendCommand(Packet packet)
        {
            if (Entity.Session != null && IsClient && IsLocalPlayer)
            {
                Entity.AddMsg(packet);
            }
        }

        public void SendUpdate(Packet packet)
        {
            if (Entity.Session != null && IsServer)
            {
                Entity.AddMsg(packet);
            }
        }

        private void OnEnable() 
        {
            Entity.RegisterBehaviour(this);
        }

        private void OnDisable() 
        {
            Entity.UnregisterBehaviour(this);
        }

        private IEnumerator Start() 
        {
            if(Entity == null)
            {
                throw new System.Exception("ERROR: There is no network ID on this object");
            }

            yield return new WaitUntil(() => Entity.IsInitialized);
            
            NetAwake();
            NetStart();
            StartCoroutine(SlowUpdate());
        }
    }
}