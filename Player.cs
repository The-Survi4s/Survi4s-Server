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

        private int DefaultCheckTime;
        private int checkTime;

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

            DefaultCheckTime = 5000;
            checkTime = DefaultCheckTime;

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
            Console.WriteLine(myId + " " + myName + " Connected");
            isOnline = true;

            // Add this player to online list ---------------------------------------
            server.onlineList.Add(this);
            Console.WriteLine("Player Online : " + server.onlineList.Count);

            // Send feedback to client ----------------------------------------------
            data = "Ok";
            formatter.Serialize(networkStream, data);

            // Begin listening to client --------------------------------------------
            BeginNormalCommunication();
        }
        private void BeginNormalCommunication()
        {
            // Thread for receiving massage ----------------------------------------------
            Thread recieveThread = new Thread(ReceivedMessage);

            // Check client online status ------------------------------------------------
            Thread onlineStatus = new Thread(CheckOnlineStatus);

            // Start all thread ----------------------------------------------------------
            recieveThread.Start();
            onlineStatus.Start();
        }     

        private void CheckOnlineStatus()
        {
            while (isOnline)
            {
                Thread.Sleep(DefaultCheckTime);
                if (checkTime == 0)
                {
                    SuddenDisconnect();
                }
                else
                {
                    checkTime = 0;
                }
            }
        }

        // Receive and proccess client massage here --------------------------------------
        private void ReceivedMessage()
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
                        SendMessage("1", data.Substring(2, (data.Length - 2)));
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
                        else if (info[1] == "ChNm")
                        {
                            ChangeName(info[2], info[3]);
                        }
                    }
                    else if (info[0] == "3")
                    {

                    }
                    else
                    {
                        if (myRoom == null)
                            return;

                        foreach(Player x in myRoom.players)
                        {
                            if(x.myName == info[0])
                            {
                                x.SendMessage(data);
                            }    
                        }
                    }

                    checkTime = DefaultCheckTime;
                }
            }
        }

        // Send Massage method ----------------------------------------------------------
        private void SendMessage(string target, string massage)
        {
            // Massage format : sender|header|data|data|data...
            // Target code : 1.All  2.Server  3.All except Sender   others:Specific player name
            string[] temp = new string[1];
            temp[0] = massage;

            SendMessage(target, temp);
        }
        private void SendMessage(string target, string[] massage)
        {
            // Massage format : sender|header|data|data|data...
            // Target code : 1.All  2.Server  3.All except Sender   others:Specific player name

            string data = myId+myName;
            foreach(string x in massage)
            {
                data += "|" + x;
            }

            // Send massage according to target ---------------------------------------------
            if(myRoom != null)
            {
                if (target == "1")
                {
                    foreach (Player x in myRoom.players)
                    {
                        SendSerializationDataHandler(x, data);
                    }
                }
                else if (target == "3")
                {
                    foreach (Player x in myRoom.players)
                    {
                        if (x.tcp != tcp)
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
        }
        private void SendMessage(string massage)
        {
            string[] msg = new string[] { massage };
            SendMessage(msg);
        }
        private void SendMessage(string[] massage)
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
                        string[] massage = new string[] { "RJnd", myRoom.roomName };
                        SendMessage(massage);

                        // Send massage to other client that we join the room ----------
                        // + Send all player in room name ------------------------------
                        string msg = myRoom.players.Count.ToString();
                        foreach (Player alpha in myRoom.players)
                        {
                            msg += "|" + alpha.myName;
                        }
                        massage = new string[] { "PlCt", msg };
                        SendMessage("1", massage);

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
            SendMessage(massage);

            state = PlayerState.room;

            // Debugging
            Console.WriteLine("Room " + roomName + " created!");
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
                        string[] massage = new string[] { "RJnd", myRoom.roomName };
                        SendMessage(massage);

                        // Send massage to other client that we join the room ----------
                        // + Send all player in room name ------------------------------
                        string msg = myRoom.players.Count.ToString();
                        foreach(Player alpha in myRoom.players)
                        {
                            msg += "|" + alpha.myName;
                        }
                        massage = new string[] { "PlCt", msg };
                        SendMessage("1", massage);

                        state = PlayerState.room;

                        // Debugging
                        Console.WriteLine(myName + " joined room " + roomName);

                        return;
                    }

                    // Send massage that the room is full ------------------------
                    SendMessage("RsF");
                }
            }

            // Send massage to client that no room can be joined -------------------
            SendMessage("RnFd");
        }

        // Exit Room -------------------------------------------------------------
        private void ExitRoom()
        {
            // Check there is other players in room ----------------------------------
            if (myRoom.players.Count > 1)
            {
                // Tell others that we left ------------------------------------------
                SendMessage("3", "LRm");

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
            SendMessage("REx");
        }

        private void ChangeName(string Id, string Name)
        {
            myId = Id;
            myName = Name;
            string[] msg = { "ChNm", Id, Name };
            SendMessage(msg);
        }

        // Sudden disconnect -----------------------------------------------------
        private void SuddenDisconnect()
        {
            isOnline = false;

            // Check client position
            if (state == PlayerState.online)
            {
                // Remove from online list
                server.onlineList.Remove(this);

                // Print Massage
                Console.WriteLine(myId + " " + myName + " Disconnected");
            }
            else if (state == PlayerState.room)
            {
                // Check there is other players in room ----------------------------------
                if (myRoom.players.Count > 1)
                {
                    // Tell others that we left ------------------------------------------
                    SendMessage("3", "LRm");

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
                myRoom = null;

                // Print Massage
                Console.WriteLine(myId + " " + myName + " Disconnected");
            }
        }

        // Set this player to master of room -------------------------------------
        public void SetToMaster()
        {
            isMaster = true;
            // Send massage to client --------------------------------------------
            SendMessage("SeMs");
        }
    }
}
