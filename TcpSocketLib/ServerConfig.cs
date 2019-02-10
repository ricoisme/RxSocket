
namespace TcpSocketLib
{
    public sealed class ServerConfig
    {
        public int BufferSize { get; set; }
        public int Backlog { get; set; }
        public string IpAddress { get; set; }
        public int Port { get; set; }
    }
}
