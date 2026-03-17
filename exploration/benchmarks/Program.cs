using BenchmarkDotNet.Running;

namespace IStreamableBenchmarks;

public class Program
{
    public static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
