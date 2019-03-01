using System;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace RxSocket.UnitTests
{
    public class Mocks
    {
        private readonly ISocketService _socketService;
        public IClientSocket _clientSocket;
        public bool ClientIsConnected => _clientSocket.IsConnected;
        public IObservable<Socket> Accepted => _socketService.Accepted;
        public IObservable<Record<object>> Reciever => _socketService.Reciever;
        public Mocks()
        {
            _socketService = new TcpService(Const.Ip, Const.Port, Const.Backlog, Const.BufferSize);
            _clientSocket = new ClientSocket();
            _socketService.Accepted.SubscribeOn(TaskPoolScheduler.Default)
              .Subscribe
              (
               r => ClientConnectAsync(),
               ex => throw ex,
               () => Console.WriteLine("Server Accepted completed")
              );
            _socketService.Reciever.SubscribeOn(TaskPoolScheduler.Default)
                .Subscribe(
                r => Console.WriteLine(r.Message),
                ex => throw ex,
                () => Console.WriteLine("Socket receiver completed")
                );
        }

        public Task StartAsync()
        {
            return _socketService.StartAsync();
        }

        public void Stop()
        {
            _socketService.Stop();
        }

        public Task ClientConnectAsync()
        {
            return _clientSocket.ConnectAsync(Const.Ip, Const.Port);
        }

        public void ClientDisconnect()
        {
            _clientSocket.Disconnect();
        }

        public Task SendAsync<T>(T message, int retryMax, Action<Record<T>> errorMessageCallback = null)
        {
            return _clientSocket.SendAsync<T>(message, retryMax, errorMessageCallback);
        }
    }

}
