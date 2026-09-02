# RateLimitEngine

> A production-oriented, extensible, distributed rate-limiting library for .NET and ASP.NET Core.

RateLimitEngine is a .NET 8 rate-limiting library designed around algorithm correctness, deterministic behavior, concurrency safety, distributed coordination, explicit failure semantics, and testability. It provides four rate-limiting algorithms, in-memory and Redis-backed state, ASP.NET Core middleware, concurrency and distributed validation, and explicit infrastructure failure strategies. The implementation intentionally separates rate-limiting algorithms from HTTP concerns and from concrete state-storage infrastructure.

## Project Status

> **Project Status:** Active development  
> **Current Phase:** Phase 15 — Resilience  
> **Current Phase Status:** Partial  
> **Completed Through:** Phase 14  
> **Current local verification:** 122 tests passed, 0 failed, 0 skipped  
> **Target:** Production-grade open-source .NET library

The core engine, four rate-limiting algorithms, Redis distributed backend, ASP.NET Core integration, distributed multi-instance validation, and most currently planned resilience scenarios are implemented and tested. The project is currently completing the remaining resilience work before moving into observability, benchmarking, documentation, packaging/CI, and final production-readiness phases.

**Phase 15 is not complete.** In particular, the dedicated Lua-script-failure resilience scenario and infrastructure retry behavior are not currently implemented/verified.

The project should be considered **production-oriented, but not yet production-ready or v1.0-ready**.

## What is RateLimitEngine?

Rate limiting looks simple at an API surface: evaluate a request and decide whether it is allowed. The underlying state transitions are much more difficult to implement correctly once concurrent and distributed workloads are involved.

Multiple requests can touch the same logical rate-limit state at the same time. A naive read/check/update sequence can allow requests that should have been rejected. Time boundaries can change the result of a decision. Different algorithms have different burst and recovery semantics. In a distributed deployment, independent application processes need to coordinate shared state without relying on process-local memory.

RateLimitEngine treats those concerns as part of the core engineering problem rather than as optional implementation details. The design therefore emphasizes:

- algorithm correctness and explicit decision semantics
- deterministic and reproducible time-dependent behavior
- concurrency-safe state transitions
- distributed coordination through Redis
- algorithm-specific store abstractions
- explicit availability versus enforcement trade-offs
- testable infrastructure boundaries
- separation between core algorithms and HTTP integration

The public core contract is centered on `IRateLimiter.EvaluateAsync(...)`, which evaluates a `RateLimitRequest` against a `RateLimitPolicy` and returns a `RateLimitDecision`.

## Why This Project Exists

### Single-process challenges

A single process still has to solve several correctness problems:

- **Race conditions:** multiple concurrent callers can observe and update the same state.
- **Mutable shared state:** counters, timestamps, token counts, and GCRA state must be updated safely.
- **Time boundaries:** fixed windows, sliding windows, token refill, and GCRA all depend on precise time semantics.
- **Burst behavior:** each algorithm has different behavior around bursts and recovery.
- **Deterministic testing:** tests should not depend on sleeping for arbitrary wall-clock time or on timing luck.

### Distributed challenges

With multiple application instances, the problem becomes state coordination as well:

- **Shared state:** independent instances must observe the same logical rate-limit key.
- **Atomic transitions:** a distributed check and update cannot safely be split into unrelated operations.
- **Expiration:** stale state must expire instead of accumulating forever.
- **Independent connections:** tests validate multiple independent Redis connections against shared state.
- **Redis availability:** connection failures, timeouts, and unexpected backend exceptions need explicit semantics.
- **Recovery:** the system must have defined behavior when Redis becomes available again.

### Integration challenges

HTTP-facing rate limiting also introduces integration semantics:

- rejected requests should result in `429 Too Many Requests`
- rate-limit metadata should be represented through response headers
- `Retry-After` must remain a rate-limit decision signal, not be confused with infrastructure retry
- algorithm/backend selection should be configurable
- backend failures should have a deterministic policy through FailOpen or FailClosed

## Key Features

- Fixed Window algorithm
- Sliding Window algorithm
- Token Bucket algorithm
- GCRA (Generic Cell Rate Algorithm)
- In-Memory state backend
- Redis state backend
- Atomic Redis-side Lua state transitions
- Algorithm-specific store abstractions: `IFixedWindowStore`, `ISlidingWindowStore`, `ITokenBucketStore`, `IGcraStore`
- `IRateLimiter` and `IRateLimiterFactory` core abstractions
- Configurable backend and algorithm selection
- ASP.NET Core middleware integration
- Default remote-IP key resolution through `RemoteIpRateLimitKeyResolver`
- FailOpen and FailClosed failure strategies
- Cancellation propagation through rate-limit evaluation
- Redis timeout and unavailable-backend handling in the tested scenarios
- Redis restart/recovery validation
- Deterministic time abstraction through `IClock`
- Dedicated concurrency tests
- Distributed multi-instance Redis tests
- Weighted request costs
- Rate-limit response metadata through `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset-After`, and `Retry-After` where applicable

The repository also contains a benchmark project skeleton, but it is **not yet a completed benchmark suite** and does not currently provide BenchmarkDotNet results.

## Supported Algorithms

### Fixed Window

Fixed Window divides time into discrete intervals and maintains a request/cost count for the current interval.

For a policy with permit limit `L` and window `W`, the current fixed interval admits requests while the accumulated cost remains within `L`. Request cost is supported, so a request can consume more than one permit.

The algorithm is straightforward and has clear window boundaries, but boundary bursts are an inherent characteristic: activity near the end of one window and the beginning of the next can be separated into different counters.

The Redis implementation performs the relevant read/check/update/expiration operation inside a Lua script and uses Redis server time for the window calculation.

### Sliding Window

Sliding Window evaluates activity over a continuously moving time interval rather than using fixed calendar-style buckets. The implementation tracks timestamped request entries and associated costs, removes expired entries, and calculates the current consumed and remaining capacity from the active set.

Compared with Fixed Window, this reduces sharp boundary effects at the cost of more state management. The Redis implementation uses a sorted set plus aggregate state and performs pruning, admission checks, updates, and expiration in one Lua-backed transition.

The implementation also uses a sequence component in Redis entry identity, so multiple events with the same millisecond timestamp do not collapse into the same sorted-set member.

### Token Bucket

Token Bucket maintains a bucket with a configured capacity `C` and replenishes tokens at a rate derived from the policy:

```text
refillRate = PermitLimit / Window.TotalSeconds
```

At time `t`, the conceptual token level is bounded by capacity:

```text
T(t) = min(C, T(t0) + r * (t - t0))
```

where:

- `C` is the configured bucket capacity
- `r` is the refill rate in tokens per second
- `T(t)` is the available token count

A request consumes `Cost` tokens. This enables differentiated request weights while retaining burst capability up to the bucket capacity.

In the current implementation, token-bucket capacity is supplied through `TokenBucketOptions`; the default capacity used by the built-in DI registration is `100`.

The Redis implementation stores token and timestamp state and performs refill, admission, update, and expiration logic inside a Lua script using Redis server time.

### GCRA

GCRA (Generic Cell Rate Algorithm) models a theoretical arrival time (TAT) and enforces an inter-arrival interval while allowing a controlled burst tolerance.

For a policy with permit limit `L` and window `W`, RateLimitEngine derives:

```text
interval       = W / L
burstTolerance = interval * (L - 1)
```

The GCRA store evaluates the request against the current TAT and, when admitted, advances TAT according to request cost. The Redis implementation performs the TAT read/check/update and expiration atomically in Lua and uses Redis server time for the distributed state transition.

The implementation also exposes `RetryAfter` and `Remaining` semantics through the `RateLimitDecision` produced by the algorithm. A request whose cost exceeds the entire permit limit is rejected before store evaluation.

## Algorithm Comparison

| Algorithm | Burst Behavior | State Model | Time Model | In-Memory | Redis | Cost | Distributed |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Fixed Window | Boundary-based bursts are possible | Counter per logical window | Fixed intervals | Yes | Yes | Yes | Yes, through shared Redis state |
| Sliding Window | Smoother than Fixed Window around boundaries | Timestamped weighted entries plus aggregate state | Continuously moving window | Yes | Yes | Yes | Yes, through shared Redis state |
| Token Bucket | Explicit burst capacity | Token count + last timestamp | Continuous refill | Yes | Yes | Yes | Yes, through shared Redis state |
| GCRA | Controlled burst tolerance around theoretical arrival time | TAT state | Continuous interval/TAT model | Yes | Yes | Yes | Yes, through shared Redis state |

These are implementation and behavioral characteristics of the current repository. They should not be interpreted as a universal benchmark ranking or as a formal proof of distributed-system correctness under every possible failure mode.

## Architecture

RateLimitEngine is organized so that the algorithm layer is independent from HTTP and from concrete persistence infrastructure.

```text
Application / HTTP
       |
       v
ASP.NET Core Integration
       |
       v
Configurable Factory / Factory Provider
       |
       v
Selected Algorithm
       |
       v
Algorithm-specific Store Abstraction
       |
       +-------------------+
       |                   |
       v                   v
   In-Memory             Redis
```

The four core store contracts are intentionally algorithm-specific:

- `IFixedWindowStore`
- `ITokenBucketStore`
- `ISlidingWindowStore`
- `IGcraStore`

This avoids forcing unrelated algorithms behind a single generic state contract and allows each algorithm to request the state operation it actually needs.

The Redis deployment model is:

```text
Application Instance A ----\
Application Instance B -----+----> Shared Redis
Application Instance C ----/
```

Redis provides the shared rate-limit state used by multiple application instances. The Redis stores use StackExchange.Redis and Lua-backed atomic state transitions where the check/update operation must be executed as a single Redis-side transition.

Redis therefore acts as distributed state infrastructure, not as the implementation of the core algorithms themselves.

### Conservative distributed semantics

Redis provides shared rate-limit state across application instances, while atomic Redis-side state transitions coordinate concurrent updates in the scenarios covered by the test suite.

The repository does **not** claim formal global linearizability, a formal distributed consistency proof, immunity to clock skew, immunity to network delay or message reordering, or a mathematical proof of correctness for every possible distributed failure mode.

## Core Abstractions

### `IRateLimiter`

The primary algorithm contract:

```csharp
ValueTask<RateLimitDecision> EvaluateAsync(
    RateLimitRequest request,
    RateLimitPolicy policy,
    CancellationToken cancellationToken = default);
```

An implementation receives a request and a policy and returns a deterministic decision model.

### `IRateLimiterFactory`

Factories decouple algorithm selection from algorithm construction:

```csharp
IRateLimiter Create(RateLimitAlgorithm algorithm);
```

The repository currently has separate in-memory and Redis factory implementations, plus ASP.NET Core configuration that chooses the appropriate backend.

### Algorithm-specific store contracts

The storage abstractions are intentionally specialized:

```text
IFixedWindowStore    -> IncrementAsync(...)
ISlidingWindowStore  -> EvaluateAsync(...)
ITokenBucketStore    -> ConsumeAsync(...)
IGcraStore           -> EvaluateAsync(...)
```

The result types returned by these contracts contain the state information needed to construct a `RateLimitDecision`.

### `IClock`

`IClock` abstracts both UTC time and elapsed-time measurement:

```csharp
public interface IClock
{
    DateTimeOffset UtcNow { get; }
    long GetTimestamp();
    TimeSpan GetElapsedTime(long startingTimestamp);
}
```

The built-in production clock uses .NET's `TimeProvider` through the public `IClock` abstraction.

### Request, policy, and decision models

`RateLimitRequest` contains a logical key and a positive request `Cost`.

`RateLimitPolicy` contains a positive `PermitLimit` and positive `Window`.

`RateLimitDecision` contains:

- `Allowed`
- `Limit`
- `Remaining`
- optional `ResetAfter`
- optional `RetryAfter`

The decision model validates its own invariants, including the distinction between successful decisions and retry metadata.

### Backend and algorithm selection

The repository exposes:

```csharp
enum RateLimitAlgorithm
{
    FixedWindow,
    SlidingWindow,
    TokenBucket,
    Gcra
}

enum RateLimitBackend
{
    InMemory,
    Redis
}
```

ASP.NET Core adds `RateLimitFailureStrategy` with `FailOpen` and `FailClosed`.

## Time Semantics

Time is a first-class dependency because every supported algorithm is time-dependent.

### `IClock`

In-memory algorithm stores receive an `IClock`. The built-in production implementation uses .NET's `TimeProvider.System`, exposing UTC time, timestamps, and elapsed-time calculation through the common abstraction.

### Deterministic test time

The test infrastructure contains `FakeClock`, an `IClock` implementation whose UTC time can be advanced deterministically. This allows tests to validate windows, token refill, expiration, and time boundaries without depending on real-time sleeps.

### Redis-side time

The Redis implementations use Redis's `TIME` command inside the Lua scripts that perform the distributed state transitions. This keeps the time used by those atomic Redis-side operations tied to the Redis server executing the script.

This does **not** constitute a guarantee that every distributed operation is immune to clock skew or that all system clocks in a deployment are globally synchronized. The scope is the Redis-side state transition used by the current implementations.

## Storage Backends

### In-Memory

The in-memory backend stores rate-limit state inside the application process.

Characteristics:

- no external infrastructure dependency
- appropriate for single-instance applications and local execution
- thread-safe behavior is implemented in the in-memory stores
- state is process-local and is not shared automatically between application instances
- application restart removes in-memory state

The in-memory factory constructs one limiter instance per algorithm and reuses those algorithm instances so state is preserved across calls instead of being recreated for every request.

### Redis

The Redis backend is intended for deployments where multiple application instances need shared rate-limit state.

The implementation uses:

- StackExchange.Redis
- one Redis store abstraction per algorithm
- Lua scripts for atomic state transitions
- Redis expiration for lifecycle management
- Redis server time for the distributed Lua transitions
- explicit cancellation propagation
- tested handling for unavailable Redis, timeout, and recovery scenarios

Using Redis provides shared state and atomic state transitions for the tested scenarios. It does not by itself imply stronger distributed guarantees than the repository and Redis execution model establish.

## Redis Atomicity

The Redis backend uses Lua scripts for operations where checking and changing rate-limit state must happen together.

Depending on the algorithm, a script may combine operations such as:

1. read the current state
2. obtain Redis server time
3. determine whether the request is admissible
4. apply request cost
5. update counters, timestamps, tokens, or TAT
6. calculate remaining/reset/retry information
7. apply expiration
8. return the resulting decision inputs

Examples include:

- Fixed Window: read the current window counter, check cost against the limit, increment, and set expiration.
- Sliding Window: prune expired entries, calculate consumed cost, decide admission, add the new entry, update aggregate state, and expire the keys.
- Token Bucket: replenish tokens, evaluate the requested cost, persist the new state, and calculate recovery information.
- GCRA: read TAT, evaluate burst tolerance, advance TAT when accepted, calculate remaining/retry information, and expire the state.

Lua scripting is therefore a real implementation mechanism in this repository. However, Phase 15.4 is still incomplete because the project does not yet have a dedicated resilience scenario that intentionally fails a Lua script and verifies the resulting recovery/failure semantics.

## ASP.NET Core Integration

The ASP.NET Core package adds a lightweight HTTP integration layer around the framework-agnostic rate-limiting engine.

The main pieces are:

- `RateLimitMiddleware`
- `RateLimitMiddlewareExtensions`
- `RateLimitOptions`
- `IRateLimitKeyResolver`
- `RemoteIpRateLimitKeyResolver`
- `RateLimitFailureStrategy`

The middleware resolves a key, creates a `RateLimitRequest`, creates a `RateLimitPolicy`, evaluates the selected limiter, writes rate-limit headers, and either continues the HTTP pipeline or returns a rate-limit/failure response.

### HTTP behavior

Rejected rate-limit decisions produce:

```text
429 Too Many Requests
```

Infrastructure failures under `FailClosed` produce:

```text
503 Service Unavailable
```

Infrastructure failures under `FailOpen` allow the request to continue through the next middleware.

### Headers

The middleware currently writes these headers when the corresponding decision data is available:

```text
X-RateLimit-Limit
X-RateLimit-Remaining
X-RateLimit-Reset-After
Retry-After
```

`X-RateLimit-Reset` is **not** a currently implemented header and should not be assumed to exist.

`Retry-After` and `ResetAfter` are rate-limit decision semantics. They do **not** represent automatic infrastructure retry behavior.

### Key resolution

The default registration uses `RemoteIpRateLimitKeyResolver`, which derives the logical rate-limit key from the HTTP request context. Applications can replace the resolver with another implementation of `IRateLimitKeyResolver` when a different client identity model is appropriate.

## Failure Semantics

Infrastructure failures are treated as explicit policy decisions rather than accidental exceptions leaking into HTTP behavior.

### FailOpen

When rate-limit storage evaluation fails, the middleware continues the request pipeline.

This prioritizes availability over enforcement. The application remains reachable, but a backend outage can temporarily disable rate-limit enforcement for affected requests.

### FailClosed

When rate-limit storage evaluation fails, the middleware returns `503 Service Unavailable` instead of allowing the request to continue.

This prioritizes enforcement over availability.

### 429 vs. 503

The distinction is intentional:

| Situation | HTTP result | Meaning |
| --- | --- | --- |
| Rate-limit decision rejects a valid evaluation | `429` | The configured limit was reached |
| Rate-limit infrastructure fails under FailOpen | Request continues | Availability is prioritized |
| Rate-limit infrastructure fails under FailClosed | `503` | Enforcement is prioritized, but the backend cannot evaluate the limit |

Cancellation caused by the caller's request-abort signal is propagated rather than converted into a synthetic rate-limit response.

## Resilience

The current resilience implementation and tests cover:

- Redis connection failure behavior
- Redis timeout behavior
- Redis unavailable behavior
- cancellation propagation
- FailOpen handling
- FailClosed handling
- recovery after Redis becomes available again
- multi-instance recovery scenarios covered by the test suite

### Remaining resilience work

Two areas remain open in Phase 15:

- **Lua script failure scenario:** Lua implementations exist and are used for atomic transitions, but a dedicated resilience test that intentionally causes a Lua script failure is not currently established. **Phase 15.4 is therefore not complete.**
- **Infrastructure retry behavior:** the project does not currently implement an automatic Redis/infrastructure retry policy as part of rate-limit evaluation. **Phase 15.6 is not implemented.**

The `RetryAfter` property on `RateLimitDecision` should not be confused with this missing infrastructure retry feature. `RetryAfter` tells a client when a rejected request can meaningfully retry the rate limit; it is not a backend reconnection or command-retry mechanism.

## Distributed Validation

The repository contains a dedicated `RateLimitEngine.DistributedTests` project for validating multi-instance behavior against shared Redis state.

The distributed test strategy includes scenarios for:

- independent Redis connections
- shared logical keys
- shared global rate limits
- concurrent access from multiple application instances
- weighted request costs
- all four algorithms
- distributed expiration
- recovery after expiration
- isolation between different keys/clients
- Redis restart/recovery behavior
- failure strategies under concurrent access

The distributed scenarios demonstrate the behavior implemented by the current architecture and test suite. They are validation of the tested cases, not a formal proof of every distributed-system property under every possible network, timing, or infrastructure failure.

## Testing Strategy

Testing is intentionally layered so algorithm correctness, infrastructure behavior, concurrency, and distributed coordination are exercised independently.

```text
Unit Tests
    |
    +-- Algorithm correctness
    +-- Models
    +-- Validation
    +-- Contracts
    +-- Time

Integration Tests
    |
    +-- Redis
    +-- ASP.NET Core
    +-- Failure behavior
    +-- Timeout
    +-- Cancellation

Concurrency Tests
    |
    +-- Fixed Window
    +-- Sliding Window
    +-- Token Bucket
    +-- GCRA

Distributed Tests
    |
    +-- Multi-instance shared state
    +-- Concurrent shared Redis state
    +-- Cost correctness
    +-- Expiration
    +-- Recovery
```

### Current local verification

The current verified local test run is:

```text
122 / 122 passed
0 failed
0 skipped
```

Breakdown:

| Test layer | Tests |
| --- | ---: |
| Unit | 73 |
| Integration | 35 |
| Concurrency | 4 |
| Distributed | 10 |
| **Total** | **122** |

These numbers represent the current local verification reported for the repository and are not a permanent guarantee that every future commit will produce the same result.

## Repository Structure

The current repository structure is:

```text
RateLimitEngine/
├── src/
│   ├── RateLimitEngine.Core/
│   ├── RateLimitEngine.Algorithms/
│   ├── RateLimitEngine.Redis/
│   └── RateLimitEngine.AspNetCore/
│
├── tests/
│   ├── RateLimitEngine.UnitTests/
│   ├── RateLimitEngine.IntegrationTests/
│   ├── RateLimitEngine.ConcurrencyTests/
│   ├── RateLimitEngine.DistributedTests/
│   └── RateLimitEngine.Testing/
│
├── benchmarks/
│   └── RateLimitEngine.Benchmarks/
│
├── samples/
│   └── RateLimitEngine.AspNetCoreDemo/
│
├── docs/
│   └── architecture/
│
├── RateLimitEngine.sln
├── LICENSE
└── README.md
```

The benchmark project currently consists of a basic console application skeleton rather than an implemented BenchmarkDotNet suite. The architecture documentation directory currently contains focused documents such as distributed-time semantics and rate-limit decision semantics; the full documentation roadmap remains incomplete.

## Getting Started

### Requirements

- .NET 8 SDK
- Redis only when the Redis backend is selected

The repository targets `net8.0`. .NET 8 should be treated as the project's current target framework; this README does not make any claim that it is the latest .NET LTS release.

### In-Memory Example

The core API can be consumed without ASP.NET Core:

```csharp
using RateLimitEngine.Algorithms;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Core.Time;

var factory = new InMemoryRateLimiterFactory(
    new SystemClock(),
    new RateLimiterOptions
    {
        TokenBucketCapacity = 100
    });

var limiter = factory.Create(RateLimitAlgorithm.FixedWindow);

var request = new RateLimitRequest(
    key: "client:123",
    cost: 1);

var policy = new RateLimitPolicy(
    permitLimit: 10,
    window: TimeSpan.FromMinutes(1));

var decision = await limiter.EvaluateAsync(request, policy);

Console.WriteLine($"Allowed: {decision.Allowed}");
Console.WriteLine($"Remaining: {decision.Remaining}");
```

The dependency-injection registration owns the backend-specific implementation and keeps in-memory algorithm instances alive for reuse.

### ASP.NET Core Example

For an in-memory ASP.NET Core application:

```csharp
using RateLimitEngine.Algorithms;
using RateLimitEngine.AspNetCore;
using RateLimitEngine.Core.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRateLimitEngineInMemory();

builder.Services.AddRateLimitEngine(options =>
{
    options.Backend = RateLimitBackend.InMemory;
    options.Algorithm = RateLimitAlgorithm.FixedWindow;
    options.PermitLimit = 100;
    options.Window = TimeSpan.FromMinutes(1);
    options.Cost = 1;
    options.FailureStrategy = RateLimitFailureStrategy.FailOpen;
});

var app = builder.Build();

app.UseRateLimitEngine();

app.MapGet("/", () => Results.Ok(new { status = "ok" }));

app.Run();
```

The extension registration validates the configured backend, algorithm, failure strategy, permit limit, window, and cost.

The default key resolver is registered by the ASP.NET Core integration. Applications that need another identity model can replace `IRateLimitKeyResolver` with their own implementation.

### Redis Example

When Redis is selected, the Redis backend must be registered before the configuration-driven factory can create a Redis limiter.

```csharp
using RateLimitEngine.AspNetCore;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Redis;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var connection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
builder.Services.AddSingleton<IConnectionMultiplexer>(connection);
builder.Services.AddRateLimitEngineRedis(connection.GetDatabase());

builder.Services.AddRateLimitEngine(options =>
{
    options.Backend = RateLimitBackend.Redis;
    options.Algorithm = RateLimitAlgorithm.FixedWindow;
    options.PermitLimit = 100;
    options.Window = TimeSpan.FromMinutes(1);
    options.Cost = 1;
    options.FailureStrategy = RateLimitFailureStrategy.FailClosed;
});

var app = builder.Build();

app.UseRateLimitEngine();
app.MapGet("/", () => Results.Ok(new { status = "ok" }));

app.Run();
```

For local development, a Redis endpoint such as `localhost:6379` can be used. No credentials are required by the example above; production deployments should supply whatever connection configuration their Redis environment requires.

## Configuration

`RateLimitOptions` currently supports:

| Option | Type | Default | Description |
| --- | --- | --- | --- |
| `Backend` | `RateLimitBackend` | `InMemory` | Selects `InMemory` or `Redis` |
| `Algorithm` | `RateLimitAlgorithm` | `FixedWindow` | Selects one of the four algorithms |
| `FailureStrategy` | `RateLimitFailureStrategy` | `FailOpen` | Determines behavior when rate-limit evaluation fails |
| `PermitLimit` | `int` | `100` | Positive permit limit |
| `Window` | `TimeSpan` | 1 minute | Positive policy window |
| `Cost` | `int` | `1` | Positive request cost |

Token Bucket capacity is configured separately through `TokenBucketOptions`. The built-in in-memory and Redis registrations default to a capacity of `100` when no custom `TokenBucketOptions` is supplied.

### Configuration from `IConfiguration`

The ASP.NET Core integration also supports configuration binding through `AddRateLimitEngine(IConfiguration, sectionName)`.

The default section is `RateLimit` and the supported keys are:

```json
{
  "RateLimit": {
    "Backend": "Redis",
    "Algorithm": "FixedWindow",
    "PermitLimit": 5,
    "WindowSeconds": 10,
    "Cost": 1,
    "FailureStrategy": "FailOpen"
  }
}
```

`WindowSeconds` is parsed into a `TimeSpan`. The configuration-driven registration validates positive integers/numbers and rejects unsupported enum values.

A Redis connection string is not part of `RateLimitOptions`; the Redis `IDatabase` is supplied to `AddRateLimitEngineRedis(...)`. The repository's ASP.NET Core sample uses the standard .NET connection-string section:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

## Rate Limit Response Semantics

### Allowed request

Depending on the generated decision, a successful request can receive:

```text
X-RateLimit-Limit
X-RateLimit-Remaining
X-RateLimit-Reset-After
```

`X-RateLimit-Reset-After` is only written when `ResetAfter` is present in the decision.

### Rejected request

A rejected decision results in:

```text
429 Too Many Requests
```

And, when the algorithm provides retry information:

```text
Retry-After
```

The exact header set therefore depends on the decision generated by the selected algorithm and store.

`RetryAfter` is part of rate-limiting semantics. It is not an automatic infrastructure retry facility.

## Distributed Deployment Model

A typical multi-instance deployment looks like:

```text
              Client Traffic
                   |
        +----------+----------+
        |                     |
        v                     v
   App Instance A       App Instance B
        |                     |
        +----------+----------+
                   |
                   v
             Shared Redis
                   |
          Atomic state updates
```

The important property is that application instances use the same logical rate-limit key against shared Redis state. The Redis backend then performs the algorithm-specific state transition server-side through Lua.

This allows the tested multi-instance scenarios to coordinate shared state rather than accidentally enforcing separate per-process limits.

The scope remains intentionally conservative: these tests validate the behavior of the implemented architecture under specific concurrent and recovery scenarios. They do not claim a universal formal distributed consistency guarantee.

## Current 20-Phase Roadmap

The project follows the following roadmap. Status labels describe the current development state and are intentionally more conservative than aspirational documentation.

## Phase 0 — Foundation

Subphases:

- Solution creation
- Project structure
- Initial repository setup
- Basic build

**Status:** ✅ COMPLETE

---

## Phase 1 — Core Domain

Subphases:

- Core models
- Store abstractions
- `IRateLimiter`
- Rate limit request
- Rate limit policy
- Rate limit decision
- State key
- Time abstraction

**Status:** ✅ COMPLETE

---

## Phase 2 — Fixed Window

Subphases:

- Algorithm implementation
- Store abstraction
- In-Memory store
- Correct counting
- Cost support
- Tests

**Status:** ✅ COMPLETE

---

## Phase 3 — Sliding Window

Subphases:

- Sliding window algorithm
- In-Memory state
- Expiration / pruning
- Cost support
- Redis implementation
- Atomic Lua
- Redis integration tests
- Timestamp collision handling

**Status:** ✅ COMPLETE

---

## Phase 4 — Token Bucket

Subphases:

- Token Bucket algorithm
- In-Memory store
- Refill calculation
- Capacity handling
- Cost support
- Redis implementation
- Atomic Lua
- Integration tests
- Limiter/store consistency

**Status:** ✅ COMPLETE

---

## Phase 5 — GCRA

Subphases:

- GCRA algorithm
- TAT state
- In-Memory store
- Redis TAT store
- Atomic Redis script
- Retry-After
- Remaining calculation
- Concurrency testing
- Integration testing

**Status:** ✅ COMPLETE

---

## Phase 6 — Unit / Contract Testing

Subphases:

- Algorithm unit tests
- Store tests
- Contract testing
- Edge cases
- Validation tests

**Status:** ✅ COMPLETE

---

## Phase 7 — Concurrency Testing

Subphases:

- In-Memory concurrency
- Redis concurrency
- Concurrent request cost
- Atomicity
- Independent connections

**Status:** ✅ COMPLETE

---

## Phase 8 — Time Abstraction

Subphases:

- `IClock`
- Deterministic test clock
- Time-based tests

**Status:** ✅ COMPLETE

---

## Phase 9 — State Lifecycle / Cleanup

Subphases:

- State lifetime review
- Cleanup
- Expiration
- Limiter lifetime correctness
- Factory lifetime correction

**Status:** ✅ COMPLETE

---

## Phase 10 — Algorithm Consistency / Cleanup

Subphases:

- Consistency between algorithms
- Cost propagation
- State reuse
- API cleanup
- Refactoring

**Status:** ✅ COMPLETE

---

## Phase 11 — Redis Distributed Backend

### 11.1 Redis infrastructure

- Script executor
- StackExchange.Redis
- Shared Redis state

### 11.2 Fixed Window Redis

- Redis Fixed Window
- Atomic operation

### 11.3 Token Bucket Redis

### 11.4 Sliding Window Redis

### 11.5 GCRA Redis

### 11.6 Integration tests

### 11.7 Atomicity tests

**Status:** ✅ COMPLETE

---

## Phase 12 — Factory / Backend Selection / DI

Subphases:

- 12.1 Algorithm enum
- 12.2 Backend enum
- 12.3 `IRateLimiterFactory`
- 12.4 In-Memory factory
- 12.5 Redis factory
- 12.6 DI registration
- 12.7 Backend provider
- 12.8 Configurable factory

**Status:** ✅ COMPLETE

---

## Phase 13 — ASP.NET Core Integration

Subphases:

- Step 1 — ASP.NET Core contracts
- Step 2 — Middleware
- Step 3 — Lifetime / state preservation
- Step 4 — HTTP validation
- Step 5 — Configuration-driven algorithm selection
- Step 6 — Token Bucket HTTP validation
- Step 7 — GCRA HTTP validation
- Step 8 — Automated ASP.NET Core integration tests
- Step 9 — Redis backend in ASP.NET Core Demo
- Step 10 — Backend + Algorithm configuration
- Step 11 — Configuration validation & failure handling

**Status:** ✅ COMPLETE

---

## Phase 14 — Distributed Multi-Instance Validation

### 14.1 Distributed test infrastructure

- `RateLimitEngine.DistributedTests`
- Redis Docker infrastructure
- Shared Redis test state

### 14.2 Cross-instance shared state

- Two independent instances
- Shared Redis
- Shared logical key
- Shared rate limit

### 14.3 Concurrent distributed access

- 1000 concurrent requests
- Independent Redis connections
- Shared global limit
- No per-process leakage

### 14.4 Cost correctness

- Weighted requests
- Distributed cost propagation

### 14.5 Algorithm coverage

- Fixed Window
- Sliding Window
- Token Bucket
- GCRA

### 14.6 Expiration / lifecycle

- Distributed expiration
- Recovery after expiration

### 14.7 Distributed isolation

- Key isolation
- Independent clients/connections

**Status:** ✅ COMPLETE

---

# Phase 15 — Resilience

### 15.1 Connection failure

✅ COMPLETE

### 15.2 Redis timeout

✅ COMPLETE

### 15.3 Redis unavailable

✅ COMPLETE

### 15.4 Lua script failure

❌ NOT IMPLEMENTED / NOT VERIFIED

### 15.5 Cancellation

✅ COMPLETE

### 15.6 Retry behavior

❌ NOT IMPLEMENTED

### 15.7 FailOpen

✅ COMPLETE

### 15.8 FailClosed

✅ COMPLETE

### 15.9 Recovery after Redis returns

✅ COMPLETE

**Overall:** 🟡 PARTIAL — 7 / 9 subphases complete

---

# Phase 16 — Observability

Subphases:

- Metrics
- Logging
- Diagnostics
- Latency measurement
- Accepted request metrics
- Rejected request metrics
- Backend metrics
- Algorithm metrics
- Error metrics
- Optional OpenTelemetry integration

**Status:** ⏳ NOT STARTED / NOT COMPLETE

---

# Phase 17 — Benchmarks

Subphases:

- Benchmark project
- BenchmarkDotNet integration
- Fixed Window benchmarks
- Sliding Window benchmarks
- Token Bucket benchmarks
- GCRA benchmarks
- In-Memory benchmarks
- Redis benchmarks
- Throughput
- Latency
- Allocations
- Contention
- Concurrency benchmarks
- Benchmark result documentation

**Status:** 🟡 SKELETON / NOT COMPLETE

Important:

A benchmark project exists, but actual benchmarking is not yet implemented. The current program is a basic console application and does not provide BenchmarkDotNet measurements or published performance results.

---

# Phase 18 — Documentation

Subphases:

- Architecture documentation
- Algorithm documentation
- Algorithm comparison
- Distributed semantics
- Redis architecture
- Lua atomicity explanation
- Configuration documentation
- ASP.NET Core usage
- Failure behavior
- Backend selection
- Algorithm selection
- Cost semantics
- Examples
- Troubleshooting
- Production deployment guidance

**Status:** 🟡 PARTIAL

Existing architecture documentation should be acknowledged, but the full documentation phase is not complete.

---

# Phase 19 — Packaging / NuGet / CI

Subphases:

- Package metadata
- NuGet package preparation
- Core package
- Algorithms package
- Redis package
- ASP.NET Core package
- `dotnet pack`
- `.nupkg` inspection
- Semantic versioning
- GitHub Actions
- CI build
- CI test
- Package validation
- NuGet trusted publishing / OIDC
- Final branding / package naming

**Status:** ⏳ NOT COMPLETE

No NuGet publication is claimed. No completed release automation or production package pipeline is claimed.

---

# Phase 20 — Production Readiness

Subphases:

- Full test suite
- Stress tests
- Failure tests
- Multi-instance deployment validation
- Package validation
- XML documentation
- Public API review
- Backward compatibility review
- README finalization
- Example applications
- Docker validation
- CI validation
- Final release checklist
- v1.0.0 preparation
- NuGet release

**Status:** ⏳ NOT COMPLETE

---

## Roadmap Status Table

| Phase | Title | Status |
| --- | --- | --- |
| 0 | Foundation | ✅ Complete |
| 1 | Core Domain | ✅ Complete |
| 2 | Fixed Window | ✅ Complete |
| 3 | Sliding Window | ✅ Complete |
| 4 | Token Bucket | ✅ Complete |
| 5 | GCRA | ✅ Complete |
| 6 | Unit / Contract Testing | ✅ Complete |
| 7 | Concurrency Testing | ✅ Complete |
| 8 | Time Abstraction | ✅ Complete |
| 9 | State Lifecycle / Cleanup | ✅ Complete |
| 10 | Algorithm Consistency / Cleanup | ✅ Complete |
| 11 | Redis Distributed Backend | ✅ Complete |
| 12 | Factory / Backend Selection / DI | ✅ Complete |
| 13 | ASP.NET Core Integration | ✅ Complete |
| 14 | Distributed Multi-Instance Validation | ✅ Complete |
| 15 | Resilience | 🟡 Partial |
| 16 | Observability | ⏳ Not Started |
| 17 | Benchmarks | 🟡 Incomplete |
| 18 | Documentation | 🟡 Partial |
| 19 | Packaging / NuGet / CI | ⏳ Not Complete |
| 20 | Production Readiness | ⏳ Not Complete |

## MVP Boundary

### MVP includes the core usable rate-limiting engine through the distributed/resilience foundation.

For this project, the practical MVP boundary is the combination of:

- the core algorithm engine
- four supported algorithms
- in-memory state
- Redis distributed state
- algorithm-specific state abstractions
- ASP.NET Core integration
- multi-instance distributed validation
- essential infrastructure failure semantics
- FailOpen and FailClosed behavior
- deterministic time and concurrency testing

This is the point at which the project has the core distributed product capability it was designed to build.

The following are maturity work beyond that core capability:

- observability
- comprehensive benchmarking
- complete documentation
- package and release engineering
- CI/CD and validation automation
- final production-readiness review
- v1.0 release preparation

The current project has reached the core distributed capability, but it is still in engineering hardening and maturity phases.

## Current Position

```text
Phase 0
   ↓
Phase 1
   ↓
...
   ↓
Phase 14 ✅
   ↓
Phase 15 🟡 CURRENT
   ├── 15.1 ✅
   ├── 15.2 ✅
   ├── 15.3 ✅
   ├── 15.4 ❌
   ├── 15.5 ✅
   ├── 15.6 ❌
   ├── 15.7 ✅
   ├── 15.8 ✅
   └── 15.9 ✅
   ↓
Phase 16 ⏳
   ↓
Phase 17
   ↓
Phase 18
   ↓
Phase 19
   ↓
Phase 20
```

**Current development position: Phase 15 — Resilience.**

Phase 15 is partially complete. Seven of its nine subphases are complete, while 15.4 and 15.6 remain open.

## What is Next?

The immediate engineering sequence is:

1. Complete **15.4 — Lua script failure scenarios**.
2. Decide and implement **15.6 — infrastructure retry behavior**, if justified by the project's design.
3. Complete **Phase 15 — Resilience**.
4. Move into **Phase 16 — Observability**.
5. Continue through benchmarking, documentation, packaging/CI, and final production-readiness work.

## Design Philosophy

RateLimitEngine is intentionally built around a small set of engineering priorities:

- **Correctness over feature count:** a smaller set of well-defined algorithms is preferable to a larger set of loosely verified features.
- **Explicit abstractions:** algorithm state, backend selection, time, request cost, and failure strategy are represented as explicit contracts or models.
- **Deterministic testing:** time-dependent behavior should be reproducible rather than dependent on timing luck.
- **Measurable behavior:** future performance claims should come from reproducible measurements rather than assumptions.
- **Small cohesive components:** algorithms, stores, middleware, and factories have separate responsibilities.
- **Dependency inversion:** core algorithm code depends on abstractions rather than concrete infrastructure.
- **Infrastructure failure as a first-class concern:** Redis failure semantics are defined explicitly through FailOpen and FailClosed.
- **No unsupported performance claims:** the project does not claim to be the fastest implementation without benchmark evidence.
- **No fabricated benchmarks:** the current benchmark project is a skeleton and no benchmark results are presented as facts.
- **No premature production-readiness claims:** the project remains in active development until the roadmap's maturity phases are completed.

## Limitations / Non-Goals

The current project boundaries include:

- **In-Memory state is process-local.** It is not a shared distributed state mechanism.
- **Redis is required for shared distributed state.** Multi-instance coordination depends on the Redis backend.
- **Distributed guarantees are bounded by the tested architecture and Redis behavior.** The test suite validates specific scenarios rather than every theoretical distributed failure mode.
- **Full observability is not yet implemented.** Metrics, diagnostics, and optional OpenTelemetry integration remain roadmap work.
- **Comprehensive benchmarking is not yet complete.** The benchmark project is currently a skeleton without BenchmarkDotNet measurements.
- **Packaging, CI, and release engineering are not yet complete.** The project is not claiming NuGet publication or a finished release pipeline.
- **The project is not yet v1.0 production-ready.** Final hardening, documentation, packaging, validation, and release work remain.

These are current scope boundaries rather than statements that the core architecture is unusable.

## Architectural Documentation

The repository currently contains focused architecture documentation under:

```text
docs/architecture/
├── distributed-time-semantics.md
└── rate-limit-decision-semantics.md
```

This documentation is useful for understanding time semantics and decision behavior, while the broader documentation roadmap in Phase 18 remains incomplete.

## Benchmarking Status

A benchmark project exists at:

```text
benchmarks/RateLimitEngine.Benchmarks/
```

At the current development state, its `Program.cs` is only a basic console application skeleton. There is no completed BenchmarkDotNet suite and therefore no repository-backed throughput, latency percentile, allocation, contention, hardware, or Redis performance result to report.

Performance work belongs to Phase 17.

## License

RateLimitEngine is licensed under the **MIT License**.

See [`LICENSE`](LICENSE) for the full license text.

## Contribution

Contributions are welcome, especially improvements that strengthen algorithm correctness, deterministic testing, concurrency behavior, distributed validation, failure semantics, or documentation.

A good contribution should:

- keep algorithm and infrastructure concerns separated
- include tests for behavior changes
- avoid introducing unsupported distributed or performance claims
- preserve the repository's explicit failure semantics
- keep public API changes intentional and reviewable

Before opening a pull request, run the relevant test projects and verify that the change does not weaken existing concurrency or distributed scenarios.

## Scope

RateLimitEngine is a rate-limiting library, not a full API gateway, SaaS rate-limiting platform, authentication system, authorization system, Kubernetes operator, or multi-language SDK ecosystem.

The project intentionally stays focused on rate-limiting correctness, storage coordination, ASP.NET Core integration, resilience, and testability.

