// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Yoyo.Runtime
{
	public partial class YoyoSession : MonoBehaviour
	{
		private class ListenerSocketState
		{
			public YoyoSession Session;
			public Socket WorkSocket;
			public int MaxConnections;
			//public Connections;
		}

        private object _sendLock = new object();

        private Socket _tcpListener;

		/// <summary>
        /// Server Functions
        /// StartServer -> Initialize Listener and Slow Update
        ///     - WIll spawn the first prefab as a "NetworkPlayerManager"
        /// Listen -> Will bind to a port and allow clients to join.
        /// </summary>
        public void StartServer()
        {
			if (IsConnected) return;

            Debug.Log("yoyo - starting server");

			//If we are listening then we are the server.
            _environment = YoyoEnvironment.Server;
            IsConnected = true;
			LocalPlayerId = -1; //For server the localplayer id will be -1.

			IPAddress ip = (IPAddress.Any);
            IPEndPoint localEndPoint = new IPEndPoint(ip, _port);
			Socket listener = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

			try
            {
                listener.Bind(localEndPoint);
                listener.Listen(_maxConnections);

                ListenerSocketState state = new ListenerSocketState();
				state.Session = this;
                state.WorkSocket = listener;
                //state.Info = _info;
                state.MaxConnections = _maxConnections;
                //state.Connections = _connections;

                listener.BeginAccept(AcceptCallback, state);
           	 	StartCoroutine(SlowUpdate());
            }
            catch (Exception e)
            {
                Debug.Log("yoyo: failed to start server: " + e.ToString());
            }
        }

        public void StopListening()
        {
            if(Environment == YoyoEnvironment.Server && CanJoin)
            {   
                CurrentlyConnecting = false;
                //StopCoroutine(ListeningThread);
                _tcpListener.Close();
            }
        }

		/// <summary>
        /// Called when a listener accepts a client
        /// </summary>
        private static void AcceptCallback(IAsyncResult ar)
        {
			// Get the socket that handles the client request.
            ListenerSocketState state = (ListenerSocketState)ar.AsyncState;
            Socket listener = state.WorkSocket;
            Socket handler = listener.EndAccept(ar);
			YoyoSession session = state.Session;

            Debug.Log("yoyo - incoming connection...");
            session.CurrentlyConnecting = true;

			// todo: this will permanently block new joins unless listen is called again...?
			if (!state.Session.CanJoin)
			{
				return;
			}
			else
			{
				// Continue listener loop
            	listener.BeginAccept(AcceptCallback, state);
			}

            TcpConnection temp = new TcpConnection(session.TcpParameters, session.ConnectionCount, handler, session);
            session.ConnectionCount++;
            lock (session._conLock)
            {
                session.Connections.Add(temp.PlayerId, temp);
            }

			//CurrentlyConnecting = false;
            if (!session.Connections.ContainsKey(session.ConnectionCount - 1))
            {
                //Connection was not fully established.
				// todo: handle
                Debug.Log("yoyo - connection was not fully established");
                session.CurrentlyConnecting = false;
				return;
            }

			// there was a waitforseconds here, not sure why

            Packet idPacket = new Packet(0, (uint)PacketType.PlayerId);
            idPacket.Write(session.Connections[session.ConnectionCount - 1].PlayerId);
			session.Connections[session.ConnectionCount - 1].Send(idPacket);

            Debug.Log("yoyo - sent packet assigning player id " + session.Connections[session.ConnectionCount - 1].PlayerId);

            session.Connections[session.ConnectionCount - 1].BeginReceive();

			// Update all current network objects
            foreach (KeyValuePair<int, NetworkIdentifier> entry in session.NetObjects)
            {
                lock (session._sendLock)
                {
                    Packet createPacket = new Packet(0, (uint)PacketType.Create);

                    createPacket.Write(entry.Value.Type);
                    createPacket.Write(entry.Value.Owner);
                    createPacket.Write(entry.Value.Identifier);
                    //createPacket.Write(entry.Value.transform.position);
                    //createPacket.Write(entry.Value.transform.rotation);
                    createPacket.Write(Vector3.zero);
                    createPacket.Write(Quaternion.identity);

                    session.Connections[session.ConnectionCount - 1].Send(createPacket);

                    Debug.Log("yoyo - sent packet creating object type " + entry.Value.Type);
                }
            }

            Debug.Log("yoyo - sending packet to create network player manager");

            // Create NetworkPlayerManager
            ThreadManager.ExecuteOnMainThread(() => session.NetInstantiate(-1, session.ConnectionCount - 1));
            session.CurrentlyConnecting = false;
        }

        public void CloseGame()
        {
            if (Environment == YoyoEnvironment.Server && IsConnected && CanJoin)
            {
                CanJoin = false;
                StopListening();
            }
        }

        /// <summary>
        /// Object functions
        /// NetCreateObject -> creates an object across the network
        /// NetDestroyObject -> Destroys an object across the network
        /// </summary>
        public GameObject NetInstantiate(int contractIndex, int owner, Vector3 initPos = new Vector3() , Quaternion rotation = new Quaternion())
        {
            if (Environment == YoyoEnvironment.Server)
            {
                GameObject go;
                lock(ObjLock)
                {
                    if (contractIndex != -1)
                    {
                        go = GameObject.Instantiate(ContractPrefabs[contractIndex], initPos, rotation);
                    }
                    else
                    {
                        go = GameObject.Instantiate(NetworkPlayerManager, initPos, rotation);
                    }
                    go.GetComponent<NetworkIdentifier>().Owner = owner;
                    go.GetComponent<NetworkIdentifier>().Identifier = NetObjectCount;
                    go.GetComponent<NetworkIdentifier>().Type = contractIndex;
                    NetObjects[NetObjectCount] = go.GetComponent<NetworkIdentifier>();
                    NetObjectCount++;

                    Packet createPacket = new Packet(0, (uint)PacketType.Create);
                    createPacket.Write(contractIndex);
                    createPacket.Write(owner);
                    createPacket.Write(NetObjectCount - 1);
                    createPacket.Write(initPos);
                    createPacket.Write(rotation);

                    lock(_masterMessage)
                    {
                        MasterPacket.Enqueue(createPacket);
                    }
                    Debug.Log($"yoyo - added create packet to master packet (type: {contractIndex}, owner: {owner}, netId: {NetObjectCount - 1})");

                    foreach(NetworkBehaviour n in go.GetComponents<NetworkBehaviour>())
                    {
                        //Force update to all clients.
                        n.IsDirty = true;
                    }
                }
                return go;
            }
            else
            {
                return null;
            }

        }

        public void NetDestroy(int netIDBad)
        {
            try
            {
                if (NetObjects.ContainsKey(netIDBad))
                {
                    Destroy(NetObjects[netIDBad].gameObject);
                    NetObjects.Remove(netIDBad);
                }
            }
            catch
            {
                // Already been destroyed.
            }
            Packet deletePacket = new Packet(0, (uint)PacketType.Destroy);
            deletePacket.Write(netIDBad);

            lock(_masterMessage)
            {
                MasterPacket.Enqueue(deletePacket);
            }
            
        }
	}
}