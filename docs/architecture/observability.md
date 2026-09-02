# Observability

RateLimitEngine provides observability through standard .NET diagnostics primitives.

## Metrics

The engine exposes a Meter named `RateLimitEngine`.

The following instruments are provided:

- `ratelimit.requests.allowed`: number of accepted requests.
- `ratelimit.requests.rejected`: number of rejected requests.
- `ratelimit.evaluation.failures`: number of evaluations that ended with an exception.
- `ratelimit.redis.retry.attempts`: number of Redis infrastructure retry attempts.
- `ratelimit.evaluation.duration`: evaluation duration in milliseconds.

Allowed and rejected request metrics are tagged with algorithm and backend.

Failure metrics additionally include the exception type.

## Diagnostics

The engine exposes an `ActivitySource` named `RateLimitEngine`.

Each instrumented evaluation creates an activity named `RateLimitEngine.Evaluate` when an activity listener is configured.

Activities include:

- rate-limit algorithm
- rate-limit backend
- whether the request was allowed

Failed evaluations are marked with an error status and include exception information and an exception event.

## Instrumentation Boundary

Observability is implemented by `InstrumentedRateLimiter`, which decorates an existing `IRateLimiter`.

The underlying limiter remains responsible for rate-limit semantics. The decorator is responsible for measuring evaluation duration and recording diagnostics.

Instrumentation therefore does not require algorithms or stores to depend directly on metrics or tracing infrastructure.

## Cancellation

Operation cancellation is propagated and is not recorded as a normal evaluation failure.

The duration measurement still completes through the instrumentation finally block.

## OpenTelemetry

The implementation uses `System.Diagnostics.Metrics` and `System.Diagnostics.ActivitySource` directly.

OpenTelemetry is not a hard runtime dependency of the engine. Consumers can connect standard .NET meters and activity sources to their preferred telemetry pipeline.

## Redis Retry Visibility

Redis retry attempts are counted at the infrastructure executor boundary.

This makes retry behavior observable without treating a successfully recovered retry as a rate-limit evaluation failure.
