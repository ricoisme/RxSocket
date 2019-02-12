using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using TcpSocketLib;

namespace SimpleClient
{
    class Program
    {
        static IClientSocket _client;
        static void Main(string[] args)
        {
            CreateHostBuild();   
        }

        private static void CreateHostBuild()
        {
            const string configName = "appsettings.json";
            var basePath = Directory.GetCurrentDirectory();
            if(!File.Exists(Path.Combine(basePath,configName)))
            {
                throw new ArgumentNullException($"{Path.Combine(basePath,configName)} does not exists");
            }
            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile(configName, optional: false, reloadOnChange: true)
                .Build();
            var serviceProvider = new ServiceCollection()
                .SetupTcpService(configuration.GetSection("ServerConfig"))
                .BuildServiceProvider();
            CreateClientSocket(serviceProvider);
        }

        private static void CreateClientSocket(ServiceProvider serviceProvider)
        {
            _client = serviceProvider.GetRequiredService<IClientSocket>();
            var serverConfig = serviceProvider.GetRequiredService<IOptions<ServerConfig>>()?.Value;
            _client.Connect(serverConfig.IpAddress, serverConfig.Port);

            string line = null;
            while ((line = Console.ReadLine()) != "")
            {
                if (line == "r")
                {
                    Console.WriteLine("Reconnecting...");
                    _client.Disconnect();
                    _client.Connect(serverConfig.IpAddress, serverConfig.Port);
                    Console.WriteLine($"IsConnected = {_client.IsConnected}");
                }
                else if (line == "exit")
                {
                    _client.Disconnect();                  
                    break;
                }
                else
                {
                    Console.WriteLine($"{line}  Sending..");
                    _client.SendAsync(line, ErrorMessageCallback).ConfigureAwait(false);
                }
            }
        }

        private static void ErrorMessageCallback<T>(Record<T> record)
        {
            Console.WriteLine($"{record.Message} Sent to {record.EndPoint} with Error. {record.Error}");
#if DEBUG

            if(record.Error.Length>0)
            {
                _client.Disconnect();
                Environment.Exit(0);
            }
#endif
        }
    }
}
