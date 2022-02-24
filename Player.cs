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
        public string name { get; private set; }
        public string id { get; private set; }

        public TcpClient tcp { get; private set; }

        private List<Player> onlineList;
        private List<Room> roomList;

        public NetworkStream stream { get; private set; }
        private Room myRoom;
        private bool isMaster;
        private bool isOnline;

        // Encryption
        private RsaEncryption rsaEncryption;
        public AesEncryption aesEncryption { get; private set; }

        // Private key
        private string ServerPrivateKey;

        public Player(TcpClient tcp, List<Player> onlineList, List<Room> roomList)
        {

        }
    }
}
