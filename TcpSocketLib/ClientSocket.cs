using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TcpSocketLib
{
    public interface IClientSocket
    {
        void Connect(string IP,int Port);
        void Disconnect();
        bool IsConnected { get; }
        Task SendAsync<T>(T message, Action<Record<T>> errorMessageCallback = null);
    }

    public sealed class ClientSocket: IClientSocket
    {
        private Socket _clientSocket;       
        public ClientSocket()
        {          
            _clientSocket = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, 
                ProtocolType.Tcp);
        }

        private Action<Socket> ShowConnected = (clientSocket) => 
        {
            Console.WriteLine($"[{clientSocket.LocalEndPoint}] Connecting to [{clientSocket.RemoteEndPoint}]");
        };

        private Action<EndPoint> ShowDisconnected = (localEndPoint) =>
          {
              Console.WriteLine($"[{localEndPoint}] Disconnected.");
          };

        bool IClientSocket.IsConnected => _clientSocket.Connected;       

        void IClientSocket.Connect(string IP, int Port)
        {
            var ipAddress = IPAddress.Parse(IP);   
            if(_clientSocket==null)
            {
                _clientSocket = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream, 
                    ProtocolType.Tcp);
            }        
            _clientSocket.ConnectAsync(new IPEndPoint(ipAddress, Port))
              .GetAwaiter().GetResult();        
            ShowConnected(_clientSocket);
        }

        void IClientSocket.Disconnect()
        {
            var localEndPoint = _clientSocket.LocalEndPoint;
            _clientSocket.Shutdown(SocketShutdown.Both);      
            _clientSocket.Close();
            _clientSocket = null;
            ShowDisconnected(localEndPoint);
        }

        Task IClientSocket.SendAsync<T>(T message, Action<Record<T>> errorMessageCallback)
        {
            var buffer = Utility.ObjectToByteArray<T>(message);
            return _clientSocket.SendAsync(new ArraySegment<byte>(buffer), SocketFlags.None)
                .ContinueWith(t =>
                {
                    errorMessageCallback?.Invoke(
                      new Record<T>
                      {
                          EndPoint = _clientSocket.RemoteEndPoint,
                          Message = message,
                          Error = $"error:{t.Exception.Message} , {t.Exception.StackTrace}"
                      }
                      );
                }, TaskContinuationOptions.OnlyOnFaulted);
                //.ContinueWith(t=> t.GetAwaiter().GetResult(), TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }
}
