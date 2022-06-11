using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace Survi4s_Server
{
    class Server
    {
        // List --------------------------------------------------------------------------
        public List<Player> onlineList { get; private set; }
        public List<Room> roomList { get; private set; }

        // Variable ----------------------------------------------------------------------
        private int port = 3002;
        private TcpListener serverListener;
        public bool IsOnline { get; private set; }

        // Constructor / Start method ----------------------------------------------------
        public Server()
        {
            // Initialization
            onlineList = new List<Player>();
            roomList = new List<Room>();

            // Try start the server
            try
            {
                serverListener = new TcpListener(IPAddress.Any, port);
                serverListener.Start();
                IsOnline = true;

                Console.WriteLine("------- Server Port " + port + " Created -------\n");
            }
            catch (Exception e)
            {
                Console.WriteLine("Server Start Error : " + e.Message);
            }
        }

        // Starting server ----------------------------------------------------------------
        public void StartListening()
        {
            // Start accepting client
            Console.WriteLine(">> Server : Start Listening");
            Thread beginListenThread = new Thread(BeginAcceptClient);
            beginListenThread.Start();
        }
        // Accepting client thread --------------------------------------------------------
        private void BeginAcceptClient()
        {
            while (IsOnline)
            {
                // Accept Client
                TcpClient client = serverListener.AcceptTcpClient();

                // Make a new class to handle client
                Player player = new Player(client, this);
            }
        }


        // Sudden disconnect -----------------------------------------------------
        public void SuddenDisconnect(Player player)
        {
            player.isOnline = false;

            // Check client position
            if (player.state == Player.PlayerState.online)
            {
                // Remove from online list
                onlineList.Remove(player);

                // Print Massage
                Console.WriteLine(player.myId + " " + player.myName + " Disconnected");
            }
            else if (player.state == Player.PlayerState.room)
            {
                // Check there is other players in room ----------------------------------
                if (player.myRoom.players.Count > 1)
                {
                    // Tell others that we left ------------------------------------------
                    player.SendMessage("3", "LRm");

                    // Check if we are the master of room --------------------------------
                    if (player.isMaster)
                    {
                        // Set other player to master ------------------------------------
                        foreach (Player x in player.myRoom.players)
                        {
                            if (x.tcp != player.tcp)
                            {
                                x.SetToMaster();
                                return;
                            }
                        }
                    }
                }
                else
                {
                    roomList.Remove(player.myRoom);
                }

                player.tcp.Close();

                player.isMaster = false;
                player.myRoom.players.Remove(player);
                player.myRoom = null;

                // Print Massage
                Console.WriteLine(player.myId + " " + player.myName + " Disconnected");
            }
        }
    }
}
