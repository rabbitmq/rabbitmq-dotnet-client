using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

namespace RabbitMQ.Benchmarks
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }

    class Config : ManualConfig
    {
        public Config()
        {
            AddJob(Job.Default.WithRuntime(CoreRuntime.Core31));
            AddJob(Job.Default.WithRuntime(ClrRuntime.Net48));
            AddExporter(DefaultExporters.Markdown, DefaultExporters.Csv);
            AddDiagnoser(new DisassemblyDiagnoser(new DisassemblyDiagnoserConfig()), MemoryDiagnoser.Default);
        }
    }
}
