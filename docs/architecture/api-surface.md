# API Surface

RateLimitEngine exposes a small set of stable abstractions around rate-limit evaluation, policy configuration, and storage.

## Core Contract

`IRateLimiter` is the primary evaluation abstraction:

```csharp
ValueTask<RateLimitDecision> EvaluateAsync(
    RateLimitRequest request,
    RateLimitPolicy policy,
    CancellationToken cancellationToken = default);
```

The contract is independent of storage and HTTP concerns.

## Policy

`RateLimitPolicy` defines the permit limit and rate-limit window.

Both values must be positive.

## Request

`RateLimitRequest` identifies the logical rate-limit subject and carries request-level evaluation information such as cost.

## Decision

`RateLimitDecision` is the common result returned by all algorithms.

It contains:

- Allowed
- Limit
- Remaining
- ResetAfter
- RetryAfter

The model validates incompatible combinations such as a successful decision carrying RetryAfter.

## Store Contracts

Algorithms use dedicated store abstractions:

- `IFixedWindowStore`
- `ITokenBucketStore`
- `ISlidingWindowStore`
- `IGcraStore`

These contracts isolate algorithm state transitions from storage implementation.

## Clock Abstraction

`IClock` provides a testable time boundary for local algorithms and integration behavior.

Production code uses `SystemClock`; tests can use `FakeClock`.

## Factories

The engine provides an `IRateLimiterFactory` boundary for selecting the requested algorithm and backend.

In-memory and Redis dependency-injection registrations provide the corresponding factory implementations.

## ASP.NET Core

The ASP.NET Core layer exposes middleware configuration through `RateLimitOptions`.

HTTP concerns remain outside the Core API. The middleware translates `RateLimitDecision` into HTTP behavior and response headers.

## Extensibility

New algorithms can be introduced by implementing the common limiter behavior and defining an appropriate store contract.

New storage backends can implement the existing algorithm-specific store abstractions without changing algorithm semantics.
