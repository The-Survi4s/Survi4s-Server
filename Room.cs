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
        private bool isLocked;
        public List<Player> players { get; private set; }

        // Constructor -----------------------------------------------------------
        public Room(string roomName, int maxPlayer, bool isPublic)
        {
            this.roomName = roomName;
            this.maxPlayer = maxPlayer;
            this.isPublic = isPublic;
            isLocked = false;
            players = new List<Player>();
        }

        // Ask if room is avaliable to join --------------------------------------
        public bool CanJoinPublic()
        {
            if(players.Count < maxPlayer && isPublic && !isLocked)
            {
                return true;
            }

            return false;
        }
        public bool CanJoinPrivate()
        {
            if (players.Count < maxPlayer && !isLocked)
            {
                return true;
            }

            return false;
        }

        // Lock the room so others can't join when game is started ---------------
        public void LockRoom()
        {
            isLocked = true;
        }
    }
}