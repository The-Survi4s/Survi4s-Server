using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Linq;

namespace Survi4s_Server
{
    class Player
    {
        public string myName { get; private set; }
        public string myId { get; private set; }

        public TcpClient tcp { get; private set; }

        private Server server;

        public NetworkStream networkStream { get; private set; }
        public Room myRoom;
        public bool isMaster;
        public bool isOnline;

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
            string data = rsaEncryption.Decrypt(answer, rsaEncryption.serverPrivateKey);

            // If failed, close client connection ----------------------------------
            if(data.Length == 0)
            {
                tcp.Close();
                return;
            }

            // If success, save the id and name -------------------------------------
            myId = data[..6];
            myName = data[6..];
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
            // Thread for receiving message ----------------------------------------------
            Thread recieveThread = new(ReceivedMessage);

            // Check client online status ------------------------------------------------
            Thread onlineStatus = new(CheckOnlineStatus);

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
                    server.SuddenDisconnect(this);
                }
                else
                {
                    checkTime = 0;
                }
            }
        }

        // Receive and proccess client message here --------------------------------------
        private void ReceivedMessage()
        {
            // Message format : target|header|data|data|data...
            // Target code : 1.All  2.Server  3.All except Sender  others:Specific player name
            // Message code in : https://docs.google.com/spreadsheets/d/1vVT-tvdHMXsiBQaf16NSQZUbZoGLrR3Ub53RYXnhZtw/edit#gid=0

            while (isOnline)
            {
                if (networkStream.DataAvailable)
                {
                    // Receive and split message here ------------------------------------
                    BinaryFormatter formatter = new BinaryFormatter();
                    string data = formatter.Deserialize(networkStream) as string;
                    string[] info = data.Split("|");
                    Console.WriteLine("Receive:" + myName + "|" + data.Trim('\0'));
                    if (info[1] == "A")
                    {
                        checkTime = DefaultCheckTime;
                        continue;
                    }
                    switch (EnumParse<Recipient>(info[0]))
                    {
                        case Recipient.All:
                            // Send message to all client in room ----------------------------
                            SendMessage(Recipient.All, data[2..]);
                            break;
                        case Recipient.Server:
                            switch (EnumParse<Subject>(info[1]))
                            {
                                case Subject.StMtc:
                                    MatchMaking();
                                    break;
                                case Subject.CrR:
                                    CreateRoom(info[2], int.Parse(info[3]), bool.Parse(info[4]));
                                    break;
                                case Subject.JnR:
                                    JoinRoom(info[2]);
                                    break;
                                case Subject.ExR:
                                    ExitRoom();
                                    break;
                                case Subject.LcR:
                                    myRoom.LockRoom();
                                    break;
                                case Subject.ChNm:
                                    ChangeName(info[2], info[3]);
                                    break;
                            }
                            break;
                        case Recipient.AllExceptSender:
                            {
                                SendMessage(Recipient.AllExceptSender, data[2..]);
                                break;
                            }
                        default:
                            {
                                if (myRoom == null) return;
                                foreach (Player x in myRoom.players.Where(x => x.myName == info[0]))
                                {
                                    x.SendMessage(info);
                                }
                                break;
                            }
                    }
                    checkTime = DefaultCheckTime;
                }
            }
        }

        // Send Message method ----------------------------------------------------------
        /// <summary>
        /// Sends message from server to this player
        /// </summary>
        /// <param name="message"></param>
        private void SendMessage(params string[] message)
        {
            // Message format : sender|header|data|data|data...

            string data = Subject.Svr.ToString();
            foreach (string x in message)
            {
                data += "|" + x;
            }
            
            SendSerializationDataHandler(this, data);
        }

        /// <summary>
        /// Pass message from this player to other recipients
        /// </summary>
        /// <param name="target"></param>
        /// <param name="message"></param>
        private void SendMessage(string target, params string[] message)
        {
            // Message format : sender|header|data|data|data...
            // Target code : 1.All  2.Server  3.All except Sender   others:Specific player name

            string data = myId+myName;
            foreach(string x in message)
            {
                data += "|" + x;
            }

            // Send message according to target ---------------------------------------------
            if(myRoom != null)
            {
                if (target == Num(Recipient.All))
                {
                    foreach (Player x in myRoom.players)
                    {
                        SendSerializationDataHandler(x, data);
                    }
                }
                else if (target == Num(Recipient.AllExceptSender))
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

        /// <summary>
        /// Pass message from this player to other recipients
        /// </summary>
        /// <param name="receiver"></param>
        /// <param name="message"></param>
        public void SendMessage(Recipient receiver, params string[] message)
        {
            SendMessage(Num(receiver), message);
        }

        /// <summary>
        /// Pass message from this player to other recipients
        /// </summary>
        /// <param name="receiver"></param>
        /// <param name="subject"></param>
        /// <param name="body"></param>
        public void SendMessage(Recipient receiver, Subject subject, params string[] body)
        {
            List<string> msg = new() { subject.ToString() };
            msg.AddRange(body);
            SendMessage(Num(receiver), msg.ToArray());
        }

        /// <summary>
        /// Pass message from this player to other recipients
        /// </summary>
        /// <param name="receiver"></param>
        /// <param name="subject"></param>
        public void SendMessage(Recipient receiver, Subject subject)
        {
            SendMessage(Num(receiver), subject.ToString());
        }

        /// <summary>
        /// Sends message from server to this player
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="body"></param>
        private void SendMessage(Subject subject, params string[] body)
        {
            List<string> msg = new() { subject.ToString() };
            msg.AddRange(body);
            SendMessage(msg.ToArray());
        }

        /// <summary>
        /// Sends message from server to this player
        /// </summary>
        /// <param name="subject"></param>
        private void SendMessage(Subject subject)
        {
            SendMessage(new string[] { subject.ToString() });
        }

        private void SendSerializationDataHandler(Player player, string data)
        {
            // Try to send, if fails, let's just assume player disconnected ------------------
            try
            {
                Console.WriteLine("Send:" + data);
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(player.networkStream, data);
            }
            catch (Exception e)
            {
                Console.WriteLine("Send message error from " + player.myId + " " + player.myName + " : " + e.Message);
                // Disconnect client from server
                server.SuddenDisconnect(player);
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

                        // Send message to client that we got the room ------------
                        SendMessage(Subject.RJnd, myRoom.roomName, myRoom.players.Count.ToString());

                        // Send message to other client that we join the room ----------
                        // + Send all player in room name ------------------------------
                        string msg = myRoom.players.Count.ToString();
                        foreach (Player alpha in myRoom.players)
                        {
                            msg += "|" + alpha.myName;
                        }
                        SendMessage(Recipient.All, Subject.PlCt, msg);

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
            Room temp = new(roomName, maxPlayer, isPublic);
            server.roomList.Add(temp);
            myRoom = temp;

            // Remove player from online list --------------------------------------
            server.onlineList.Remove(this);
            myRoom.players.Add(this);

            // Room creator is master of the room ----------------------------------
            isMaster = true;

            // Send message to client that we create the room ----------------------
            SendMessage(Subject.RCrd, myRoom.roomName);

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

                        // Send message to client that we have joined to room ------
                        SendMessage(Subject.RJnd, myRoom.roomName, myRoom.players.Count.ToString());

                        // Send message to other client that we join the room ----------
                        // + Send all player in room name ------------------------------
                        string msg = myRoom.players.Count.ToString();
                        foreach(Player alpha in myRoom.players)
                        {
                            msg += "|" + alpha.myName;
                        }
                        SendMessage(Recipient.All, Subject.PlCt, msg);

                        state = PlayerState.room;

                        // Debugging
                        Console.WriteLine(myName + " joined room " + roomName);

                        return;
                    }

                    // Send message that the room is full ------------------------
                    SendMessage(Subject.RsF);
                }
            }

            // Send message to client that no room can be joined -------------------
            SendMessage(Subject.RnFd);
        }

        // Exit Room -------------------------------------------------------------
        private void ExitRoom()
        {
            // Check there is other players in room ----------------------------------
            if (myRoom.players.Count > 1)
            {
                // Tell others that we left ------------------------------------------
                SendMessage(Recipient.AllExceptSender, Subject.LRm);

                // Check if we are the master of room --------------------------------
                if (isMaster)
                {
                    // Set other player to master ------------------------------------
                    foreach (Player x in myRoom.players)
                    {
                        if (x.tcp != tcp)
                        {
                            x.SetToMaster();
                            break;
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

            // Send message to client --------------------------------------------
            SendMessage(Subject.REx);
        }

        private void ChangeName(string Id, string Name)
        {
            myId = Id;
            myName = Name;
            SendMessage(Subject.ChNm, Id, Name);
        }

        // Set this player to master of room -------------------------------------
        public void SetToMaster()
        {
            isMaster = true;
            // Send message to client --------------------------------------------
            SendMessage(Subject.SeMs);
        }

        /// <summary>
        /// Parses a string to an enum of type <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T">The enum type</typeparam>
        /// <param name="stringToEnum">the string to parse</param>
        /// <returns>The enum <typeparamref name="T"/></returns>
        public static T EnumParse<T>(string stringToEnum)
        {
            return (T)Enum.Parse(typeof(T), stringToEnum, true);
        }

        public static string Num(Recipient recipient) => ((int)recipient).ToString();
    }
}
