// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System.Collections;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Yoyo.Runtime
{
	public partial class YoyoSession : MonoBehaviour
	{
		/// <summary>
        /// Client Functions 
        /// Start Client - Will join with a server specified
        /// at IpAddress and Port.
        /// </summary>
        public void StartClient()
        {
            if (!IsConnected)
            {
                Debug.Log("yoyo - starting client");
                StartCoroutine(ConnectingClient());
            }
        }

        public IEnumerator ConnectingClient()
        {
            _environment = YoyoEnvironment.None;
            IsConnected = false;
            CurrentlyConnecting = false;
            //Setup our socket
            IPAddress ip = (IPAddress.Parse(_ipAddressString));
            IPEndPoint endP = new IPEndPoint(ip, _port);
            Socket clientSocket = new Socket(ip.AddressFamily, SocketType.Stream,
                ProtocolType.Tcp);
            //Connect client
            clientSocket.BeginConnect(endP, ConnectingCallback, clientSocket);
            Debug.Log("Trying to wait for server...");
            //Wait for the client to connect
            while(!CurrentlyConnecting)
            {
                yield return new WaitForSeconds(MasterTimer);
            }
            //yield return new WaitUntil(() => CurrentlyConnecting);
            //StartCoroutine(Connections[0].TCPRecv());  //It is 0 on the client because we only have 1 socket.
            Connections[0].BeginReceive();
            StartCoroutine(SlowUpdate());  //This will allow the client to send messages to the server.
        }

        public void ConnectingCallback(System.IAsyncResult ar)
        {
            // Client will use the con list (but only have one entry).
            _environment = YoyoEnvironment.Client;
            TcpConnection temp = new TcpConnection(0, (Socket)ar.AsyncState, this);
            temp.TCPCon.EndConnect(ar);//This finishes the TCP connection (DOES NOT DISCONNECT)    
            IsConnected = true;
            Connections.Add(0, temp);
            CurrentlyConnecting = true;
        }
	}
}