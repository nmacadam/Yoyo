// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System;
using System.Collections;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Yoyo.Runtime
{
    public struct SocketParameters
    {
        public int BufferSize;
        public bool NoDelay;
    }

	public class TcpConnection
	{
        private int _playerId;
        private Socket _socket;
        private YoyoSession _session;

        //public const int DataBufferSize = 2048;

        private readonly int _dataBufferSize;

        private byte[] _buffer;// = new byte[DataBufferSize];
        private Packet receivedData = new Packet();

        //private int _bytesReceived = 0;
        private StringBuilder _stringBuilder = new StringBuilder();
        private bool _tcpDidReceive = false;
        private bool _tcpIsSending = false;

        private bool _isDisconnecting = false;
        private bool _didDisconnect = false;

        public int PlayerId { get => _playerId; private set => _playerId = value; }
        public Socket TCPCon { get => _socket; private set => _socket = value; }
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

            //_socket.ReceiveBufferSize = DataBufferSize;
            //_socket.SendBufferSize = DataBufferSize;
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
                Debug.Log($"yoyo - pre write length: {packet.Length()} byte packet");

                packet.WriteLength();

                Debug.Log($"yoyo - sending {packet.Length()} byte packet");

                TCPCon.BeginSend(packet.ToArray(), 0, packet.Length(), 0, new System.AsyncCallback(this.SendCallback), TCPCon);
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
            Debug.Log("yoyo - socket is ready to receive packets");
            try
            {
                TCPCon.BeginReceive(_buffer, 0, _dataBufferSize, 0, TCPRecvCallback, this);
            }
            catch (Exception e)
            {
                Debug.Log("yoyo - error: " + e.ToString());
            }
        }

        /// <summary>
        /// RECEIVE FUNCTION
        /// </summary>

        public IEnumerator TCPRecv()
        {
            Debug.Log("yoyo - socket is ready to receive packets");

            while (true)
            {
                Debug.Log("yoyo - A");
                bool IsRecv = false;
                while (!IsRecv)
                {
                    try
                    {
                        TCPCon.BeginReceive(_buffer, 0, _dataBufferSize, 0, new System.AsyncCallback(TCPRecvCallback), this);
                        IsRecv = true;
                        break;
                    }
                    catch 
                    {
                        IsRecv = false;
                    }
                    yield return new WaitForSeconds(.1f);
                }
                Debug.Log("yoyo - B");
                //Wait to recv messages
                yield return new WaitUntil(() => _tcpDidReceive);
                _tcpDidReceive = false;
                Debug.Log("yoyo - C");
                //string responce = _stringBuilder.ToString();
                //if (responce.Trim(' ') == "")
                //{
                    //We do NOT want any empty strings.  It will cause a problem.
                    //MyCore.Disconnect(PlayerId);
                //}

                
                // Packet packet = new Packet(_buffer);

                // int packetLength = packet.ReadLength();

                

                // PacketHeader header = packet.ReadHeader();

                // Debug.Log("received: " + (PacketType)header.PacketType);

                // if (Session.Environment == YoyoEnvironment.Client)
                // {
                //     ClientPacketResponse(header, packet);
                // }
                // else if (Session.Environment == YoyoEnvironment.Server)
                // {
                //     ServerPacketResponse(header, packet);
                // }

                // if (packetLength < _bytesReceived)
                // {
                //     Debug.Log("received more than one packet");

                //     byte[] nextBuffer = new byte[_bytesReceived - packetLength];
                //     System.Array.ConstrainedCopy(_buffer, packetLength, nextBuffer, 0, _bytesReceived - packetLength);
                //     Packet next = new Packet(nextBuffer);

                //     // ! should make new packet here
                //     packet.ReadLength();
                //     PacketHeader header2 = packet.ReadHeader();
                //     Debug.Log("second packet: " + (PacketType)header2.PacketType);
                // }



                // //Parse string
                // string[] commands = responce.Split('\n');
                // for (int i = 0; i < commands.Length; i++)
                // {
                //     if (commands[i].Trim(' ') == "")
                //     {
                //         continue;
                //     }
                //     if (commands[i].Trim(' ') == "OK" && Session.Environment == YoyoEnvironment.Client)
                //     {
                //         Debug.Log("Client Recieved OK.");
                //         //Do nothing, just a heartbeat
                //     }
                //     else if (commands[i].StartsWith("PLAYERID"))
                //     {
                //         if (Session.Environment == YoyoEnvironment.Client)
                //         {
                //             try
                //             {
                //                 //This is how the client get's their player id.
                //                 //All values will be seperated by a '#' mark.
                //                 //PLayerID#<NUM> will signify the player ID for this connection.
                //                 PlayerId = int.Parse(commands[i].Split('#')[1]);
                //                 Session.LocalPlayerId = PlayerId;
                //             }
                //             catch (System.FormatException)
                //             {
                //                 Debug.Log("Got scrambled Message: " + commands[i]);
                //             }
                //         }
                //         else
                //         {//Should never happen
                //         }
                //     }
                //     else if (commands[i].StartsWith("DISCON#"))
                //     {
                //         if (Session.Environment == YoyoEnvironment.Server)
                //         {
                //             try
                //             {
                //                 int badCon = int.Parse(commands[i].Split('#')[1]);
                //                 Session.Disconnect(badCon);
                //                 Debug.Log("There are now only " + Session.Connections.Count + " players in the game.");
                //             }
                //             catch (System.FormatException)
                //             {
                //                 Debug.Log("We received a scrambled message+ " + commands[i]);
                //             }
                //             catch (System.Exception e)
                //             {
                //                 Debug.Log("Unkown exception: " + e.ToString());
                //             }
                //         }
                //         else
                //         {//If client
                //             Session.LeaveGame();
                //         }
                //     }
                //     else if (commands[i].StartsWith("CREATE"))
                //     {
                //         if (Session.Environment == YoyoEnvironment.Client)
                //         {
                //             string[] arg = commands[i].Split('#');
                //             try
                //             {
                //                 int o = int.Parse(arg[2]);
                //                 int n = int.Parse(arg[3]);
                //                 Vector3 pos = new Vector3(float.Parse(arg[4]), float.Parse(arg[5]), float.Parse(arg[6]));
                //                 Quaternion qtemp = Quaternion.identity;
                //                 if (arg.Length >= 11)
                //                 {
                //                     qtemp = new Quaternion(float.Parse(arg[7]), float.Parse(arg[8]), float.Parse(arg[9]), float.Parse(arg[10]));
                //                 }
                                

                //                 int type = int.Parse(arg[1]);
                //                 GameObject Temp;
                //                 if (type != -1)
                //                 {
                //                     Temp = GameObject.Instantiate(Session.ContractPrefabs[int.Parse(arg[1])], pos, qtemp);
                //                 }
                //                 else
                //                 {
                //                     Temp = GameObject.Instantiate(Session.NetworkPlayerManager, pos, qtemp);
                //                 }
                //                 Temp.GetComponent<NetworkIdentifier>().Owner = o;
                //                 Temp.GetComponent<NetworkIdentifier>().Identifier = n;
                //                 Temp.GetComponent<NetworkIdentifier>().Type = type;
                //                 Session.NetObjects[n] = Temp.GetComponent<NetworkIdentifier>();
                //                 /*lock(MyCore._masterMessage)
                //                 {   //Notify the server that we need to get update on this object.
                //                     MyCore.MasterMessage += "DIRTY#" + n+"\n";
                //                 }*/
                //             }
                //             catch
                //             {
                //                 //Malformed packet.
                //             }
                //         }
                //     }
                //     else if (commands[i].StartsWith("DELETE"))
                //     {
                //         if(Session.Environment == YoyoEnvironment.Client)
                //         {
                //             try
                //             {
                //                 string[] args = commands[i].Split('#');
                //                 if (Session.NetObjects.ContainsKey(int.Parse(args[1])))
                //                 {
                //                     GameObject.Destroy(Session.NetObjects[int.Parse(args[1])].gameObject);
                //                     Session.NetObjects.Remove(int.Parse(args[1]));
                //                 }

                //             }
                //             catch (System.Exception e)
                //             {
                //                 Debug.Log("ERROR OCCURED: " + e);
                //             }
                //         }
                //     }
                //     else if (commands[i].StartsWith("DIRTY"))
                //     {
                //         if(Session.Environment == YoyoEnvironment.Server)
                //         {
                //             int id = int.Parse(commands[i].Split('#')[1]);
                //             if (Session.NetObjects.ContainsKey(id))
                //             {
                //                 foreach (NetworkBehaviour n in Session.NetObjects[id].gameObject.GetComponents<NetworkBehaviour>())
                //                 {
                //                     n.IsDirty = true;
                //                 }
                //             }
                //         }
                //     }
                //     else
                //     {
                //         //We will assume it is Game Object specific message
                //         //string msg = "COMMAND#" + myId.netId + "#" + var + "#" + value;
                //         string[] args = commands[i].Split('#');
                //         int n = int.Parse(args[1]);
                //         if(Session.NetObjects.ContainsKey(n))
                //         {
                //             Session.NetObjects[n].Net_Update(args[0], args[2], args[3]);
                //         }
                //     }
                // }

                //_bytesReceived = 0;
                
                _stringBuilder.Length = 0;
                _stringBuilder = new StringBuilder();
                _tcpDidReceive = false;
                yield return new WaitForSeconds(.01f);//This will prevent traffic from stalling other co-routines.
            }
        }

        private void TCPRecvCallback(System.IAsyncResult ar)
        {
            try
            {           
                Debug.Log("yoyo - received packet...");
                int byteLength = TCPCon.EndReceive(ar);

                if (byteLength <= 0)
                {
                    // todo: disconnect
                    Debug.Log("yoyo - received packet was empty");
                    return;
                }

                Debug.Log($"yoyo:packetInfo - received {byteLength} total bytes");

                //this._tcpDidReceive = true;

                byte[] data = new byte[byteLength];
                Array.Copy(_buffer, data, byteLength);

                receivedData.Reset(HandleData(data));
                TCPCon.BeginReceive(_buffer, 0, _dataBufferSize, 0, TCPRecvCallback, this);

                // if (byteLength > 0)
                // {
                //     Debug.Log($"yoyo - received {byteLength} bytes");

                //     this._tcpDidReceive = true;

                //     byte[] data = new byte[byteLength];
                //     Array.Copy(_buffer, data, byteLength);

                //     receivedData.Reset(HandleData(data));
                // }
                // else
                // {
                //     Debug.Log("yoyo - received packet was empty");
                // }

                // if (_bytesReceived > 0)
                // {
                //     this._tcpDidReceive = true;
                //     // this._stringBuilder.Append(Encoding.ASCII.GetString(this._buffer, 0, bytesRead));
                //     // string ts = this._stringBuilder.ToString();

                //     // Packet packet = new Packet(_buffer);
                //     // int packetLength = packet.ReadLength(false);

                //     // //Debug.Log("received: " + (PacketType)header.PacketType);

                //     // // ! handle this
                //     // if (ts[ts.Length - 1] != '\n') // keep reading if not end of packet
                //     // {
                //     //     //TCPCon.BeginReceive(_buffer, 0, 1024, 0, new System.AsyncCallback(TCPRecvCallback), this);                      
                //     //     TCPCon.BeginReceive(_buffer, 0, packetLength, 0, new System.AsyncCallback(TCPRecvCallback), this);                      
                //     // }
                //     // else
                //     // {
                //     //     this._tcpDidReceive = true;                    
                //     // }
                // }
            }
            catch (System.Exception e)
            {
                //Cannot do anything here.  The callback is not allowed to disconnect.
                //Debug.Log("yoyo - exception: " + e.ToString());
                TCPCon.Shutdown(SocketShutdown.Both);
                TCPCon.Close();
            }
        }

        private bool HandleData(byte[] data)
        {
            Debug.Log("yoyo - handling packet data");
            int _packetLength = 0;

            receivedData.SetBytes(data);

            if (receivedData.UnreadLength() >= 4)
            {
                _packetLength = receivedData.ReadInt();
                Debug.Log("yoyo:packetInfo - packet length: " + _packetLength);

                if (_packetLength <= 0)
                {
                    Debug.Log("yoyo:packetInfo - completed received data read");
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
                    Debug.Log("yoyo:packetInfo - received packet type: " + (PacketType)header.PacketType);

                    if (Session.Environment == YoyoEnvironment.Client)
                    {
                        ClientPacketResponse(header, packet);
                    }
                    else if (Session.Environment == YoyoEnvironment.Server)
                    {
                        ServerPacketResponse(header, packet);
                    }
                });

                Debug.Log("yoyo:packetInfo - remaining unread bytes: " + receivedData.UnreadLength());

                _packetLength = 0;
                if (receivedData.UnreadLength() >= 4)
                {
                    _packetLength = receivedData.ReadInt();
                    Debug.Log("yoyo:packetInfo - packet length: " + _packetLength);

                    if (_packetLength <= 0)
                    {
                        Debug.Log("yoyo:packetInfo - completed received data read");
                        return true;
                    }
                }
            }

            if (_packetLength <= 1)
            {
                Debug.Log("yoyo:packetInfo - completed received data read");
                return true;
            }

            Debug.Log("yoyo:packetInfo - packet read incomplete...");
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

                        Debug.Log($"Creating object: (type: {type}, owner: {owner}, netId: {netId})");

                        Vector3 position = packet.ReadVector3();

                        // ! could cause an issue?
                        Quaternion rotation = packet.ReadQuaternion();

                        // int o = int.Parse(arg[2]);
                        // int n = int.Parse(arg[3]);
                        // Vector3 pos = new Vector3(float.Parse(arg[4]), float.Parse(arg[5]), float.Parse(arg[6]));
                        // Quaternion qtemp = Quaternion.identity;
                        // if (arg.Length >= 11)
                        // {
                        //     qtemp = new Quaternion(float.Parse(arg[7]), float.Parse(arg[8]), float.Parse(arg[9]), float.Parse(arg[10]));
                        // }
                        

                        //int type = int.Parse(arg[1]);
                        GameObject Temp;
                        if (type != -1)
                        {
                            Temp = GameObject.Instantiate(Session.ContractPrefabs[type], position, rotation);
                        }
                        else
                        {
                            Temp = GameObject.Instantiate(Session.NetworkPlayerManager, position, rotation);
                        }
                        Temp.GetComponent<NetworkIdentifier>().Owner = owner;
                        Temp.GetComponent<NetworkIdentifier>().Identifier = netId;
                        Temp.GetComponent<NetworkIdentifier>().Type = type;
                        Session.NetObjects[netId] = Temp.GetComponent<NetworkIdentifier>();
                        /*lock(MyCore._masterMessage)
                        {   //Notify the server that we need to get update on this object.
                            MyCore.MasterMessage += "DIRTY#" + n+"\n";
                        }*/
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
                        if (Session.NetObjects.ContainsKey(badId))
                        {
                            GameObject.Destroy(Session.NetObjects[badId].gameObject);
                            Session.NetObjects.Remove(badId);
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
                    int id = packet.ReadInt();//int.Parse(commands[i].Split('#')[1]);
                    if (Session.NetObjects.ContainsKey(id))
                    {
                        foreach (NetworkBehaviour n in Session.NetObjects[id].gameObject.GetComponents<NetworkBehaviour>())
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
                        //int badCon = int.Parse(commands[i].Split('#')[1]);
                        Session.Disconnect(badCon);
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
            // ! might need this
            //int type = packet.ReadInt();
            int netId = packet.ReadInt();
            Debug.Log("Passing to NetID: " + netId);
            //string flag = packet.ReadString();
            //string value = packet.ReadString();
            if(Session.NetObjects.ContainsKey(netId))
            {
                ThreadManager.ExecuteOnMainThread(() => Session.NetObjects[netId].Net_Update(type, packet));
                //Session.NetObjects[netId].Net_Update(type, packet);
            }
        }
	}
}