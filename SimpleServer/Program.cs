using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using TcpSocketLib;

namespace SimpleServer
{
    class Program
    {
        static IService tcpServer;
        static void Main(string[] args)
        {          
            CreateHostBuild();
            Console.CancelKeyPress += Console_CancelKeyPress;
            //AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            tcpServer.Connected += TcpServer_Connected;
            tcpServer.Disconnected += TcpServer_Disconnected;
            tcpServer.RecievedMessages.SubscribeOn(TaskPoolScheduler.Default)
                .Subscribe(
                r => Console.WriteLine($"Receive:{r.Message} from [{r.EndPoint}]"),
                ex=> Console.WriteLine(ex),
                () => Console.WriteLine("Socket receiver completed")
                );
            tcpServer.SendMessages.SubscribeOn(TaskPoolScheduler.Default)
                .Subscribe(
                r=> Console.WriteLine($"Self Sending Message:{r.Message}"),
                 ex => Console.WriteLine(ex),
                () => Console.WriteLine("Socket sender completed")
                );
            tcpServer?.Start();         
        }       

        private static void TcpServer_Disconnected()
        {
            Console.WriteLine($"{tcpServer.ServerEndPoint.Address} Server stooped. unmount port {tcpServer.ServerEndPoint.Port}");
        }

        private static void TcpServer_Connected()
        {
            Console.WriteLine($"Server started. Listening at {tcpServer.ServerEndPoint.Address}:{tcpServer.ServerEndPoint.Port}");
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
            GetTcpServer(serviceProvider);
        }

        static void GetTcpServer(ServiceProvider serviceProvider)
        {
            tcpServer = serviceProvider.GetRequiredService<IService>();
        }

        //private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        //{
        //    const string dotName = "dotnet";
        //    tcpServer?.Stop();
        //    // https://github.com/dotnet/corefx/pull/31827   
        //    // https://github.com/dotnet/cli/issues/7426
        //    var processes = Process.GetProcessesByName(dotName);
        //    processes?.Where(p => p.Id > 0 && !p.HasExited && p.MainWindowTitle.Contains(dotName, StringComparison.CurrentCultureIgnoreCase))
        //        .FirstOrDefault()?.Kill();
        //}

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs args)
        {
            tcpServer.Connected -= TcpServer_Connected;
            tcpServer.Disconnected -= TcpServer_Disconnected;
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
