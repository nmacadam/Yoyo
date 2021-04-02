// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System.Collections;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Yoyo.Runtime
{
	public class TcpConnection
	{
        private int _playerId;
        private Socket _socket;
        private YoyoSession _session;

        private byte[] _buffer = new byte[1024];
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

        public TcpConnection(int playerId, Socket socket, YoyoSession session)
        {
            _playerId = playerId;
            _socket = socket;
            _session = session;
        }

        /// <summary>
        /// SEND STUFF
        /// This will deal with sending information across the current 
        /// NET_Connection's socket.
        /// </summary>
        /// <param name="byteData"></param>
        public void Send(byte[] byteData)
        {
            try
            {
                TCPCon.BeginSend(byteData, 0, byteData.Length, 0, new System.AsyncCallback(this.SendCallback), TCPCon);
                _tcpIsSending = true;
            }
            catch
            {
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


        /// <summary>
        /// RECEIVE FUNCTION
        /// </summary>

        public IEnumerator TCPRecv()
        {
            while (true)
            {
                bool IsRecv = false;
                while (!IsRecv)
                {
                    try
                    {
                        TCPCon.BeginReceive(_buffer, 0, 1024, 0, new System.AsyncCallback(TCPRecvCallback), this);
                        IsRecv = true;
                        break;
                    }
                    catch 
                    {
                        IsRecv = false;
                    }
                    yield return new WaitForSeconds(.1f);
                }
                //Wait to recv messages
                yield return new WaitUntil(() => _tcpDidReceive);
                _tcpDidReceive = false;
                string responce = _stringBuilder.ToString();
                //if (responce.Trim(' ') == "")
                //{
                    //We do NOT want any empty strings.  It will cause a problem.
                    //MyCore.Disconnect(PlayerId);
                //}
                //Parse string
                string[] commands = responce.Split('\n');
                for (int i = 0; i < commands.Length; i++)
                {
                    if (commands[i].Trim(' ') == "")
                    {
                        continue;
                    }
                    if (commands[i].Trim(' ') == "OK" && Session.Environment == YoyoEnvironment.Client)
                    {
                        Debug.Log("Client Recieved OK.");
                        //Do nothing, just a heartbeat
                    }
                    else if (commands[i].StartsWith("PLAYERID"))
                    {
                        if (Session.Environment == YoyoEnvironment.Client)
                        {
                            try
                            {
                                //This is how the client get's their player id.
                                //All values will be seperated by a '#' mark.
                                //PLayerID#<NUM> will signify the player ID for this connection.
                                PlayerId = int.Parse(commands[i].Split('#')[1]);
                                Session.LocalPlayerId = PlayerId;
                            }
                            catch (System.FormatException)
                            {
                                Debug.Log("Got scrambled Message: " + commands[i]);
                            }
                        }
                        else
                        {//Should never happen
                        }
                    }
                    else if (commands[i].StartsWith("DISCON#"))
                    {
                        if (Session.Environment == YoyoEnvironment.Server)
                        {
                            try
                            {
                                int badCon = int.Parse(commands[i].Split('#')[1]);
                                Session.Disconnect(badCon);
                                Debug.Log("There are now only " + Session.Connections.Count + " players in the game.");
                            }
                            catch (System.FormatException)
                            {
                                Debug.Log("We received a scrambled message+ " + commands[i]);
                            }
                            catch (System.Exception e)
                            {
                                Debug.Log("Unkown exception: " + e.ToString());
                            }
                        }
                        else
                        {//If client
                            Session.LeaveGame();
                        }
                    }
                    else if (commands[i].StartsWith("CREATE"))
                    {
                        if (Session.Environment == YoyoEnvironment.Client)
                        {
                            string[] arg = commands[i].Split('#');
                            try
                            {
                                int o = int.Parse(arg[2]);
                                int n = int.Parse(arg[3]);
                                Vector3 pos = new Vector3(float.Parse(arg[4]), float.Parse(arg[5]), float.Parse(arg[6]));
                                Quaternion qtemp = Quaternion.identity;
                                if (arg.Length >= 11)
                                {
                                    qtemp = new Quaternion(float.Parse(arg[7]), float.Parse(arg[8]), float.Parse(arg[9]), float.Parse(arg[10]));
                                }
                                

                                int type = int.Parse(arg[1]);
                                GameObject Temp;
                                if (type != -1)
                                {
                                    Temp = GameObject.Instantiate(Session.ContractPrefabs[int.Parse(arg[1])], pos, qtemp);
                                }
                                else
                                {
                                    Temp = GameObject.Instantiate(Session.NetworkPlayerManager, pos, qtemp);
                                }
                                Temp.GetComponent<NetworkIdentifier>().Owner = o;
                                Temp.GetComponent<NetworkIdentifier>().Identifier = n;
                                Temp.GetComponent<NetworkIdentifier>().Type = type;
                                Session.NetObjects[n] = Temp.GetComponent<NetworkIdentifier>();
                                /*lock(MyCore._masterMessage)
                                {   //Notify the server that we need to get update on this object.
                                    MyCore.MasterMessage += "DIRTY#" + n+"\n";
                                }*/
                            }
                            catch
                            {
                                //Malformed packet.
                            }
                        }
                    }
                    else if (commands[i].StartsWith("DELETE"))
                    {
                        if(Session.Environment == YoyoEnvironment.Client)
                        {
                            try
                            {
                                string[] args = commands[i].Split('#');
                                if (Session.NetObjects.ContainsKey(int.Parse(args[1])))
                                {
                                    GameObject.Destroy(Session.NetObjects[int.Parse(args[1])].gameObject);
                                    Session.NetObjects.Remove(int.Parse(args[1]));
                                }

                            }
                            catch (System.Exception e)
                            {
                                Debug.Log("ERROR OCCURED: " + e);
                            }
                        }
                    }
                    else if (commands[i].StartsWith("DIRTY"))
                    {
                        if(Session.Environment == YoyoEnvironment.Server)
                        {
                            int id = int.Parse(commands[i].Split('#')[1]);
                            if (Session.NetObjects.ContainsKey(id))
                            {
                                foreach (NetworkBehaviour n in Session.NetObjects[id].gameObject.GetComponents<NetworkBehaviour>())
                                {
                                    n.IsDirty = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        //We will assume it is Game Object specific message
                        //string msg = "COMMAND#" + myId.netId + "#" + var + "#" + value;
                        string[] args = commands[i].Split('#');
                        int n = int.Parse(args[1]);
                        if(Session.NetObjects.ContainsKey(n))
                        {
                            Session.NetObjects[n].Net_Update(args[0], args[2], args[3]);
                        }
                    }
                }

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
                int bytesRead = -1;
                bytesRead = TCPCon.EndReceive(ar);
                if (bytesRead > 0)
                {
                    this._stringBuilder.Append(Encoding.ASCII.GetString(this._buffer, 0, bytesRead));
                    string ts = this._stringBuilder.ToString();
                    if (ts[ts.Length - 1] != '\n')
                    {
                        TCPCon.BeginReceive(_buffer, 0, 1024, 0, new System.AsyncCallback(TCPRecvCallback), this);                      
                    }
                    else
                    {
                        this._tcpDidReceive = true;                    
                    }
                }
            }
            catch (System.Exception e)
            {
                //Cannot do anything here.  The callback is not allowed to disconnect.
            }
        }
	}
}