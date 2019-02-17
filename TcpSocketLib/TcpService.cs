using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpSocketLib
{
    public interface IService
    {
        event Action Connected;
        event Action Disconnected;
        IObservable<Record<object>> Reciever { get;}
        IObservable<Record<object>> Sender{ get; }
        void Start();
        void Stop();
        Task SendAsync<T>(T message, Action<Record<T>> errorMessageCallback = null);
        IPEndPoint ServerEndPoint { get; }
    }

    public sealed class TcpService : IService
    {
        private static bool _accept { get; set; }
        private static ConcurrentDictionary<int, Socket> _connections = new ConcurrentDictionary<int, Socket>();
        private CancellationTokenSource _cancellation = new CancellationTokenSource();
        private ISubject<Record<object>> _sender = new Subject<Record<object>>();
        private readonly Socket _listenerSocket;
        private readonly int _backlog;
        private readonly int _bufferSize;      
        private readonly int _retryMax;
        private readonly IPEndPoint _serverEndPoint;

        public TcpService(IOptions<ServerConfig> serverConfig)
            : this(serverConfig.Value.IpAddress,
                 serverConfig.Value.Port,
                 serverConfig.Value.Backlog,
                 serverConfig.Value.BufferSize)
        {
            _retryMax = serverConfig.Value.Retry;
        }

        public TcpService(ServerConfig serverConfig)
            :this(serverConfig.IpAddress,
                 serverConfig.Port,
                 serverConfig.Backlog,
                 serverConfig.BufferSize)
        {
            _retryMax = serverConfig.Retry;
        }

        public TcpService(string IP, int port, int backlog, int bufferSize)
        {
            var address = IPAddress.Parse(IP);
            _serverEndPoint = new IPEndPoint(address, port);          
            _backlog = backlog;
            _bufferSize = bufferSize;            
            _listenerSocket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream, 
                ProtocolType.Tcp);
            try
            {
                _listenerSocket.Bind(_serverEndPoint);
            }
            catch
            {
               var result= Utility.Retry(_retryMax,
                    ()=>Task.Factory.StartNew(() => _listenerSocket.Bind(_serverEndPoint))
                );
                if(!result)
                {
                    throw;
                }
            }           
        }        

        private static Action ShowTotalConnections = () =>
        {
            Console.WriteLine($"Waiting for client... {_connections.Count} connected at the moment.");
        };

        private Action<EndPoint> SocketDisposed = (ep) =>
        {             
              Console.WriteLine($"[{ep}]: client socket disposed.");
        };

        public event Action Connected = () => { };
        public event Action Disconnected = () => { };

        private static event Action<Record<object>> MessageReceived = _ => { };

        public IPEndPoint ServerEndPoint => this._serverEndPoint;

        public IObservable<Record<object>> Reciever => Observable.FromEvent<Record<object>>
            (a => MessageReceived += a,
            a => MessageReceived -= a);

        public IObservable<Record<object>> Sender => this._sender;
       
        void IService.Start()
        {
            _listenerSocket.Listen(_backlog);
            _accept = true;         
            Connected();
#if DEBUG
            (this as IService).SendAsync("Debug Mode: send message");
            (this as IService).SendAsync("Debug Mode: send message again");
#endif
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
            ShowTotalConnections();

            var pipe = new Pipe();
            var writing = FillPipeAsync(socket, pipe.Writer, bufferSize);
            var reading = ReadPipeAsync(socket, pipe.Reader);

            await Task.WhenAll(reading, writing).ConfigureAwait(false);
          
            Console.WriteLine($"[{socket.RemoteEndPoint}]: disconnected");
            _connections.TryRemove(socket.GetHashCode(), out var socketClient);
            ShowTotalConnections();
        }

        private static async Task FillPipeAsync(Socket socket, PipeWriter writer,
            int minimumBufferSize)
        {           
            while (true)
            {
                try
                {
                    // Request a minimum of 1024 bytes from the PipeWriter
                    var memory = writer.GetMemory(minimumBufferSize);

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
                var result = await writer.FlushAsync().ConfigureAwait(false);

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
                var result = await reader.ReadAsync().ConfigureAwait(false);

                var buffer = result.Buffer;
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
                var message = new StringBuilder();
                foreach (var segment in buffer)
                {
#if NETSTANDARD2_0
                    message.Append(Encoding.UTF8.GetString(segment.Span.ToArray())); 
#else
                    message.Append(Encoding.UTF8.GetString(segment));
#endif
                }
                MessageReceived(new Record<object> {
                    EndPoint = socket.RemoteEndPoint,
                    Message = message.ToString().Trim('"'),//due to JsonConvert.SerializeObject
                    Error = string.Empty });
            }
        }

        #region legacy code

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

        #endregion

        private void ClientDispose()
        {
            foreach (var connection in _connections)
            {
                _connections.TryRemove(connection.Key, out var socketClient);
                if (socketClient.Connected)
                {
                    var endPoint = socketClient.RemoteEndPoint;                 
                    socketClient.Shutdown(SocketShutdown.Both);
                    socketClient.Close();
                    SocketDisposed(endPoint);
                }
            }
        }

        void IService.Stop()
        {
            ClientDispose();
            if (_listenerSocket.Connected)
            {
                _listenerSocket.Shutdown(SocketShutdown.Both);
                _listenerSocket.Close();
                _accept = false;
            }
            Disconnected();
            //_cancellation.Token.Register(() =>
            //{
            //    _listenerSocket.Shutdown(SocketShutdown.Both);
            //    _listenerSocket.Close();
            //    _accept = false;            
            //    //Console.WriteLine($"{this._serverEndPoint.Address} Server stooped. unmount port {this._serverEndPoint.Port}");
            //});   
        }

        Task IService.SendAsync<T>(T message, Action<Record<T>> errorMessageCallback)
        {
            var buffer = Utility.ObjectToByteArray(message);
            var localEndPoint = _listenerSocket.LocalEndPoint;
            bool ReConnect()
            {
                return Utility.Retry(_retryMax,
                    () => _listenerSocket.ConnectAsync(localEndPoint));
            }

            bool ReSend()
            {
                return Utility.Retry(_retryMax, () => _listenerSocket.SendAsync(new ArraySegment<byte>(buffer), SocketFlags.None));
            }

            void callbackInvoke(string errorMessage)
            {
                errorMessageCallback?.Invoke(
                     new Record<T>
                     {
                         EndPoint = localEndPoint,
                         Message = message,
                         Error = $"error:{errorMessage}"
                     });
            }

            if (!Utility.IsConnect(_listenerSocket))
            {               
                var result = ReConnect();
                var errorMessage = $"[{localEndPoint}] Disconnected.";
                if (!result)
                {                
                    callbackInvoke(errorMessage);
                    Disconnected();
                }
                return Observable.Start(() => Task.FromResult(result))
                    .Do(x => this._sender.OnNext
                    (
                     new Record<object>
                     {
                         Message = message,
                         EndPoint = localEndPoint,
                         Error = errorMessage
                     }
                    ),
                    ex => { callbackInvoke($"{ex.Message}, {ex.StackTrace}"); }                   
                ).ToTask(_cancellation.Token);
            }

            return Observable.Start(() =>
                _listenerSocket.SendAsync(new ArraySegment<byte>(buffer), SocketFlags.None)
            ) //.SelectMany(_ => message)
             .Do(x => this._sender.OnNext
             (
                 new Record<object>
                 {
                     Message = message,
                     EndPoint = localEndPoint,
                     Error = "" }
                 ),
                 ex =>
                 {
                     var result = ReSend();
                     if (!result)
                     {
                         var errorMessage = $"{ex.Message}, {ex.StackTrace}";
                         callbackInvoke(errorMessage);
                     }
                 }               
              ).ToTask(_cancellation.Token);
        }
    }

    public static class Utility
    {
        internal static byte[] Convert(string message)
        {
            return Encoding.UTF8.GetBytes(message + '\n');//need \n to be endOfLine
            //var header = BitConverter.GetBytes(body.Length);
            //return header.Concat(body).ToArray();
        }

        internal static byte[] ObjectToByteArray<T>(T obj)
        {
            if (obj == null)
            {
                return null;
            }              
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj) + '\n');
        }

        public static bool IsConnect(Socket client)
        {
            var pollResult = client.Poll(250, SelectMode.SelectRead);
            var availableResult = (client.Available == 0);
            if (pollResult && availableResult)
            {
                return false;
            }              
            else
            {
                return true;
            }
        }

        public static bool Retry(int maxLoop, Func<Task> action)
        {          
            for(int i=0;i< maxLoop;i++)
            {
                Thread.Sleep(100);
                try
                {
                   action?.Invoke().Wait();                    
                   return true;
                }
                catch
                {                   
                    if(i<maxLoop)
                    {
                        continue;
                    }
                    break;                 
                }               
            }
            return false;
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
