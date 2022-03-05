using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace Survi4s_Server
{
    class Player
    {
        public string myName { get; private set; }
        public string myId { get; private set; }

        public TcpClient tcp { get; private set; }

        private Server server;

        public NetworkStream networkStream { get; private set; }
        private Room myRoom;
        private bool isMaster;
        private bool isOnline;

        public enum PlayerState { online, room }

        public PlayerState state { get; private set; }

        // Encryption ---------------------------------------------------------------
        private RsaEncryption rsaEncryption;

        // Private key --------------------------------------------------------------
        private string PrivateKeyFile = "Private-Key.txt";

        // Constructor --------------------------------------------------------------
        public Player(TcpClient tcp, Server server)
        {
            this.tcp = tcp;
            this.server = server;

            networkStream = tcp.GetStream();
            isOnline = true;

            state = PlayerState.online;

            rsaEncryption = new RsaEncryption(GetPrivateKey());

            // Begin verification process --------------------------------------------
             BeginVerification();
        }

        private string GetPrivateKey()
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), PrivateKeyFile));
        }

        private void BeginVerification()
        {
            // Wait to receive client ID + name -------------------------------------
            BinaryFormatter formatter = new BinaryFormatter();
            string answer = formatter.Deserialize(networkStream) as string;

            // Try to decrypt it ----------------------------------------------------
            string data = "";
            data = rsaEncryption.Decrypt(answer, rsaEncryption.serverPrivateKey);

            // If failed, close client connection ----------------------------------
            if(data.Length == 0)
            {
                tcp.Close();
                return;
            }

            // If success, save the id and name -------------------------------------
            myId = data.Substring(0, 6);
            myName = data.Substring(6, (data.Length - 6));
            Console.WriteLine(myId + " " + myName + " is Connected");

            // Add this player to online list ---------------------------------------
            server.onlineList.Add(this);

            // Send feedback to client ----------------------------------------------
            data = "Ok";
            formatter.Serialize(networkStream, data);

            // Begin listening to client --------------------------------------------
            BeginNormalCommunication();
        }
        private void BeginNormalCommunication()
        {
            // Thread for receiving massage ----------------------------------------------
            Thread recieveThread = new Thread(ReceivedMassage);

            // Thread for checking connection --------------------------------------------
            //Thread checkConnectionThread = new Thread(CheckConnection);

            // Start all thread ----------------------------------------------------------
            recieveThread.Start();
            //checkConnectionThread.Start();
        }

        // Receive and proccess client massage here --------------------------------------
        private void ReceivedMassage()
        {
            // Massage format : target|header|data|data|data...
            // Target code : 1.All  2.Server  3.All except Sender  others:Specific player name
            // Massage code in : https://docs.google.com/spreadsheets/d/1vVT-tvdHMXsiBQaf16NSQZUbZoGLrR3Ub53RYXnhZtw/edit#gid=0

            while (isOnline)
            {
                if (networkStream.DataAvailable)
                {
                    // Receive and split massage here ------------------------------------
                    BinaryFormatter formatter = new BinaryFormatter();
                    string data = formatter.Deserialize(networkStream) as string;
                    string[] info = data.Split("|");
                    
                    if(info[0] == "1")
                    {
                        // Send massage to all client in room ----------------------------
                        SendMassage("1", data.Substring(2, (data.Length - 2)));
                    }
                    else if (info[0] == "2")
                    {
                        if (info[1] == "StMtc")
                        {
                            MatchMaking();
                        }
                        else if (info[1] == "CrR")
                        {
                            CreateRoom(info[2], int.Parse(info[3]), bool.Parse(info[4]));
                        }
                        else if (info[1] == "JnR")
                        {
                            JoinRoom(info[2]);
                        }
                        else if (info[1] == "ExR")
                        {
                            ExitRoom();
                        }
                        else if (info[1] == "LcR")
                        {
                            myRoom.LockRoom();
                        }
                    }
                    else if (info[0] == "3")
                    {

                    }
                    else
                    {

                    }
                }
            }
        }

        // Send Massage method ----------------------------------------------------------
        private void SendMassage(string target, string massage)
        {
            // Massage format : sender|header|data|data|data...
            // Target code : 1.All  2.Server  3.All except Sender   others:Specific player name
            string[] temp = new string[1];
            temp[0] = massage;

            SendMassage(target, temp);
        }
        private void SendMassage(string target, string[] massage)
        {
            // Massage format : sender|header|data|data|data...
            // Target code : 1.All  2.Server  3.All except Sender   others:Specific player name

            string data = myId+myName;
            foreach(string x in massage)
            {
                data += "|" + x;
            }

            // Send massage according to target ---------------------------------------------
            if(target == "1")
            {
                foreach(Player x in myRoom.players)
                {
                    SendSerializationDataHandler(x, data);
                }
            }
            else if (target == "3")
            {
                foreach (Player x in myRoom.players)
                {
                    if(x.tcp != tcp)
                    {
                        SendSerializationDataHandler(x, data);
                    }
                }
            }
            else
            {
                foreach (Player x in myRoom.players)
                {
                    if ((x.myId + x.myName) == (target))
                    {
                        SendSerializationDataHandler(x, data);
                    }
                }
            }
        }
        private void SendMassage(string massage)
        {
            string[] msg = new string[] { massage };
            SendMassage(msg);
        }
        private void SendMassage(string[] massage)
        {
            // Massage format : sender|header|data|data|data...

            string data = "Svr";
            foreach(string x in massage)
            {
                data += "|" + x;
            }
            SendSerializationDataHandler(this, data);
        }

        private void SendSerializationDataHandler(Player player, string Thedata)
        {
            // Try to send, if filed, let's just assume player disconnected ------------------
            try
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(player.networkStream, Thedata);
            }
            catch (Exception e)
            {
                Console.WriteLine("Send massage error from " + player.myId + " " + player.myName + " : " + e.Message);
                // Disconnect client from server
                SuddenDisconnect();
            }
        }

        // Matchmaking method ------------------------------------------------------
        private void MatchMaking()
        {
            // Check if there is room in list --------------------------------------
            if (server.roomList.Count > 0)
            {
                for(int i = 0; i < server.roomList.Count; i++)
                {
                    // If we found the room ----------------------------------------
                    if (server.roomList[i].CanJoinPublic())
                    {
                        server.onlineList.Remove(this);
                        server.roomList[i].players.Add(this);

                        myRoom = server.roomList[i];

                        // Send massage to client that we got the room ------------
                        string[] massage = new string[] { "RJnd", myRoom.roomName, myRoom.players.Count.ToString() };
                        SendMassage(massage);

                        state = PlayerState.room;

                        return;
                    }
                }
            }

            // Just make new room if there is no room can be joined ----------------
            CreateRoom();
        }

        // Create Room method ------------------------------------------------------
        private void CreateRoom()
        {
            // Default Room setting ------------------------------------------------
            CreateRoom(myName + myId + "Room", 4, true);
        }
        private void CreateRoom(string roomName, int maxPlayer, bool isPublic)
        {
            // Create new class room -----------------------------------------------
            Room temp = new Room(roomName, maxPlayer, isPublic);
            server.roomList.Add(temp);
            myRoom = temp;

            // Remove player from online list --------------------------------------
            server.onlineList.Remove(this);
            myRoom.players.Add(this);

            // Room creator is master of the room ----------------------------------
            isMaster = true;

            // Send massage to client that we create the room ----------------------
            string[] massage = new string[] { "RCrd", myRoom.roomName };
            SendMassage(massage);

            state = PlayerState.room;
        }
        
        // Join Room method --------------------------------------------------------
        private void JoinRoom(string roomName)
        {
            // Find the correct room name ------------------------------------------
            foreach(Room x in server.roomList)
            {
                // Find The room ---------------------------------------------------
                if(x.roomName == roomName)
                {
                    // Check if the room still can be joined ----------------------
                    if (x.CanJoinPrivate())
                    {
                        // Join  the room ------------------------------------------
                        server.onlineList.Remove(this);
                        x.players.Add(this);
                        myRoom = x;

                        // Send massage to client that we has been joined to room ------
                        string[] massage = new string[] { "RJnd", myRoom.roomName, myRoom.players.Count.ToString() };
                        SendMassage(massage);

                        // Send massage to other client that we join the room ----------
                        massage = new string[] { "PlCt", myRoom.players.Count.ToString() };
                        SendMassage("3", massage);

                        state = PlayerState.room;

                        return;
                    }

                    // Send massage that the room is full ------------------------
                    SendMassage("RsF");
                }
            }

            // Send massage to client that no room can be joined -------------------
            SendMassage("RnFd");
        }

        // Exit Room -------------------------------------------------------------
        private void ExitRoom()
        {
            // Check there is other players in room ----------------------------------
            if (myRoom.players.Count > 1)
            {
                // Tell others that we left ------------------------------------------
                SendMassage("3", "LRm");

                // Check if we are the master of room --------------------------------
                if (isMaster)
                {
                    // Set other player to master ------------------------------------
                    foreach (Player x in myRoom.players)
                    {
                        if (x.tcp != tcp)
                        {
                            x.SetToMaster();
                            return;
                        }
                    }
                }
            }
            else
            {
                server.roomList.Remove(myRoom);
            }

            isMaster = false;
            myRoom.players.Remove(this);
            server.onlineList.Add(this);

            myRoom = null;
            state = PlayerState.online;

            // Send massage to client --------------------------------------------
            SendMassage("REx");
        }

        // Sudden disconnect -----------------------------------------------------
        private void SuddenDisconnect()
        {
            // Check client position
            if (state == PlayerState.online)
            {
                // Remove from online list
                server.onlineList.Remove(this);
            }
            else if (state == PlayerState.room)
            {
                // Remove from room list
                myRoom.players.Remove(this);

                // Tell other player in room
                SendMassage("3", "LRm");
            }
        }

        // Set this player to master of room -------------------------------------
        public void SetToMaster()
        {
            isMaster = true;
            // Send massage to client --------------------------------------------
            SendMassage("SeMs");
        }
    }
}
