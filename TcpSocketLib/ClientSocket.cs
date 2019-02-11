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
        Task SendAsync(string message,Action<Record> errorMessageCallback=null);
    }

    public sealed class ClientSocket: IClientSocket
    {
        private Socket _clientSocket;
        public ClientSocket()
        {
            _clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
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
                _clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
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

        async Task IClientSocket.SendAsync(string message, Action<Record> errorMessageCallback=null)
        {
            try
            {
                var buffer = Convert(message);
                await _clientSocket.SendAsync(new ArraySegment<byte>(buffer), SocketFlags.None);
            }
            catch(Exception ex)
            {
                errorMessageCallback?.Invoke(
                    new Record { EndPoint= _clientSocket.RemoteEndPoint,
                        Message = message,
                        Error =$"error:{ex.Message} , {ex.StackTrace}" }
                    );
            }          
        }

        internal byte[] Convert(string message)
        {
            return Encoding.UTF8.GetBytes(message + '\n');//need \n to be endOfLine
            //var header = BitConverter.GetBytes(body.Length);
            //return header.Concat(body).ToArray();
        }
    }
}
