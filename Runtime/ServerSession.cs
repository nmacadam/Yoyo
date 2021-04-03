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

            TcpConnection temp = new TcpConnection(session.ConnectionCount, handler, session);
            session.ConnectionCount++;
            lock (session._conLock)
            {
                session.Connections.Add(temp.PlayerId, temp);
            }
            session.CurrentlyConnecting = true;

			//CurrentlyConnecting = false;
            if (!session.Connections.ContainsKey(session.ConnectionCount - 1))
            {
                //Connection was not fully established.
				// todo: handle
				return;
            }

			// there was a waitforseconds here, not sure why

			//session.Connections[session.ConnectionCount - 1].Send(Encoding.ASCII.GetBytes("PLAYERID#" + session.Connections[session.ConnectionCount - 1].PlayerId + "\n"));
            Packet idPacket = new Packet(0, (uint)PacketType.PlayerId);
            idPacket.Write(session.Connections[session.ConnectionCount - 1].PlayerId);
			session.Connections[session.ConnectionCount - 1].Send(idPacket);

            Debug.Log("yoyo - sent packet assigning player id " + session.Connections[session.ConnectionCount - 1].PlayerId);

			// todo: start receive
            session.Connections[session.ConnectionCount - 1].BeginReceive();
            //ThreadManager.ExecuteOnMainThread(() => session.StartCoroutine(session.Connections[session.ConnectionCount - 1].TCPRecv()));

			//Udpate all current network objects
            foreach (KeyValuePair<int,NetworkIdentifier> entry in session.NetObjects)
            {//This will create a custom create string for each existing object in the game.
            //     string tempRot = entry.Value.transform.rotation.ToString();
            //     tempRot = tempRot.Replace(',', '#');
            //     tempRot = tempRot.Replace('(', '#');
            //     tempRot = tempRot.Replace(')', '\0');            
            //     string MSG = "CREATE#" + entry.Value.Type + "#" + entry.Value.Owner +
            //    "#" + entry.Value.Identifier + "#" + entry.Value.transform.position.x.ToString("n2") + 
            //    "#" + entry.Value.transform.position.y.ToString("n2") + "#" 
            //    + entry.Value.transform.position.z.ToString("n2") + tempRot+"\n";
            //     session.Connections[session.ConnectionCount - 1].Send(Encoding.ASCII.GetBytes(MSG));

                Packet createPacket = new Packet(0, (uint)PacketType.Create);
                createPacket.Write(entry.Value.Type);
                createPacket.Write(entry.Value.Owner);
                createPacket.Write(entry.Value.Identifier);
                createPacket.Write(entry.Value.transform.position);
                createPacket.Write(entry.Value.transform.rotation);
                session.Connections[session.ConnectionCount - 1].Send(createPacket);

                Debug.Log("yoyo - sent packet creating object type " + entry.Value.Type);
            }

            Debug.Log("yoyo - sending packet to create network player manager");

            //Create NetworkPlayerManager
            ThreadManager.ExecuteOnMainThread(() => session.NetCreateObject(-1, session.ConnectionCount - 1, new Vector3(session.Connections[session.ConnectionCount -1].PlayerId*2-3,0,0)));
        }

        public void ListenCallBack(System.IAsyncResult ar)
        {
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);
            TcpConnection temp = new TcpConnection(ConnectionCount, handler, this);
            ConnectionCount++;
            lock (_conLock)
            {
                Connections.Add(temp.PlayerId, temp);
            }
            CurrentlyConnecting = true;
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
        public GameObject NetCreateObject(int type, int ownMe, Vector3 initPos = new Vector3() , Quaternion rotation = new Quaternion())
        {
            if (Environment == YoyoEnvironment.Server)
            {
                GameObject temp;
                lock(ObjLock)
                {
                    if (type != -1)
                    {
                        temp = GameObject.Instantiate(ContractPrefabs[type], initPos, rotation);
                    }
                    else
                    {
                        temp = GameObject.Instantiate(NetworkPlayerManager, initPos, rotation);
                    }
                    temp.GetComponent<NetworkIdentifier>().Owner = ownMe;
                    temp.GetComponent<NetworkIdentifier>().Identifier = NetObjectCount;
                    temp.GetComponent<NetworkIdentifier>().Type = type;
                    NetObjects[NetObjectCount] = temp.GetComponent<NetworkIdentifier>();
                    NetObjectCount++;

                    Packet createPacket = new Packet(0, (uint)PacketType.Create);
                    createPacket.Write(type);
                    createPacket.Write(ownMe);
                    createPacket.Write(NetObjectCount - 1);
                    createPacket.Write(initPos);
                    createPacket.Write(rotation);
                    // string MSG = "CREATE#" + type + "#" + ownMe +
                    // "#" + (NetObjectCount - 1) + "#" + initPos.x.ToString("n2") + "#" +
                    // initPos.y.ToString("n2") + "#" + initPos.z.ToString("n2")+"#"+
                    // rotation.x.ToString("n2")+"#" + rotation.y.ToString("n2") + "#" + rotation.z.ToString("n2") + "#" + rotation.w.ToString("n2")+ "\n";
                    lock(_masterMessage)
                    {
                        //MasterMessage += MSG;
                        MasterPacket.Enqueue(createPacket);
                    }
                    Debug.Log("yoyo - added create packet to master packet");

                    foreach(NetworkBehaviour n in temp.GetComponents<NetworkBehaviour>())
                    {
                        //Force update to all clients.
                        n.IsDirty = true;
                    }
                }
                return temp;
            }
            else
            {
                return null;
            }

        }

        public void NetDestroyObject(int netIDBad)
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
                //Already been destroyed.
            }
            //string msg = "DELETE#" + netIDBad+"\n";
            Packet deletePacket = new Packet(0, (uint)PacketType.Destroy);
            deletePacket.Write(netIDBad);

            lock(_masterMessage)
            {
                //MasterMessage += msg;
                MasterPacket.Enqueue(deletePacket);
            }
            
        }
		// public void CloseServer()
		// {}

		// public void Listen()
		// {}

		// public void StopListening()
		// {}

		// public void NetInstantiate() 
		// {}

		// public void NetDestroy() 
		// {}
	}
}