using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpChatMessenger
{
    public class TcpChatMessenger
    {
        // connection object
        public readonly string ServerAddress;
        public readonly int Port;
        private TcpClient _client;
        public bool Running { get; private set; }

        // buffer and messaging
        public readonly int BufferSize = 2 * 1024; // 2kb
        private NetworkStream _msgStream = null;

        // personal data
        public readonly string Name;

        public TcpChatMessenger(string serverAddress, int port=6000, string name="default")
        {
            // create a non-connected TcpClient
            _client = new TcpClient(); // other constructors will start a connection
            _client.SendBufferSize = BufferSize;
            _client.ReceiveBufferSize = BufferSize;

            // set the other things
            ServerAddress = serverAddress;
            Port = port;
            Name = name;
        }
        public void Connect()
        {
            // try to connects
            _client.Connect(ServerAddress, Port); // will resolve DNS for us, blocks
            EndPoint endPoint = _client.Client.RemoteEndPoint;

            // make sure we are connected
            if (_client.Connected)
            {
                // got in!
                Console.WriteLine($"Connect to the server at {endPoint}.");

                // tell them we are a messenger
                _msgStream = _client.GetStream();
                byte[] msgBuffer = Encoding.UTF8.GetBytes($"name:{Name}");
                _msgStream.Write(msgBuffer, 0, msgBuffer.Length); //blocks

                // If we are still connected after sending name, that means the server accepts us
                if (!_isDisconnected(_client))
                    Running = true;
                else
                {
                    // Name was probably taken
                    _cleanupNetworkResources();
                    Console.WriteLine($"The server rejected us; \"{Name}\" is probably in use");
                }
            }
            else
            {
                _cleanupNetworkResources();
                Console.WriteLine("Wasn't able to connect to the server at {0}.", endPoint);
            }
        }
        public void SendMessage()
        {
            bool wasRunning = Running;
            while (Running)
            {
                // poll for user input
                Console.Write($"{Name}> ");
                string msg = Console.ReadLine();

                // quit or send message
                if (msg.ToLower() == "quit" || msg.ToLower() == "exit")
                {
                    // user want to quit
                    Console.WriteLine("Disconnecting...");
                    Running = false;
                }
                else if (msg != string.Empty)
                {
                    byte[] msgBuffer = Encoding.UTF8.GetBytes(msg);
                    _msgStream.Write(msgBuffer, 0, msgBuffer.Length); // blocks
                }

                // use less CPU
                Thread.Sleep(10);

                // check the server didn't disconnect us
                if (_isDisconnected(_client))
                {
                    Running = false;
                    Console.WriteLine("Server has disconnected from us. \n:[");
                }
            }

            _cleanupNetworkResources();
            if (wasRunning)
                Console.WriteLine("Disconnected");
        }

        private void _cleanupNetworkResources()
        {
            _msgStream?.Close();
            _msgStream = null;
            _client.Close();
        }

        private bool _isDisconnected(TcpClient v)
        {
            try
            {
                Socket s = v.Client;
                return s.Poll(10 * 1000, SelectMode.SelectRead) && (s.Available == 0);
            }
            catch (SocketException se)
            {

                // we got a socket error, assume it's disconnected
                return true;
            }
        }

        static void Main(string[] args)
        {
            // get a name
            Console.Write("Enter a name to use: ");
            string name = Console.ReadLine();

            // setup the messenger
            Console.Write("input ip address: ");
            string host = Console.ReadLine();
            int port = 6000;
            TcpChatMessenger messenger = new TcpChatMessenger(host, port, name: name);

            messenger.Connect();
            messenger.SendMessage();
        }
    }
}
