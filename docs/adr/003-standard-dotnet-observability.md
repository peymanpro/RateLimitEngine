# ADR-003: Use Standard .NET Diagnostics Primitives

## Status

Accepted

## Context

RateLimitEngine needs metrics and tracing without forcing consumers to depend on a specific telemetry vendor or OpenTelemetry package.

Observability should also remain outside algorithm and storage semantics.

## Decision

Instrumentation is implemented through `System.Diagnostics.Metrics` and `System.Diagnostics.ActivitySource`.

`InstrumentedRateLimiter` decorates `IRateLimiter` and records metrics and activities around evaluations.

The engine does not take a hard dependency on OpenTelemetry.

## Consequences

Consumers can connect the standard .NET diagnostics sources to their existing telemetry infrastructure.

Algorithms remain independent of telemetry implementations.

Instrumentation can be enabled and consumed without changing the rate-limit decision contract.
