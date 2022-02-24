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
        private List<Player> onlineList;
        private List<Room> roomList;

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
                Player player = new Player(client, onlineList, roomList);
            }
        }


    }
}
