
using BenchmarkDotNet.Running;

namespace RxSocket.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {           
            var summary = BenchmarkRunner.Run<MyBenchmarks>();
        }
    }
}
