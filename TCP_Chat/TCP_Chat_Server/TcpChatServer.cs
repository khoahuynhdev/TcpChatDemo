using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TCP_Chat_Server
{
    class TcpChatServer
    {
        // what listens in
        private TcpListener _listener;
        
        // types of clients connected
        private List<TcpClient> _viewers = new List<TcpClient>();
        private List<TcpClient> _messengers = new List<TcpClient>();
        private List<TcpClient> _Members = new List<TcpClient>();

        // names that are taken by other messengers
        private Dictionary<TcpClient, string> _names = new Dictionary<TcpClient, string>();
        // Messages needs to be sent
        private Queue<string> _messageQueue = new Queue<string>();

        // Extra data
        public readonly string ChatName;
        public readonly int Port;
        public bool Running { get; private set; }

        // buffer
        public readonly int BufferSize = 2 * 1024; // 2kb

        public static TcpChatServer chat;
        public TcpChatServer(string chatName, int port)
        {
            // set data
            ChatName = chatName;
            Port = port;
            Running = false;

            // make the listener listens for connections on network device
            _listener = new TcpListener(IPAddress.Any, Port);
        }

        public void ShutDown()
        {
            Running = false;
            Console.WriteLine("Shutting down the server.");
        }

        // Start running the server until ShutDown is called
        public void Run()
        {
            Console.WriteLine("Starting the \"{0}\" TCP chat server on port {1}", ChatName, Port);
            Console.WriteLine("Press Ctrl-C to shutdown the server at anytime");

            // Make the server run
            _listener.Start(); //
            Running = true;
            while (Running)
            {
                // check for new client
                if (_listener.Pending()) _handleNewConnection();

                // Do the rest
                _checkForDisconnects();
                _checkForNewMessages();
                _sendMessages();

                // use less CPU
                Thread.Sleep(10);
            }

            // Stop the server, and clean up any connection clients
            foreach (TcpClient v in _viewers)
                _cleanupClient(v);
            foreach (TcpClient m in _messengers)
                _cleanupClient(m);
            _listener.Stop();

            Console.WriteLine("Server is shutdown");
        }

        private static void _cleanupClient(TcpClient v)
        {
            v.GetStream().Close(); // close the network stream
            v.Close(); // close the connection
        }

        /// <summary>
        /// Clear out the message queue and send to the viewers
        /// </summary>
        private void _sendMessages()
        {
            foreach (string msg in _messageQueue)
            {
                // encode the message
                byte[] msgBuffer = Encoding.UTF8.GetBytes(msg);

                // send msg to each viewer
                foreach (TcpClient v in _viewers)
                    v.GetStream().Write(msgBuffer, 0, msgBuffer.Length); //blocks

            }
            // clear out the queue
            _messageQueue.Clear();
        }

        /// <summary>
        /// see if any of messengers have a new message and put it in the queue
        /// </summary>
        private void _checkForNewMessages()
        {
            foreach (TcpClient m in _messengers)
            {
                int messageLength = m.Available; // get the amount of data that has been received from the network
                // and is avaiable to read.
                if (messageLength > 0)
                {
                    // there is one, get it
                    byte[] msgBuffer = new byte[messageLength];
                    m.GetStream().Read(msgBuffer, 0, messageLength); // blocks

                    // attach a name to it and shove it into the queue
                    string msg = $"{_names[m]}: {Encoding.UTF8.GetString(msgBuffer)}";
                    Console.WriteLine($"Incoming message: {msg}");
                    _messageQueue.Enqueue(msg);
                }

            }
        }

        // Sees if any of the clients have left the chat server
        private void _checkForDisconnects()
        {
            // Check the viewers first
            foreach (TcpClient v in _viewers.ToArray())
            {
                if (_isDisconnected(v))
                {
                    Console.WriteLine($"Viewer {v.Client.RemoteEndPoint} has left.");

                    // clean up 
                    _viewers.Remove(v);
                    _cleanupClient(v);
                }
            }
            // check for the messengers
            foreach (TcpClient m in _messengers.ToArray())
            {
                if (_isDisconnected(m))
                {
                    // get info about messenger
                    string name = _names[m];

                    // tell the viewers that a messenger has left
                    Console.WriteLine($"Messenger {name} has left.");
                    _messageQueue.Enqueue($"{name} has left the chat.");

                    // cleanup on our end
                    _messengers.Remove(m); // remove from list
                    _names.Remove(m); // remove from name
                    _cleanupClient(m);

                }
            }
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

        private async void _handleNewConnection()
        {
            // there is atleast one, see what the want
            bool good = false;
            TcpClient newClient = await _listener.AcceptTcpClientAsync(); // blocks
            NetworkStream netStream = newClient.GetStream();

            // modify the default buffer sizes
            newClient.SendBufferSize = BufferSize;
            newClient.ReceiveBufferSize = BufferSize;

            // Print some info
            EndPoint endPoint = newClient.Client.RemoteEndPoint;
            Console.WriteLine($"Handling a new client from {endPoint}...");

            // let them identifies themselves
            byte[] msgBuffer = new byte[BufferSize];
            int bytesRead = await netStream.ReadAsync(msgBuffer, 0, msgBuffer.Length); // blocks

            if (bytesRead > 0)
            {
                string msg = Encoding.UTF8.GetString(msgBuffer, 0, bytesRead);

                if (msg == "viewer")
                {
                    // they just want to watch
                    good = true;
                    _viewers.Add(newClient);

                    Console.WriteLine($"{endPoint} is a viewer.");

                    // send them a "hello messenger"
                    msg = String.Format($"Welcome to the \"{ChatName}\" chat Server!");
                    msgBuffer = Encoding.UTF8.GetBytes(msg);
                    await netStream.WriteAsync(msgBuffer, 0, msgBuffer.Length);
                }
                else if (msg.StartsWith("name:"))
                {
                    // So they might be a messenger
                    string name = msg.Substring(msg.IndexOf(':') + 1);
                    if (!String.IsNullOrEmpty(name) && !_names.ContainsValue(name))
                    {
                        // they are new here, add them in
                        good = true;
                        _names.Add(newClient, name);
                        _messengers.Add(newClient);

                        Console.WriteLine($"{endPoint} is a messenger with the name {name}");

                        // tell the viewer we have a new messenger
                        _messageQueue.Enqueue(string.Format($"{name} has joined the chat"));

                    }
                }
                else
                {
                    // Wasn't a viewer or messenger, cleanup anyway
                    Console.WriteLine($"Wasn't able to identify {endPoint} as a viewer or messenger");
                    _cleanupClient(newClient);
                }
            }

            // do we really want them?
            if (!good)
                newClient.Close();
        }
      
        static void Main(string[] args)
        {
            // create the server
            string name = "Test Server";
            int port = 6000;
            chat = new TcpChatServer(name, port);

            Console.CancelKeyPress += (sender, e) =>
            {
                chat.ShutDown();
                e.Cancel = true;
            };

            chat.Run();
        }
    }
}
