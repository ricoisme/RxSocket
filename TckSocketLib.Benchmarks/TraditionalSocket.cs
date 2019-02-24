using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TcpSocketLib;

namespace TckSocketLib.Benchmarks
{
    internal sealed class TraditionalSocket
    {
        private readonly ServerConfig _config;
        private Socket _listenSocket;
        public TraditionalSocket()
        {
            _config = new ServerConfig
            {
                IpAddress = "127.0.0.1",
                Port = 9902,
                Backlog = 120,
                BufferSize = 512,
                Retry = 3
            };
            _listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        }

        public event Action Connected = () => { };

        public event Action Disconnected = () => { };

        public async Task Start()
        {
            _listenSocket.Bind(new IPEndPoint(IPAddress.Parse(_config.IpAddress), _config.Port));
            Console.WriteLine($"Listening on port {_config.Port}");

            _listenSocket.Listen(120);
            Connected();
            while (true)
            {
                var socket = await _listenSocket.AcceptAsync();
                _ = ProcessLinesAsync(socket);
            }
        }

        public void Stop()
        {
            if (_listenSocket != null)
            {
                _listenSocket.Shutdown(SocketShutdown.Both);
                _listenSocket.Close();
                _listenSocket = null;
            }
            Disconnected();
        }

        private async Task ProcessLinesAsync(Socket socket)
        {
            Console.WriteLine($"[{socket.RemoteEndPoint}]: connected");

            using (var stream = new NetworkStream(socket))
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    ProcessLine(socket, await reader.ReadLineAsync());
                }
            }
            Disconnected();
            Console.WriteLine($"[{socket.RemoteEndPoint}]: disconnected");
        }

        private void ProcessLine(Socket socket, string s)
        {

            Console.Write($"[{socket.RemoteEndPoint}]: ");
            Console.WriteLine(s);
        }

    }
}
