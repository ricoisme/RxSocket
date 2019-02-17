using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Mathematics;
using BenchmarkDotNet.Order;
using TcpSocketLib;

namespace TckSocketLib.Benchmarks
{
    [ShortRunJob]   
    [RankColumn(NumeralSystem.Arabic)]   
    public class MyBenchmarks
    {
        private readonly ServerConfig _config;
        private readonly IClientSocket _client;
        private readonly IService _tcpService;

        [Params(100,1000)]
        public int Totals { get; set; }

        [Params(1024, 4096, 8192)]
        public int BufferSize { get; set; }

        public MyBenchmarks()
        {
            _config = new ServerConfig {
                BufferSize = 10240,
                Backlog =240,
                IpAddress ="127.0.0.1",
                Port=8787,
                Retry=3
            };
            _tcpService = new TcpService(_config);
            _client = new ClientSocket();         
        }

        [GlobalSetup]
        public void Setup()
        {
            _tcpService.Connected += _tcpService_Connected;
            _tcpService.Disconnected += _tcpService_Disconnected;
            _tcpService.Reciever.SubscribeOn(TaskPoolScheduler.Default)
               .Subscribe(
               r => Console.WriteLine($"Receive:{r.Message} from [{r.EndPoint}]"),
               ex => Console.WriteLine(ex),
               () => Console.WriteLine("Socket receiver completed")
               );
            Task.Factory.StartNew(() => _tcpService.Start()); 
        }

        private void _tcpService_Disconnected()
        {
           
        }

        private void _tcpService_Connected()
        {
            _client.Connect(_config.IpAddress, _config.Port);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _client.Disconnect();
            _tcpService.Stop();
        }

        [Benchmark]      
        [MemoryDiagnoser]
        public void SendAsyncMessage()
        {          
            var line = Encoding.UTF8.GetBytes(new string('r', BufferSize) + Environment.NewLine);
            while (Totals>0)
            {
                _client.SendAsync(line,0).ConfigureAwait(false);
                Totals--;
            }
        }
    }
}
