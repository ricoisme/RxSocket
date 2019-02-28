# RxSocket 
<p align=center>
<a target="_blank" href="https://www.nuget.org/packages/RxSocketWithIoPipeLines/" title="nuget version"><img src="https://img.shields.io/badge/nuget-v1.0.0-blue.svg"></a>
<a target="_blank" href="https://github.com/ricoisme/RxSocket/tree/master" title="Build Status"><img src="https://img.shields.io/badge/build-passing-green.svg"></a>
<a target="_blank" href="http://nodejs.org/download/" title="net standard version">
<img src="https://img.shields.io/badge/netstandard-2.0-blue.svg"></a>
<a target="_blank" href="https://opensource.org/licenses/MIT" title="License: MIT">
<img alt="GitHub" src="https://img.shields.io/github/license/ricoisme/RxSocket.svg">
</a>
</p>


The RxSocket use io.pipelines with RX to handle socket connection and networkstream.

it draw inspiration from [davidfowl's project](https://github.com/davidfowl/TcpEcho).

ISocketService interface of TcpService providing socket both client and server APIs. 

Another thing,also providing subscribe stream both sender and reciever.

It is provided under the MIT License

## Features
Simple Tcp Server with IO.Pipelines  
Simple asynchronous event based Reciever and Sender

## Usage
To install RxSocketWithIoPipelines from within Visual studion, search for RxSocketWithIoPipeLines in the NuGet Package Manager UI, or run the following command in the Package Manager Console:
```
Install-Package RxSocketWithIoPipeLines -Version 1.0.0
```

## TCP Server
```
var  tcpServer = serviceProvider.GetRequiredService<IService>();
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
                  r => Console.WriteLine($"Error happend:{r.Method}, {r.Exception.Message},{r.Exception.StackTrace}"),
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
```

## Socket Client
```
 var client = _serviceProvider.GetRequiredService<IClientSocket>();
  Task.Run(async () => await _client.ConnectAsync(_serverConfig.IpAddress, _serverConfig.Port)
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
```
