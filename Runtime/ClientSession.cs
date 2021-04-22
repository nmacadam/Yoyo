// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Yoyo.Runtime
{
	public partial class YoyoSession : MonoBehaviour
	{
        private Action _onNoServerResponse;

        public Action OnNoServerResponse { get => _onNoServerResponse; set => _onNoServerResponse = value; }

        /// <summary>
        /// Client Functions 
        /// Start Client - Will join with a server specified
        /// at IpAddress and Port.
        /// </summary>
        public void StartClient()
        {
            if (!IsConnected)
            {
                Initialize();

                Debug.Log("yoyo - starting client");
                StartCoroutine(ConnectingClient());
            }
        }

        public IEnumerator ConnectingClient()
        {
            _environment = YoyoEnvironment.None;
            IsConnected = false;
            CurrentlyConnecting = false;

            // Setup our socket
            IPAddress ip = (IPAddress.Parse(_ipAddressString));
            IPEndPoint endP = new IPEndPoint(ip, _port);
            Socket clientSocket = new Socket(ip.AddressFamily, SocketType.Stream,
                ProtocolType.Tcp);
            
            // Connect client
            clientSocket.BeginConnect(endP, ConnectingCallback, clientSocket);

            Debug.Log("yoyo - trying to wait for server...");
            // Wait for the client to connect
            while(!CurrentlyConnecting)
            {
                yield return new WaitForSecondsRealtime(MasterTimer);
            }

            StartCoroutine(CancelIfNoResponse());

            Connections[0].BeginReceive(); // It is 0 on the client because we only have 1 socket.
            StartCoroutine(SlowUpdate());  // This will allow the client to send messages to the server.
        }

        private IEnumerator CancelIfNoResponse()
        {
            float _t = Time.unscaledTime;

            while(_localPlayerId == -1)
            {
                if (Time.unscaledTime - _t > 10f)
                {
                    _onNoServerResponse?.Invoke();
                    yield break;
                }

                yield return null;
            }
        }

        public void ConnectingCallback(System.IAsyncResult ar)
        {
            // Client will use the con list (but only have one entry).
            _environment = YoyoEnvironment.Client;
            TcpConnection temp = new TcpConnection(_tcpParameters, 0, (Socket)ar.AsyncState, this);
            temp.Socket.EndConnect(ar);// This finishes the TCP connection
            IsConnected = true;
            Connections.Add(0, temp);
            CurrentlyConnecting = true;
        }
	}
}