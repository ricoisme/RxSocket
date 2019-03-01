using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RxSocket;

namespace SimpleServer
{
    class Program
    {
        static ISocketService tcpServer;
        static async Task Main(string[] args)
        {
            CreateHostBuild();
            Console.CancelKeyPress += Console_CancelKeyPress;
            //AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;        
            tcpServer.Accepted.SubscribeOn(TaskPoolScheduler.Default)
                .Subscribe
                (
                 r => Console.WriteLine($"Server started. Listening at {r.LocalEndPoint}"),
                 ex => Console.WriteLine(ex),
                  () => Console.WriteLine("Server Accepted completed")
                );
            tcpServer.Disconnected.SubscribeOn(TaskPoolScheduler.Default)
                .Subscribe
                (
                 r => Console.WriteLine($"{r} Server stooped."),
                 ex => Console.WriteLine(ex),
                 () => Console.WriteLine("Server Disconnected completed")
                );
            tcpServer.Error.SubscribeOn(TaskPoolScheduler.Default)
                .Subscribe
                (
                  r => Console.WriteLine($"Error happened:{r.Method}, {r.Exception.Message},{r.Exception.StackTrace}"),
                 ex => Console.WriteLine(ex),
                 () => Console.WriteLine("Server Error completed")
                );
            tcpServer.Reciever.SubscribeOn(TaskPoolScheduler.Default)
                .Subscribe(
                r => Console.WriteLine($"Receive:{r.Message} from [{r.EndPoint}]"),
                ex => Console.WriteLine(ex),
                () => Console.WriteLine("Socket receiver completed")
                );
            tcpServer.Sender.SubscribeOn(TaskPoolScheduler.Default)
                .Subscribe(
                r => Console.WriteLine($"Self Sending Message:{r.Message}"),
                 ex => Console.WriteLine(ex),
                () => Console.WriteLine("Socket sender completed")
                );
            await tcpServer?.StartAsync();
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
            var serviceProvider = new ServiceCollection()
                .SetupTcpService(configuration.GetSection("ServerConfig"))
                .BuildServiceProvider();
            GetTcpServer(serviceProvider);
        }

        static void GetTcpServer(ServiceProvider serviceProvider)
        {
            tcpServer = serviceProvider.GetRequiredService<ISocketService>();
            var serverConfig = serviceProvider.GetRequiredService<IOptions<ServerConfig>>();

        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs args)
        {
            tcpServer?.Stop();
            Console.WriteLine("The read operation has been interrupted.");
            Console.WriteLine($"Key pressed: {args.SpecialKey}");
            Console.WriteLine($"Cancel property: {args.Cancel}");
            Console.WriteLine("Setting the Cancel property to true.");
            args.Cancel = true;
            Environment.Exit(0);
        }
    }
}
