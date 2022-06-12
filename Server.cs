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
                Player player = new(client, this);
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
                    player.SendMessage(Recipient.AllExceptSender, Subject.LRm);

                    // Check if we are the master of room --------------------------------
                    if (player.isMaster)
                    {
                        // Set other player to master ------------------------------------
                        foreach (Player x in player.myRoom.players)
                        {
                            if (x.tcp != player.tcp)
                            {
                                x.SetToMaster();
                                break;
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

    #region Type declarations
    /// <summary>
    /// Message subject
    /// </summary>
    public enum Subject
    {
        /// <summary>Server</summary>
        Svr,
        /// <summary>Room created</summary>
        RCrd,
        /// <summary>Room joined</summary>
        RJnd,
        /// <summary>Room not found</summary>
        RnFd,
        /// <summary>Room is full</summary>
        RsF,
        /// <summary>Exit room</summary>
        REx,
        /// <summary>Sync mouse position</summary>
        MPos,
        /// <summary>Player count</summary>
        PlCt,
        /// <summary>Start game</summary>
        StGm,
        /// <summary>Spawn <see cref="Player"/></summary>
        SpwP,
        /// <summary>Equip <see cref="WeaponBase"/></summary>
        EqWp,
        /// <summary><see cref="Player"/> attack</summary>
        PAtk,
        /// <summary>Spawn <see cref="BulletBase"/></summary>
        SpwB,
        /// <summary>Modify <see cref="Monster"/> hp</summary>
        MdMo,
        /// <summary>Modify <see cref="Player"/> hp</summary>
        MdPl,
        /// <summary>Correct <see cref="Player"/> dead position</summary>
        PlDd,
        /// <summary>Modify <see cref="Wall"/> hp</summary>
        MdWl,
        /// <summary>Spawn <see cref="Monster"/></summary>
        SpwM,
        /// <summary>Add <see cref="StatusEffectBase"/> to a <see cref="Monster"/></summary>
        MoEf,
        /// <summary><see cref="Monster"/> attack</summary>
        MAtk,
        /// <summary>Modify <see cref="Statue"/> hp</summary>
        MdSt,
        /// <summary><see cref="Player"/> leave room / disconnect</summary>
        LRm,
        /// <summary>Destroy <see cref="BulletBase"/></summary>
        DBl,
        /// <summary>Rebuilt <see cref="Wall"/></summary>
        RbWl,
        /// <summary>Upgrade <see cref="WeaponBase"/></summary>
        UpWpn,
        /// <summary>Sync <see cref="Player"/> velocity</summary>
        PlVl,
        /// <summary><see cref="Player"/> jump</summary>
        PJmp,
        /// <summary>Change name</summary>
        ChNm,
        /// <summary>Game over</summary>
        GmOv,
        /// <summary>Sync <see cref="Player"/> position</summary>
        PlPos,
        /// <summary>Start Matchmaking</summary>
        StMtc,
        /// <summary>Request create room</summary>
        CrR,
        /// <summary>Request join room</summary>
        JnR,
        /// <summary>Request exit room</summary>
        ExR,
        /// <summary>Request lock room</summary>
        LcR,
        /// <summary>Set to Master</summary>
        SeMs
    }

    public enum Recipient { None, All, Server, AllExceptSender, SpecificPlayer }
    #endregion
}
