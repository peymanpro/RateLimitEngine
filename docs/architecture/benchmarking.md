# Benchmarking

RateLimitEngine includes BenchmarkDotNet suites for evaluating algorithm behavior, instrumentation overhead, and concurrency behavior.

## Benchmark Categories

The benchmark suite covers:

- In-memory algorithm benchmarks
- Redis-backed algorithm benchmarks
- In-memory concurrency benchmarks
- Redis concurrency benchmarks

## Methodology

Benchmarks compare equivalent raw and instrumented limiter paths.

The instrumented path uses InstrumentedRateLimiter around the same underlying limiter implementation. This isolates the approximate cost of the observability layer without changing the algorithm or storage implementation.

Concurrency benchmarks use actual parallel workers executed through Task.Run. Each worker performs a configured number of asynchronous evaluations.

## In-Memory Benchmarks

In-memory benchmarks measure local algorithm execution without Redis network latency.

They are useful for comparing relative algorithm behavior and observing the overhead introduced by instrumentation.

Observed instrumentation overhead includes additional metrics, activity setup, tag construction, and timing operations.

Allocation measurements consistently show additional allocation from instrumentation. Latency differences are workload- and runtime-dependent and should not be interpreted as universal production performance characteristics.

## Redis Benchmarks

Redis benchmarks compare the same Redis-backed limiter through raw and instrumented paths.

Redis introduces network and server round-trip latency that is substantially larger than the local instrumentation work in these scenarios.

Consequently, small latency differences between raw and instrumented Redis runs are not considered statistically meaningful evidence that instrumentation improves or degrades Redis throughput.

Allocation differences provide a more stable signal for the cost of the instrumentation layer.

## Concurrency Benchmarks

Concurrency benchmarks vary worker concurrency and measure the complete set of operations performed by those workers.

The in-memory suite evaluates concurrency levels from 1 through 16 workers.

The Redis suite evaluates concurrency levels from 1 through 8 workers.

These benchmarks are intended to expose contention behavior and the interaction between concurrency and instrumentation overhead.

## Benchmark Hygiene

Benchmark methodology must not modify production algorithm behavior merely to make a benchmark easier to run.

Where an algorithm requires a particular workload shape to avoid unrealistic state growth, the benchmark configuration may be adjusted while keeping the production implementation unchanged.

Benchmark logs and BenchmarkDotNet generated artifacts are excluded from version control.

## Interpretation

Benchmark results are environment-specific.

Latency numbers should be interpreted together with allocation measurements, workload parameters, concurrency level, and infrastructure conditions.

Redis results in particular should not be presented as universal latency or throughput guarantees.
