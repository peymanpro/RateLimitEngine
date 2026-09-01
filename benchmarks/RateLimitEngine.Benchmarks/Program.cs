using BenchmarkDotNet.Running;
using RateLimitEngine.Benchmarks;

BenchmarkSwitcher
    .FromAssembly(typeof(FixedWindowInMemoryBenchmarks).Assembly)
    .Run(args);
