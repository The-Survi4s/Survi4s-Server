using System;
using System.Collections.Generic;
using System.Text;

namespace Survi4s_Server
{
    class Room
    {
        // Variable --------------------------------------------------------------
        public string roomName { get; private set; }
        public int maxPlayer { get; private set; }
        public bool isPublic { get; private set; }
        public List<Player> players { get; private set; }

        // Constructor -----------------------------------------------------------
        public Room(string roomName, int maxPlayer, bool isPublic)
        {
            this.roomName = roomName;
            this.maxPlayer = maxPlayer;
            this.isPublic = isPublic;
            players = new List<Player>();
        }

        // Ask if room is avaliable to join --------------------------------------
        public bool CanJoinPublic()
        {
            if(maxPlayer < players.Count && isPublic)
            {
                return true;
            }

            return false;
        }
        public bool CanJoinPrivate()
        {
            if (maxPlayer < players.Count)
            {
                return true;
            }

            return false;
        }
    }
}