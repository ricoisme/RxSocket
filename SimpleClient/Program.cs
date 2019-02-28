using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.ComponentModel;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using RxSocket;

namespace SimpleClient
{
    class Program
    {
        private static System.Timers.Timer _timers = new System.Timers.Timer();
        private static bool _needReConnecting { get; set; }
        static IClientSocket _client;
        private static IConfigurationRoot _configuration { get; set; }
        private static ServiceProvider _serviceProvider { get; set; }
        private static ServerConfig _serverConfig { get; set; }
        static void Main(string[] args)
        {
            _timers.Interval = 10 * 1000;
            _timers.Elapsed += _timers_Elapsed;
            _timers.AutoReset = true;
            _needReConnecting = false;
            CreateHostBuild();
        }

        private static void _timers_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!_needReConnecting)
            {
                Console.WriteLine("dont need to connect againg.");
                return;
            }
            _needReConnecting = false;
            Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId}:ThreadState:{Thread.CurrentThread.ThreadState}, Reconnecting to Server.");
            if (_client == null)
            {
                _client = _serviceProvider.GetRequiredService<IClientSocket>();
            }
            if (!_client.IsConnected)
            {
                Task.Run(async () => await _client.ConnectAsync(_serverConfig.IpAddress, _serverConfig.Port))
                      .ContinueWith(tresult =>
                      {
                          if (tresult.Status == TaskStatus.Faulted)
                          {
                              _needReConnecting = true;
                              Console.WriteLine($"{Task.CurrentId} status changed to  {_needReConnecting}.");
                          }
                          else
                          {
                              Console.WriteLine("Successfully connected to the Server.");
                          }
                      });
            }
        }

        private static void CreateHostBuild()
        {
            const string configName = "appsettings.json";
            var basePath = Directory.GetCurrentDirectory();
            if (!File.Exists(Path.Combine(basePath, configName)))
            {
                throw new ArgumentNullException($"{Path.Combine(basePath, configName)} does not exists");
            }
            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile(configName, optional: false, reloadOnChange: true)
                .Build();
            _configuration = configuration;
            _serviceProvider = new ServiceCollection()
                .SetupTcpService(configuration.GetSection("ServerConfig"))
                .BuildServiceProvider();
            CreateClientSocket();
        }

        private static void CreateClientSocket()
        {
            _serverConfig = _serviceProvider.GetRequiredService<IOptions<ServerConfig>>()?.Value;
            _client = _serviceProvider.GetRequiredService<IClientSocket>();
            _timers.Start();
            Task.Run(async () => _client.ConnectAsync(_serverConfig.IpAddress, _serverConfig.Port)
            .ConfigureAwait(false))
                .ContinueWith(tresult =>
                {
                    if (tresult.Status == TaskStatus.Faulted)
                    {
                        Console.WriteLine($"connect {_serverConfig.IpAddress}:{_serverConfig.Port} faulted");
                        _needReConnecting = true;
                    }
                });
            string line = null;
            while ((line = Console.ReadLine()) != "")
            {
                if (line == "r")
                {
                    Console.WriteLine("Reconnecting...");
                    _client.Disconnect();
                    Task.Run(async () => await _client.ConnectAsync(_serverConfig.IpAddress, _serverConfig.Port))
                         .ContinueWith(tresult =>
                         {
                             if (tresult.Status == TaskStatus.Faulted)
                             {
                                 Console.WriteLine($"Connected Faulted.");
                             }
                             else
                             {
                                 Console.WriteLine($"IsConnected = {_client.IsConnected}");
                             }
                         });
                }
                else if (line == "exit")
                {
                    _client.Disconnect();
                    break;
                }
                else
                {
                    Console.WriteLine($"{line}  Sending..");
                    _client.SendAsync(line, 3, ErrorMessageCallback).ConfigureAwait(false);
                }
            }
        }

        private static void ErrorMessageCallback<T>(Record<T> record)
        {
            Console.WriteLine($"{record.Message} Sent to {record.EndPoint} with Error. {record.Error}");
#if DEBUG

            if (record.Error.Length > 0)
            {
                _client.Disconnect();
                Environment.Exit(0);
            }
#endif
        }
    }
}
