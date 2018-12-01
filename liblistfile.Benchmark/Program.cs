using System;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Running;

namespace liblistfile.Benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = ManualConfig.Create(DefaultConfig.Instance)
                .With
                (
                    new SimpleFilter
                    (
                        b =>
                        {
                            var isClrJob = b.Job.Environment.Runtime?.Name == "Clr";
                            var isRunningOnMono = !(Type.GetType("Mono.Runtime") is null);

                            if (!isClrJob)
                            {
                                return true;
                            }

                            return !isRunningOnMono;
                        }
                    )
                );

            BenchmarkRunner.Run<Benchmark>(config);
        }
    }
}
