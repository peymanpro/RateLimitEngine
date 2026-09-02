# ADR-005: Keep Benchmark Methodology Separate from Production Semantics

## Status

Accepted

## Context

Performance benchmarks can become misleading when production code is modified solely to accommodate a benchmark.

RateLimitEngine also has materially different execution characteristics between in-memory and Redis-backed implementations.

## Decision

Benchmark configurations may adapt workload parameters when required to produce a bounded and representative measurement, but production algorithm behavior must not be changed for benchmark convenience.

In-memory benchmarks measure local execution and instrumentation overhead.

Redis benchmarks measure the complete Redis-backed evaluation path and must account for network and Redis round-trip effects.

Concurrency benchmarks use actual asynchronous worker execution rather than merely issuing calls synchronously before awaiting them.

## Consequences

Benchmark results remain tied to an explicit methodology.

Redis latency measurements are not treated as universal infrastructure guarantees.

Instrumentation overhead can be evaluated without changing the underlying algorithm or storage implementation.

Benchmark-specific workload adjustments remain visible in the benchmark source rather than being hidden inside production code.
