using Microsoft.Extensions.Options;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpSocketLib
{
    public interface IService
    {
        void Start();
        void Stop();
        IPEndPoint ServerEndPoint { get; }
    }

    public sealed class TcpService : IService
    {       
        private static bool _accept { get; set; }
        private static ConcurrentDictionary<int, Socket> _connections = new ConcurrentDictionary<int, Socket>();
        private CancellationTokenSource _cancellation = new CancellationTokenSource();
        private readonly Socket _listenerSocket;
        private readonly int _backlog;
        private readonly int _bufferSize;
        private readonly IPEndPoint _serverEndPoint;

        public TcpService(IOptions<ServerConfig> serverConfig)
            :this(serverConfig.Value.IpAddress,
                 serverConfig.Value.Port,
                 serverConfig.Value.Backlog,
                 serverConfig.Value.BufferSize)
        {
        }

        public TcpService(string IP,int Port,int Backlog,int BufferSize)
        {
            IPAddress address = IPAddress.Parse(IP);
            _serverEndPoint = new IPEndPoint(address, Port);
            //_listenerTcp = new TcpListener(address, Port);
            _backlog = Backlog;
            _bufferSize = BufferSize;
            _listenerSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _listenerSocket.Bind(_serverEndPoint);
        }

        public IPEndPoint ServerEndPoint => this._serverEndPoint;

        void IService.Start()
        {
            _listenerSocket.Listen(_backlog);
            _accept = true;
            Console.WriteLine($"Server started. Listening at {this._serverEndPoint.Address}:{this._serverEndPoint.Port}");
            Listen().GetAwaiter().GetResult();
        }

        private async Task Listen()
        {
            if (_listenerSocket != null && _accept)
            {
                // Continue listening.               
                while (true)
                {
                    var socket = await _listenerSocket.AcceptAsync();
                    _ = ProcessLinesAsync(socket,_bufferSize);
                }
            }
        }

        private static async Task ProcessLinesAsync(Socket socket,int bufferSize)
        {         
            Console.WriteLine($"[{socket.RemoteEndPoint}]: connected");
            _connections.AddOrUpdate(socket.GetHashCode(), socket, (key, oldValue) => socket);
            Console.WriteLine($"Waiting for client... {_connections.Count} connected at the moment.");

            var pipe = new Pipe();
            Task writing = FillPipeAsync(socket, pipe.Writer, bufferSize);
            Task reading = ReadPipeAsync(socket, pipe.Reader);

            await Task.WhenAll(reading, writing).ConfigureAwait(false);

            Console.WriteLine($"[{socket.RemoteEndPoint}]: disconnected");
        }

        private static async Task FillPipeAsync(Socket socket, PipeWriter writer,
            int minimumBufferSize)
        {           
            while (true)
            {
                try
                {
                    // Request a minimum of 1024 bytes from the PipeWriter
                    Memory<byte> memory = writer.GetMemory(minimumBufferSize);

                    int bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None).ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    // Tell the PipeWriter how much was read
                    writer.Advance(bytesRead);
                }
                catch
                {
                    break;
                }

                // Make the data available to the PipeReader
                FlushResult result = await writer.FlushAsync().ConfigureAwait(false);

                if (result.IsCompleted)
                {
                    break;
                }
            }

            // Signal to the reader that we're done writing
            writer.Complete();
        }

        private static async Task ReadPipeAsync(Socket socket, PipeReader reader)
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync().ConfigureAwait(false);

                ReadOnlySequence<byte> buffer = result.Buffer;
                SequencePosition? position = null;

                do
                {
                    // Find the EOL
                    position = buffer.PositionOf((byte)'\n');

                    if (position != null)
                    {
                        var line = buffer.Slice(0, position.Value);
                        ProcessLine(socket, line);

                        // This is equivalent to position + 1
                        var next = buffer.GetPosition(1, position.Value);

                        // Skip what we've already processed including \n
                        buffer = buffer.Slice(next);
                    }
                }
                while (position != null);

                // We sliced the buffer until no more data could be processed
                // Tell the PipeReader how much we consumed and how much we left to process
                reader.AdvanceTo(buffer.Start, buffer.End);
              
                if (result.IsCompleted)
                {
                    break;
                }
            }

            reader.Complete();
        }

        private static void ProcessLine(Socket socket, in ReadOnlySequence<byte> buffer)
        {
            if (_accept)
            {
                Console.Write($"[{socket.RemoteEndPoint}]: ");
                foreach (var segment in buffer)
                {
#if NETSTANDARD2_0
                    Console.Write(Encoding.UTF8.GetString(segment.Span.ToArray()));
#else
                    Console.Write(Encoding.UTF8.GetString(segment));
#endif
                }
                Console.WriteLine();
            }
        }

        //private async void listenTcp()
        //{
        //    var clientTask = _listenerSocket.AcceptTcpClientAsync(); // Get the client           
        //    if (clientTask.Result != null)
        //    {
        //        var tcpClient = clientTask.Result;
        //        _connections.AddOrUpdate(tcpClient.GetHashCode(), tcpClient,
        //            (key, oldValue) => tcpClient);
        //        Console.WriteLine($"Client {tcpClient.GetHashCode()} connected. Waiting for data.");


        //        var message = string.Empty;

        //        await Task.Run(() => {
        //            while (!message.StartsWith("quit"))
        //            {         
        //               try
        //                {
        //                    //byte[] data = Encoding.UTF8.GetBytes("Send next data: [enter 'quit' to terminate] ");
        //                    //tcpClient.GetStream().Write(data, 0, data.Length);

        //                    byte[] buffer = new byte[1024];
        //                    int bytes = tcpClient.GetStream().Read(buffer, 0, buffer.Length);
        //                    while (bytes > 0)
        //                    {
        //                        message = Encoding.UTF8.GetString(buffer,0, bytes);
        //                        bytes = 0;
        //                        if (!tcpClient.GetStream().DataAvailable)
        //                            break;
        //                    }
        //                    Console.WriteLine(message);
        //                }
        //                catch
        //                { }           
        //            }
        //        }).ConfigureAwait(false);

        //        Console.WriteLine($"Closing {tcpClient.GetHashCode()} connection. {_connections.Count} connected at the moment.");
        //        _connections.TryRemove(tcpClient.GetHashCode(), out tcpClient);
        //        //tcpClient.GetStream().Dispose();
        //        tcpClient.Close();// dont need call both dispose and close. which choose one
        //    }
        //}

        private void ClientDispose()
        {
            foreach (KeyValuePair<int, Socket> connection in _connections)
            {
                _connections.TryRemove(connection.Key, out var socketClient);
                if (socketClient.Connected)
                {
                    socketClient.Shutdown(SocketShutdown.Both);
                    socketClient.Close();
                }
            }
        }

        void IService.Stop()
        {
            ClientDispose();          
            _cancellation.Token.Register(() =>
            {
                _listenerSocket.Shutdown(SocketShutdown.Both);
                _listenerSocket.Close();
                _accept = false;
                Console.WriteLine($"{this._serverEndPoint.Address} Server stooped. unmount port {this._serverEndPoint.Port}");
            });   
        }
    }

#if NET461 || NETSTANDARD2_0
    internal static class Extensions
    {
        public static Task<int> ReceiveAsync(this Socket socket, Memory<byte> memory, SocketFlags socketFlags)
        {
            var arraySegment = GetArray(memory);
            return SocketTaskExtensions.ReceiveAsync(socket, arraySegment, socketFlags);
        }

        public static string GetString(this Encoding encoding, ReadOnlyMemory<byte> memory)
        {
            var arraySegment = GetArray(memory);
            return encoding.GetString(arraySegment.Array, arraySegment.Offset, arraySegment.Count);
        }

        private static ArraySegment<byte> GetArray(Memory<byte> memory)
        {
            return GetArray((ReadOnlyMemory<byte>)memory);
        }

        private static ArraySegment<byte> GetArray(ReadOnlyMemory<byte> memory)
        {
            if (!MemoryMarshal.TryGetArray(memory, out var result))
            {
                throw new InvalidOperationException("Buffer backed by array was expected");
            }

            return result;
        }
    }
#endif
}
