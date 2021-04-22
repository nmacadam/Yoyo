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

            Initialize();

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
            lock (session._socketOperationLock)
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

            //Debug.Log("yoyo - sent packet assigning player id " + session.Connections[session.ConnectionCount - 1].PlayerId);

            session.Connections[session.ConnectionCount - 1].BeginReceive();

            // Need to execute this on main thread because accessing the transform of the object causes issues otherwise
            ThreadManager.ExecuteOnMainThread(() => 
            {
                // Update all current network objects
                foreach (KeyValuePair<int, NetworkEntity> entry in session.NetEntities)
                {
                    Vector3 position = entry.Value.transform.position;
                    Quaternion rotation = entry.Value.transform.rotation;

                    lock (session._sendLock)
                    {
                        Packet createPacket = new Packet(0, (uint)PacketType.Create);

                        createPacket.Write(entry.Value.PrefabId);
                        createPacket.Write(entry.Value.Owner);
                        createPacket.Write(entry.Value.Identifier);
                        createPacket.Write(entry.Value.transform.position);
                        createPacket.Write(entry.Value.transform.rotation);
                        //createPacket.Write(Vector3.zero);
                        //createPacket.Write(Quaternion.identity);

                        session.Connections[session.ConnectionCount - 1].Send(createPacket);
                    }
                    
                    //Debug.Log("yoyo - sent packet creating object type " + entry.Value.Type + $" at ({position})");
                }
            });

			// // Update all current network objects
            // foreach (KeyValuePair<int, NetworkEntity> entry in session.NetEntities)
            // {
            //     Vector3 position = entry.Value.transform.position;
            //     //Quaternion rotation = entry.Value.transform.rotation;

            //     lock (session._sendLock)
            //     {
            //         Packet createPacket = new Packet(0, (uint)PacketType.Create);

            //         createPacket.Write(entry.Value.Type);
            //         createPacket.Write(entry.Value.Owner);
            //         createPacket.Write(entry.Value.Identifier);
            //         //createPacket.Write(entry.Value.transform.position);
            //         //createPacket.Write(entry.Value.transform.rotation);
            //         createPacket.Write(Vector3.zero);
            //         createPacket.Write(Quaternion.identity);

            //         session.Connections[session.ConnectionCount - 1].Send(createPacket);
            //     }
                
            //     Debug.Log("yoyo - sent packet creating object type " + entry.Value.Type + $" at ({position})");
            // }

            //Debug.Log("yoyo - sending packet to create network player manager");

            // Create NetworkPlayerManager
            ThreadManager.ExecuteOnMainThread(() => session.NetInstantiate(0, session.ConnectionCount - 1));
            session.CurrentlyConnecting = false;
        }

        // todo: not sure if this needs to exist since it is basically like stop listening
        public void CloseGame()
        {
            if (Environment == YoyoEnvironment.Server && IsConnected && CanJoin)
            {
                CanJoin = false;
                StopListening();
            }
        }

        public void ShutdownServer()
        {
            StartCoroutine(ShutdownServerRoutine());
        }

        public IEnumerator ShutdownServerRoutine()
        {
            if (Environment != YoyoEnvironment.Server || !IsConnected) yield break;

            List<int> disconnectTargets = new List<int>();
            foreach (var connection in Connections)
			{
                Packet disconnectPacket = new Packet(0, (uint)PacketType.Disconnect);
                disconnectPacket.Write(connection.Value.PlayerId);

                connection.Value.Send(disconnectPacket);
				disconnectTargets.Add(connection.Key);
			}

            yield return new WaitForSecondsRealtime(3f);

            foreach (var target in disconnectTargets)
            {
                DisconnectClient(target);
            }

            foreach (var entity in NetEntities.Values)
            {
                if (entity != null)
                {
                    Destroy(entity.gameObject);
                }
            }

            try
            {
                _tcpListener.Close();
            }
            catch
            {

            }

            _isConnected = false;
            _environment = YoyoEnvironment.None;
            _currentlyConnecting = false;
            _connectionCount = 0;
            _netEntityCount = 0;

            NetEntities.Clear();
            Connections.Clear();
        }

        /// <summary>
        /// Instantiates an GameObject across the network
        /// </summary>
        /// <param name="contractIndex">The prefab's index in the network contract</param>
        /// <param name="owner">The player ID of the object's owner</param>
        /// <param name="position">The GameObject's position</param>
        /// <param name="rotation">The GameObject's rotation</param>
        /// <returns>The server-side GameObject</returns>
        public GameObject NetInstantiate(int contractIndex, int owner, Vector3 position = new Vector3() , Quaternion rotation = new Quaternion())
        {
            if (Environment == YoyoEnvironment.Server)
            {
                GameObject go;
                lock(ObjLock)
                {
                    go = GameObject.Instantiate(NetworkContract.GetPrefab(contractIndex), position, rotation);

                    // if (contractIndex != -1)
                    // {
                    //     go = GameObject.Instantiate(ContractPrefabs[contractIndex], position, rotation);
                    // }
                    // else
                    // {
                    //     go = GameObject.Instantiate(NetworkPlayerManager, position, rotation);
                    // }
                    go.GetComponent<NetworkEntity>().Owner = owner;
                    go.GetComponent<NetworkEntity>().Identifier = NetEntityCount;
                    //go.GetComponent<NetworkEntity>().Type = contractIndex;
                    NetEntities[NetEntityCount] = go.GetComponent<NetworkEntity>();
                    NetEntityCount++;

                    Packet createPacket = new Packet(0, (uint)PacketType.Create);
                    createPacket.Write(contractIndex);
                    createPacket.Write(owner);
                    createPacket.Write(NetEntityCount - 1);
                    createPacket.Write(position);
                    createPacket.Write(rotation);

                    lock(_masterPacketLock)
                    {
                        MasterPacket.Enqueue(createPacket);
                    }
                    //Debug.Log($"yoyo - added create packet to master packet (type: {contractIndex}, owner: {owner}, netId: {NetEntityCount - 1})");

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

        /// <summary>
        /// Destroys an object across the network
        /// </summary>
        /// <param name="netIDBad">The object's net identifier</param>
        public void NetDestroy(int netIDBad)
        {
            try
            {
                if (NetEntities.ContainsKey(netIDBad))
                {
                    Destroy(NetEntities[netIDBad].gameObject);
                    NetEntities.Remove(netIDBad);
                }
            }
            catch
            {
                // Already been destroyed.
            }
            Packet deletePacket = new Packet(0, (uint)PacketType.Destroy);
            deletePacket.Write(netIDBad);

            lock(_masterPacketLock)
            {
                MasterPacket.Enqueue(deletePacket);
            }
            
        }
	}
}