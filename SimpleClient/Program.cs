using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using TcpSocketLib;

namespace SimpleClient
{
    class Program
    {        
        static void Main(string[] args)
        {
            CreateHostBuild();
           
            Console.ReadLine();
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
            var client = serviceProvider.GetRequiredService<IClientSocket>();
            var serverConfig = serviceProvider.GetRequiredService<IOptions<ServerConfig>>()?.Value;
            client.Connect(serverConfig.IpAddress, serverConfig.Port);

            string line = null;
            while ((line = Console.ReadLine()) != "")
            {
                if (line == "r")
                {
                    Console.WriteLine("Reconnecting...");
                    client.Disconnect();
                    client.Connect(serverConfig.IpAddress, serverConfig.Port);
                    Console.WriteLine($"IsConnected = {client.IsConnected}");
                }
                else if (line == "exit")
                {
                    client.Disconnect();                  
                    break;
                }
                else
                {
                    Console.WriteLine($"{line}  Sending..");
                    client.SendAsync(line).ConfigureAwait(false);
                }
            }
        }
    }
}
