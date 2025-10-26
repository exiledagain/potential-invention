using System.Net;
using System.Net.Sockets;

namespace pi_melon_mod.Server
{
    internal class ByteServer
    {
        public class ServerResponse(TcpClient client)
        {
            public bool Wrote { get; private set; }
            private readonly TcpClient Client = client;

            public void Write(byte[] data)
            {
                if (Wrote)
                {
                    throw new Exception("Already wrote response");
                }
                Wrote = true;
                var stream = new BinaryWriter(Client.GetStream());
                stream.Write(data.Length);
                stream.Write(data);
            }
        }

        public int MaxMessageSize = 1024 * 1024;
        public delegate void RemoteRequest(byte[] data, ServerResponse response);
        public event RemoteRequest OnRemoteRequest;

        private readonly int DesiredPort;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
        public ByteServer(int desiredPort)
        {
            DesiredPort = desiredPort;
        }

        public async void Start()
        {
            var listener = new TcpListener(IPAddress.Loopback, DesiredPort);
            listener.Start();
            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                _ = Task.Run(() =>
                {
                    RequestLoop(client);
                });
            }
        }

        void RequestLoop(TcpClient client)
        {
            client.NoDelay = true;
            byte[] sizeBuf = new byte[4];
            using var stream = new BinaryReader(client.GetStream());
            while (true)
            {
                // must be little endian
                int messageSize = stream.ReadInt32();
                if (messageSize > MaxMessageSize)
                {
                    throw new Exception("Request from client too big size=" + messageSize);
                }
                var response = new ServerResponse(client);
                OnRemoteRequest(stream.ReadBytes(messageSize), response);
                if (!response.Wrote)
                {
                    response.Write([]);
                }
            }
        }
    }
}
