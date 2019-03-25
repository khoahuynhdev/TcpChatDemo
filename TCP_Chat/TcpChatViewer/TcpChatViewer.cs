using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpChatViewer
{
    class TcpChatViewer
    {
        // connection object
        public readonly string ServerAddress;
        public readonly int Port;
        private TcpClient _client;
        public bool Running { get; private set; }
        private bool _disconnectRequested = false;

        // buffer and messaging
        public readonly int BufferSize = 2 * 1024; // 2kb
        private NetworkStream _msgStream = null;

        public TcpChatViewer(string serverAddress, int port = 6000)
        {
            // create a non-connected TcpClient
            _client = new TcpClient
            {
                SendBufferSize = BufferSize,
                ReceiveBufferSize = BufferSize
            }; // other constructors will start a connection

            // set the other things
            ServerAddress = serverAddress;
            Port = port;
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
                byte[] msgBuffer = Encoding.UTF8.GetBytes("viewer");
                _msgStream.Write(msgBuffer, 0, msgBuffer.Length); //blocks

                // If we are still connected after sending name, that means the server accepts us
                if (!_isDisconnected(_client))
                    Running = true;
                else
                {
                    // Server doens't see us as a viewer, cleanup
                    _cleanupNetworkResources();
                    Console.WriteLine("The server didn't recognise us as a Viewer.\n:[");
                }
            }
            else
            {
                _cleanupNetworkResources();
                Console.WriteLine("Wasn't able to connect to the server at {0}.", endPoint);
            }
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
        public void Disconnect()
        {
            Running = false;
            _disconnectRequested = true;
            Console.WriteLine("Disconnecting from the chat...");
        }
        public void ListenForMessages()
        {
            bool wasRunning = Running;

            // Listen for messages
            while (Running)
            {
                // Do we have a new message?
                int messageLength = _client.Available;
                if (messageLength > 0)
                {
                    //Console.WriteLine("New incoming message of {0} bytes", messageLength);

                    // Read the whole message
                    byte[] msgBuffer = new byte[messageLength];
                    _msgStream.Read(msgBuffer, 0, messageLength);   // Blocks

                    // An alternative way of reading
                    //int bytesRead = 0;
                    //while (bytesRead < messageLength)
                    //{
                    //    bytesRead += _msgStream.Read(_msgBuffer,
                    //                                 bytesRead,
                    //                                 _msgBuffer.Length - bytesRead);
                    //    Thread.Sleep(1);    // Use less CPU
                    //}

                    // Decode it and print it
                    string msg = Encoding.UTF8.GetString(msgBuffer);
                    Console.WriteLine(msg);
                }

                // Use less CPU
                Thread.Sleep(10);

                // Check the server didn't disconnect us
                if (_isDisconnected(_client))
                {
                    Running = false;
                    Console.WriteLine("Server has disconnected from us.\n:[");
                }

                // Check that a cancel has been requested by the user
                Running &= !_disconnectRequested;
            }

            // Cleanup
            _cleanupNetworkResources();
            if (wasRunning)
                Console.WriteLine("Disconnected.");
        }

        public static TcpChatViewer viewer;
        static void Main(string[] args)
        {
            Console.WriteLine("===Chat-Board===");
            Console.CancelKeyPress += (sender, e) =>
            {
                viewer.Disconnect();
                e.Cancel = true;
            };
            // setup the messenger
            string host = "localhost";
            int port = 6000;
            viewer = new TcpChatViewer(host, port);

            viewer.Connect();
            viewer.ListenForMessages();
        }
    }
}
