using System;

namespace Survi4s_Server
{
    class Program
    {
        static void Main(string[] args)
        {
            Server server = new Server();
            server.StartListening();
        }
    }
}
