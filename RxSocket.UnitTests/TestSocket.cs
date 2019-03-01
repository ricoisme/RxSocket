using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace RxSocket.UnitTests
{
    public class TestSocket : IDisposable
    {
        private readonly Mocks _mocks;
        private readonly ITestOutputHelper _output;
        public TestSocket(ITestOutputHelper output)
        {
            _mocks = new Mocks();
            _output = output;
            Task.Run(_mocks.StartAsync);
        }

        [Fact(Timeout = 5 * 1000)]
        public void Can_Send_Receive_Message()
        {        
            _mocks.ClientIsConnected.Should().BeTrue();
            _mocks.Reciever.Should().NotBeNull();
            _mocks.SendAsync("rico" + Environment.NewLine, 0);

            var message = _mocks.Reciever.SubscribeOn(TaskPoolScheduler.Default)
                  .DistinctUntilChanged().Take(1).GetAwaiter().GetResult();
            message.Should().NotBeNull();
            message.EndPoint.ToString().Should().StartWith("127.0.0.1");
            message.Message.Should().Equals("rico");
            message.Error.Should().BeNullOrEmpty();
            _output.WriteLine(message.EndPoint.ToString());
            _output.WriteLine(message.Message.ToString());
            _output.WriteLine(message.Error);
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
