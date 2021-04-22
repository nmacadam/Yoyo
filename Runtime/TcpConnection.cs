// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System;
using System.Net.Sockets;
using UnityEngine;
using Yoyo.Attributes;

namespace Yoyo.Runtime
{
    [System.Serializable]
    public struct SocketParameters
    {
        [Tooltip("What is the packet buffer size for the socket?")]
        [NumberDropdown(512, 1024, 2048, 4096, 8192)] 
        public int BufferSize;
        [Tooltip("Should the socket use Nagle's Algorithm?")]
        public bool NoDelay;
    }

	public class TcpConnection
	{
        private int _playerId;
        private Socket _socket;
        private YoyoSession _session;

        private readonly int _dataBufferSize;

        private byte[] _buffer;
        private Packet receivedData = new Packet();

        private bool _tcpDidReceive = false;
        private bool _tcpIsSending = false;

        private bool _isDisconnecting = false;
        private bool _didDisconnect = false;

        public int PlayerId { get => _playerId; private set => _playerId = value; }
        public Socket Socket { get => _socket; private set => _socket = value; }
        public YoyoSession Session { get => _session; private set => _session = value; }

        public bool IsDisconnecting { get => _isDisconnecting; set => _isDisconnecting = value; }
        public bool DidDisconnect { get => _didDisconnect; set => _didDisconnect = value; }

        public TcpConnection(SocketParameters parameters, int playerId, Socket socket, YoyoSession session)
        {
            _playerId = playerId;
            _socket = socket;
            _session = session;

            _dataBufferSize = parameters.BufferSize;
            _buffer = new byte[_dataBufferSize];

            _socket.ReceiveBufferSize = _dataBufferSize;
            _socket.SendBufferSize = _dataBufferSize;

            _socket.NoDelay = parameters.NoDelay;
        }

        ~TcpConnection()
        {
            if (_socket != null && _socket.Connected)
            {
                try
                {
                    Socket.Shutdown(SocketShutdown.Both);
                    Socket.Close();
                }
                catch 
                {}
            }
        }

        /// <summary>
        /// SEND STUFF
        /// This will deal with sending information across the current 
        /// NET_Connection's socket.
        /// </summary>
        /// <param name="byteData"></param>
        //public void Send(byte[] byteData)
        public void Send(Packet packet)
        {
            try
            {
                //Debug.Log($"yoyo - pre write length: {packet.Length()} byte packet");

                packet.WriteLength();

                //Debug.Log($"yoyo - sending {packet.Length()} byte packet");

                Socket.BeginSend(packet.ToArray(), 0, packet.Length(), 0, new System.AsyncCallback(this.SendCallback), Socket);
                _tcpIsSending = true;
            }
            catch (Exception e)
            {
                Debug.Log("yoyo - encountered error when sending packet: " + e.ToString());
                DidDisconnect = true;
                //Can only happen when the server is pulled offline unexpectedly.
                _tcpIsSending = false;
            }
        }

        private void SendCallback(System.IAsyncResult ar)
        {
            try
            {
                _tcpIsSending = false;   
                if (IsDisconnecting && Session.Environment == YoyoEnvironment.Client)
                {
                    DidDisconnect = true;
                }
            }
            catch (System.Exception e)
            {
                Debug.Log("Sending Failed: "+e.ToString());
            }
        }

        public void BeginReceive()
        {
            if (_session == null || _session.ShuttingDown) return;

            Debug.Log("yoyo - socket is ready to receive packets");
            try
            {
                Socket.BeginReceive(_buffer, 0, _dataBufferSize, 0, TCPRecvCallback, this);
            }
            catch (Exception e)
            {
                Debug.Log("yoyo - error: " + e.ToString());
            }
        }

        private void TCPRecvCallback(System.IAsyncResult ar)
        {
            if (_session == null || _session.ShuttingDown) return;

            try
            {           
                //Debug.Log("yoyo - received packet...");
                int byteLength = Socket.EndReceive(ar);

                if (byteLength <= 0)
                {
                    // todo: disconnect
                    Debug.Log("yoyo - received packet was empty");
                    return;
                }

                //Debug.Log($"yoyo:packetInfo - received {byteLength} total bytes");

                byte[] data = new byte[byteLength];
                Array.Copy(_buffer, data, byteLength);

                receivedData.Reset(HandleData(data));
                Socket.BeginReceive(_buffer, 0, _dataBufferSize, 0, TCPRecvCallback, this);
            }
            catch (System.Exception e)
            {
                //Cannot do anything here.  The callback is not allowed to disconnect.
                //Debug.Log("yoyo - exception: " + e.ToString());
                //Socket.Shutdown(SocketShutdown.Both);
                //Socket.Close();
            }
        }

        private bool HandleData(byte[] data)
        {
            //Debug.Log("yoyo - handling packet data");
            int _packetLength = 0;

            receivedData.SetBytes(data);

            if (receivedData.UnreadLength() >= 4)
            {
                _packetLength = receivedData.ReadInt();
                //Debug.Log("yoyo:packetInfo - packet length: " + _packetLength);

                if (_packetLength <= 0)
                {
                    //Debug.Log("yoyo:packetInfo - completed received data read");
                    return true;
                }
            }

            while (_packetLength > 0 && _packetLength <= receivedData.UnreadLength())
            {
                byte[] _packetBytes = receivedData.ReadBytes(_packetLength);
                ThreadManager.ExecuteOnMainThread(() =>
                {
                    Packet packet = new Packet(_packetBytes);
                    PacketHeader header = packet.ReadHeader();
                    //int _packetId = _packet.ReadInt();
                    //Server.packetHandlers[_packetId](id, _packet);
                    //Debug.Log("yoyo:packetInfo - received packet type: " + (PacketType)header.PacketType);

                    if (Session.Environment == YoyoEnvironment.Client)
                    {
                        ClientPacketResponse(header, packet);
                    }
                    else if (Session.Environment == YoyoEnvironment.Server)
                    {
                        ServerPacketResponse(header, packet);
                    }
                });

                //Debug.Log("yoyo:packetInfo - remaining unread bytes: " + receivedData.UnreadLength());

                _packetLength = 0;
                if (receivedData.UnreadLength() >= 4)
                {
                    _packetLength = receivedData.ReadInt();
                    //Debug.Log("yoyo:packetInfo - packet length: " + _packetLength);

                    if (_packetLength <= 0)
                    {
                        //Debug.Log("yoyo:packetInfo - completed received data read");
                        return true;
                    }
                }
            }

            if (_packetLength <= 1)
            {
                //Debug.Log("yoyo:packetInfo - completed received data read");
                return true;
            }

            //Debug.Log("yoyo:packetInfo - packet read incomplete...");
            return false;
        }

        private void ClientPacketResponse(PacketHeader header, Packet packet)
        {
            // at this point the header has already been read
            PacketType packetType = (PacketType)header.PacketType;

            switch (packetType)
            {
                case PacketType.PlayerId:
                {
                    try
                    {
                        //This is how the client get's their player id.
                        //All values will be seperated by a '#' mark.
                        //PLayerID#<NUM> will signify the player ID for this connection.
                        PlayerId = packet.ReadInt();
                        Debug.Log("Assigned player id " + PlayerId);
                        Session.LocalPlayerId = PlayerId;
                    }
                    catch (System.FormatException)
                    {
                        Debug.Log("Got scrambled Message");
                    }
                    return;
                }
                case PacketType.Create:
                {
                    //string[] arg = commands[i].Split('#');
                    try
                    {
                        int type = packet.ReadInt();
                        int owner = packet.ReadInt();
                        int netId = packet.ReadInt();

                        Debug.Log($"Creating object: (prefab id: {type}, owner: {owner}, netId: {netId})");

                        Vector3 position = packet.ReadVector3();
                        Quaternion rotation = packet.ReadQuaternion();
                        
                        GameObject go = GameObject.Instantiate(Session.NetworkContract.GetPrefab(type), position, rotation);
                        // if (type != -1)
                        // {
                        //     go = GameObject.Instantiate(Session.ContractPrefabs[type], position, rotation);
                        // }
                        // else
                        // {
                        //     go = GameObject.Instantiate(Session.NetworkPlayerManager, position, rotation);
                        // }
                        go.GetComponent<NetworkEntity>().Owner = owner;
                        go.GetComponent<NetworkEntity>().Identifier = netId;
                        //go.GetComponent<NetworkEntity>().Type = type;
                        Session.NetEntities[netId] = go.GetComponent<NetworkEntity>();
                    }
                    catch
                    {
                        //Malformed packet.
                    }
                    return;
                }
                case PacketType.Destroy:
                {
                    try
                    {
                        int badId = packet.ReadInt();
                        if (Session.NetEntities.ContainsKey(badId))
                        {
                            GameObject.Destroy(Session.NetEntities[badId].gameObject);
                            Session.NetEntities.Remove(badId);
                        }

                    }
                    catch (System.Exception e)
                    {
                        Debug.Log("ERROR OCCURED: " + e);
                    }
                    return;
                }
                case PacketType.Dirty:
                {
                    return;
                }
                case PacketType.Disconnect:
                {
                    Session.LeaveGame();
                    return;
                }
                case PacketType.Command:
                {
                    PassPacketToObject(packetType, packet);
                    return;
                }
                case PacketType.Update:
                {
                    PassPacketToObject(packetType, packet);
                    return;
                }
                default: 
                {
                    PassPacketToObject(packetType, packet);
                    return;
                }
            }
        }

        private void ServerPacketResponse(PacketHeader header, Packet packet)
        {
            // at this point the header has already been read
            PacketType packetType = (PacketType)header.PacketType;

            switch (packetType)
            {
                case PacketType.PlayerId:
                {
                    // do nothing
                    return;
                }
                case PacketType.Create:
                {
                    return;
                }
                case PacketType.Destroy:
                {
                    return;
                }
                case PacketType.Dirty:
                {
                    int id = packet.ReadInt();
                    if (Session.NetEntities.ContainsKey(id))
                    {
                        foreach (NetworkBehaviour n in Session.NetEntities[id].gameObject.GetComponents<NetworkBehaviour>())
                        {
                            n.IsDirty = true;
                        }
                    }
                    return;
                }
                case PacketType.Disconnect:
                {
                    try
                    {
                        // ! could be wrong!
                        int badCon = packet.ReadInt();
                        Session.DisconnectClient(badCon);
                        Debug.Log("There are now only " + Session.Connections.Count + " players in the game.");
                    }
                    catch (System.FormatException)
                    {
                        Debug.Log("We received a scrambled message");
                    }
                    catch (System.Exception e)
                    {
                        Debug.Log("Unknown exception: " + e.ToString());
                    }
                    return;
                }
                case PacketType.Command:
                {
                    PassPacketToObject(packetType, packet);
                    return;
                }
                case PacketType.Update:
                {
                    PassPacketToObject(packetType, packet);
                    return;
                }
                default: 
                {
                    PassPacketToObject(packetType, packet);
                    return;
                }
            }
        }

        private void PassPacketToObject(PacketType type, Packet packet)
        {
            int netId = packet.ReadInt();
            //Debug.Log("Passing to NetID: " + netId);
            if(Session.NetEntities.ContainsKey(netId))
            {
                ThreadManager.ExecuteOnMainThread(() => Session.NetEntities[netId].Net_Update(type, packet));
            }
        }
	}
}