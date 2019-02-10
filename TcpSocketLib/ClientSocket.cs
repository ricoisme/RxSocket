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
        Task SendAsync(string message);
    }

    public sealed class ClientSocket: IClientSocket
    {
        private readonly Socket _clientSocket;
        public ClientSocket()
        {
            _clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        }

        bool IClientSocket.IsConnected => _clientSocket.Connected;

        void IClientSocket.Connect(string IP, int Port)
        {
            var ipAddress = IPAddress.Parse(IP);
            // _clientSocket.ConnectAsync(new IPEndPoint(ipAddress, Port));   
            Task.Factory
                .FromAsync(_clientSocket.BeginConnect, _clientSocket.EndConnect, 
                 IP, Port,null)
                .ContinueWith(_ => _clientSocket.Connect(new IPEndPoint(ipAddress, Port)), TaskContinuationOptions.OnlyOnRanToCompletion);
            Console.WriteLine($"{ _clientSocket.GetHashCode()} Connecting to {IP} at Port {Port}");
        }

        void IClientSocket.Disconnect()
        {           
            _clientSocket.Shutdown(SocketShutdown.Both);
            _clientSocket.Close();
            Console.WriteLine($"{ _clientSocket.GetHashCode()} Disconnected.");
        }

        Task IClientSocket.SendAsync(string message)
        {
            var buffer = Convert(message);
            return _clientSocket.SendAsync(new ArraySegment<byte>(buffer), SocketFlags.None);
        }

        internal byte[] Convert(string message)
        {
            return Encoding.UTF8.GetBytes(message + '\n');//need \n to be endOfLine
            //var header = BitConverter.GetBytes(body.Length);
            //return header.Concat(body).ToArray();
        }
    }
}
