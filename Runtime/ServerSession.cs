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
        private Socket TCP_Listener;

		/// <summary>
        /// Server Functions
        /// StartServer -> Initialize Listener and Slow Update
        ///     - WIll spawn the first prefab as a "NetworkPlayerManager"
        /// Listen -> Will bind to a port and allow clients to join.
        /// </summary>
        public void StartServer()
        {
            if (!IsConnected)
            {
                ListeningThread = StartCoroutine(Listen());
                StartCoroutine(SlowUpdate());
            }
        }

        public void StopListening()
        {
            if(Environment == YoyoEnvironment.Server && CanJoin)
            {   
                CurrentlyConnecting = false;
                StopCoroutine(ListeningThread);
                TCP_Listener.Close();
            }
        }

		public IEnumerator Listen()
        {
            //If we are listening then we are the server.
            _environment = YoyoEnvironment.Server;
            IsConnected = true;
            LocalPlayerId = -1; //For server the localplayer id will be -1.
                                //Initialize port to listen to
                                
            IPAddress ip = (IPAddress.Any);
            IPEndPoint endP = new IPEndPoint(ip, _port);
            //We could do UDP in some cases but for now we will do TCP
            TCP_Listener = new Socket(ip.AddressFamily,SocketType.Stream, ProtocolType.Tcp);

            //Now I have a socket listener.
            TCP_Listener.Bind(endP);
            TCP_Listener.Listen(_maxConnections);

            while(CanJoin)
            {
                CurrentlyConnecting = false;
                
                TCP_Listener.BeginAccept(new System.AsyncCallback(this.ListenCallBack), TCP_Listener);               
                yield return new WaitUntil(() => CurrentlyConnecting);
                DateTime time2 = DateTime.Now;
                TimeSpan timeS = time2 - StartConnection;

                CurrentlyConnecting = false;
                if (Connections.ContainsKey(ConCounter - 1) == false)
                {
                    //Connection was not fully established.
                    continue;
                }
                yield return new WaitForSeconds(2*(float)timeS.TotalSeconds);
                Connections[ConCounter - 1].Send(Encoding.ASCII.GetBytes("PLAYERID#" + Connections[ConCounter - 1].PlayerId + "\n"));
                //Start Server side listening for client messages.
                StartCoroutine(Connections[ConCounter - 1].TCPRecv());

                //Udpate all current network objects
                foreach (KeyValuePair<int,NetworkIdentifier> entry in NetObjs)
                {//This will create a custom create string for each existing object in the game.
                    string tempRot = entry.Value.transform.rotation.ToString();
                    tempRot = tempRot.Replace(',', '#');
                    tempRot = tempRot.Replace('(', '#');
                    tempRot = tempRot.Replace(')', '\0');            

                    string MSG = "CREATE#" + entry.Value.Type + "#" + entry.Value.Owner +
                   "#" + entry.Value.Identifier + "#" + entry.Value.transform.position.x.ToString("n2") + 
                   "#" + entry.Value.transform.position.y.ToString("n2") + "#" 
                   + entry.Value.transform.position.z.ToString("n2") + tempRot+"\n";
                    Connections[ConCounter - 1].Send(Encoding.ASCII.GetBytes(MSG));
                }
                //Create NetworkPlayerManager
                NetCreateObject(-1, ConCounter - 1, new Vector3(Connections[ConCounter -1].PlayerId*2-3,0,0));
                yield return new WaitForSeconds(.1f);
            }
        }
        public void ListenCallBack(System.IAsyncResult ar)
        {
            StartConnection = DateTime.Now;
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);
            TcpConnection temp = new TcpConnection();
            temp.TCPCon = handler;
            temp.PlayerId = ConCounter;
            ConCounter++;
            temp.Session = this;
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
                StopCoroutine(ListeningThread);
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
                lock(_objLock)
                {
                    if (type != -1)
                    {
                        temp = GameObject.Instantiate(SpawnPrefab[type], initPos, rotation);
                    }
                    else
                    {
                        temp = GameObject.Instantiate(NetworkPlayerManager, initPos, rotation);
                    }
                    temp.GetComponent<NetworkIdentifier>().Owner = ownMe;
                    temp.GetComponent<NetworkIdentifier>().Identifier = ObjectCounter;
                    temp.GetComponent<NetworkIdentifier>().Type = type;
                    NetObjs[ObjectCounter] = temp.GetComponent<NetworkIdentifier>();
                    ObjectCounter++;
                    string MSG = "CREATE#" + type + "#" + ownMe +
                    "#" + (ObjectCounter - 1) + "#" + initPos.x.ToString("n2") + "#" +
                    initPos.y.ToString("n2") + "#" + initPos.z.ToString("n2")+"#"+
                    rotation.x.ToString("n2")+"#" + rotation.y.ToString("n2") + "#" + rotation.z.ToString("n2") + "#" + rotation.w.ToString("n2")+ "\n";
                    lock(_masterMessage)
                    {
                        MasterMessage += MSG;
                    }
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
                if (NetObjs.ContainsKey(netIDBad))
                {
                    Destroy(NetObjs[netIDBad].gameObject);
                    NetObjs.Remove(netIDBad);
                }
            }
            catch
            {
                //Already been destroyed.
            }
            string msg = "DELETE#" + netIDBad+"\n";
            lock(_masterMessage)
            {
                MasterMessage += msg;
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