using System;
using System.Net.Sockets;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace RxSocket.UnitTests
{
    public class TestServer : IDisposable
    {
        private readonly Mocks _mocks;
        private readonly ITestOutputHelper _output;
        public TestServer(ITestOutputHelper output)
        {
            _mocks = new Mocks();
            _output = output;
            Task.Run(_mocks.StartAsync);
        }

        [Fact(Timeout = 5 * 1000)]
        public void Can_Send_Receive_Message()
        {
            _mocks.Accepted.SubscribeOn(TaskPoolScheduler.Default)
                 .DistinctUntilChanged().Take(1).GetAwaiter().GetResult();
            _mocks.ClientIsConnected.Should().BeTrue();
            _mocks.Reciever.Should().NotBeNull();

            _mocks.SendAsync("rico" + Environment.NewLine, 0)
                .ContinueWith(tresult =>
                {
                    var status = tresult.Status;
                    status.Should().NotBeSameAs(TaskStatus.Faulted);
                });

            var message = _mocks.Reciever.SubscribeOn(TaskPoolScheduler.Default)
                  .DistinctUntilChanged().Take(1).GetAwaiter().GetResult();
            message.Should().NotBeNull();
            _output.WriteLine(message.Message.ToString());
            message.Message.Should().Equals("rico");

            _output.WriteLine("Can_Send_Receive_Message done.");
        }

        public void Dispose()
        {
            _mocks.Stop();
            _mocks.ClientDisconnect();
            _output.WriteLine("Dispose done.");
        }
    }
}
