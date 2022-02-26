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

        // Encryption
        private RsaEncryption rsaEncryption;

        // Private key
        private string PrivateKeyFile = "Private-Key.txt";

        public Player(TcpClient tcp, Server server)
        {
            this.tcp = tcp;
            this.server = server;

            networkStream = tcp.GetStream();
            isOnline = true;

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
                Server.CloseConnection(this);
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
            while (isOnline)
            {
                if (networkStream.DataAvailable)
                {
                    // Check massage here
                    BinaryFormatter formatter = new BinaryFormatter();

                }
            }
        }

        private void SendMassage()
        {

        }
    }
}
