# RateLimitEngine

> A production-oriented, extensible, distributed rate-limiting library for .NET and ASP.NET Core.

RateLimitEngine is a .NET 8 rate-limiting library built around correctness, concurrency safety, deterministic behavior, distributed state coordination, explicit failure semantics, and testability.

It provides four rate-limiting algorithms, in-memory and Redis-backed state, ASP.NET Core integration, weighted requests, customizable client identification, observability, and explicit behavior when the rate-limiting backend becomes unavailable.

The library is designed to keep the **rate-limiting algorithm**, **state storage**, and **HTTP integration** as separate concerns.

---

## Installation

Install only the packages required by your application.

For ASP.NET Core applications:

```powershell
dotnet add package RateLimitEngine.AspNetCore
dotnet add package RateLimitEngine.Algorithms
dotnet add package RateLimitEngine.Redis
```

For applications that only need the in-memory rate-limiting engine:

```powershell
dotnet add package RateLimitEngine.Algorithms
```

For applications that provide their own integration and need only the Redis backend:

```powershell
dotnet add package RateLimitEngine.Redis
```

`RateLimitEngine.Core` is installed automatically as a dependency where required.

The current release is `1.0.0`.

---

# What Problem Does RateLimitEngine Solve?

Rate limiting is often described as a simple rule:

> Allow a request if the client has not exceeded a configured limit.

In practice, a reliable rate limiter has to answer much harder questions:

* What happens when many requests arrive concurrently?
* How is state updated without race conditions?
* What happens at time-window boundaries?
* How should requests with different costs be handled?
* How can several application instances share the same limit?
* What happens when Redis becomes unavailable?
* Should an application fail open or fail closed?
* How can time-dependent behavior be tested deterministically?

RateLimitEngine treats these problems as part of the core design rather than as implementation details hidden behind a simple counter.

---

# Core Concepts

RateLimitEngine is built around a small set of concepts.

## Request

A `RateLimitRequest` describes the operation being limited.

It contains:

* a logical client key
* a request cost

For example:

```csharp
var request = new RateLimitRequest(
    key: "tenant:42",
    cost: 1);
```

The key does not have to be an IP address. It can represent any logical identity used by the application.

Examples include:

```text
client:192.0.2.10
user:12345
tenant:42
api-key:abc123
service:checkout
```

---

## Policy

A `RateLimitPolicy` defines the limit:

```csharp
var policy = new RateLimitPolicy(
    permitLimit: 100,
    window: TimeSpan.FromMinutes(1));
```

The policy contains:

* the permit limit
* the time window

The policy is deliberately independent from HTTP and storage infrastructure.

---

## Decision

Evaluation returns a `RateLimitDecision`.

A decision contains:

* `Allowed`
* `Limit`
* `Remaining`
* `ResetAfter`
* `RetryAfter`

Example:

```csharp
var decision = await limiter.EvaluateAsync(
    request,
    policy);

if (decision.Allowed)
{
    // continue
}
else
{
    // reject
}
```

`RetryAfter` is a rate-limit signal telling the caller when the request may meaningfully be retried.

It is **not** an instruction to retry a failed Redis operation.

---

# The Main Abstraction

The central runtime abstraction is `IRateLimiter`:

```csharp
ValueTask<RateLimitDecision> EvaluateAsync(
    RateLimitRequest request,
    RateLimitPolicy policy,
    CancellationToken cancellationToken = default);
```

The application does not need to know how the selected algorithm stores or updates its state.

This separation makes it possible to change:

* algorithm
* storage backend
* HTTP integration
* client-key strategy

without changing the conceptual rate-limiting operation itself.

---

# Supported Algorithms

RateLimitEngine currently supports four algorithms.

| Algorithm      | Best suited for                                          |
| -------------- | -------------------------------------------------------- |
| Fixed Window   | Simple quotas and predictable fixed intervals            |
| Sliding Window | Smoother behavior around window boundaries               |
| Token Bucket   | Burst-friendly traffic shaping                           |
| GCRA           | Controlled burst tolerance with precise timing semantics |

All four algorithms support weighted requests.

---

# Fixed Window

Fixed Window divides time into discrete intervals.

For example:

```text
100 requests / minute
```

A request consumes one or more permits depending on its cost.

### Example

```text
12:00:00 ───────────────── 12:01:00
           100 permits

12:01:00 ───────────────── 12:02:00
           100 permits
```

A major characteristic of Fixed Window is that requests near the boundary of two windows can benefit from the transition between windows.

### Good use cases

* simple API quotas
* administrative APIs
* coarse request limits
* applications where strict boundary smoothing is not required

---

# Sliding Window

Sliding Window evaluates usage over a continuously moving interval.

Instead of asking:

> How many requests occurred in this calendar-style window?

it asks:

> How much traffic occurred during the most recent interval?

This reduces the sharp boundary behavior of Fixed Window.

The implementation tracks timestamped weighted entries and removes entries that have expired from the active window.

### Good use cases

* public APIs
* user-facing APIs
* APIs where smoother behavior around boundaries is desirable

---

# Token Bucket

Token Bucket models a bucket of available tokens.

Tokens are continuously replenished according to the policy:

```text
refillRate = PermitLimit / Window.TotalSeconds
```

A request consumes tokens according to its cost.

The bucket has a configured capacity.

Example:

```text
capacity = 100
limit = 100
window = 1 minute
```

The application can therefore tolerate bursts up to the configured capacity while still enforcing a long-term refill rate.

### Good use cases

* APIs with legitimate bursts
* traffic shaping
* services with uneven request arrival patterns
* workloads with different request costs

Token Bucket capacity is configured with `TokenBucketOptions`.

The built-in registration uses a default capacity of `100` when no custom capacity is supplied.

---

# GCRA

GCRA (Generic Cell Rate Algorithm) models traffic through a theoretical arrival time (TAT).

For a permit limit `L` and window `W`, RateLimitEngine derives:

```text
interval       = W / L
burstTolerance = interval * (L - 1)
```

GCRA is useful when precise timing and controlled burst tolerance matter.

The implementation also exposes meaningful `RetryAfter` and `Remaining` information through the resulting `RateLimitDecision`.

### Good use cases

* precise API traffic control
* systems where timing semantics matter
* workloads that need controlled burst behavior without a traditional token counter

---

# Choosing an Algorithm

A practical starting point:

| Requirement                    | Recommended algorithm |
| ------------------------------ | --------------------- |
| Keep implementation simple     | Fixed Window          |
| Reduce boundary bursts         | Sliding Window        |
| Allow explicit bursts          | Token Bucket          |
| Precise interval-based control | GCRA                  |

This is a behavioral guideline, not a benchmark ranking.

The right choice depends on the traffic pattern and the semantics your application needs.

---

# Storage Backends

The algorithm and the state backend are separate concepts.

RateLimitEngine provides two backends.

## In-Memory

The in-memory backend keeps state inside the application process.

Characteristics:

* no external infrastructure
* very low operational complexity
* suitable for single-instance applications
* thread-safe state transitions
* state is local to the process
* state is lost when the process restarts
* state is not automatically shared between application instances

Use it when each application instance can legitimately have its own rate-limit state.

Do **not** use it when several application instances must enforce one shared global limit.

---

## Redis

The Redis backend is intended for distributed deployments.

Multiple application instances can share rate-limit state:

```text
             Application
             Instances
          A      B      C
           \     |     /
            \    |    /
             \   |   /
              Shared Redis
```

The Redis implementation uses:

* StackExchange.Redis
* algorithm-specific Redis stores
* Redis server time
* Lua scripts
* atomic server-side state transitions
* expiration for lifecycle management

Redis is therefore the shared state coordination layer rather than the implementation of the rate-limiting algorithms themselves.

---

# In-Memory Example

A framework-agnostic application can register the in-memory backend through dependency injection:

```csharp
using Microsoft.Extensions.DependencyInjection;
using RateLimitEngine.Algorithms;
using RateLimitEngine.Core.Abstractions;
using RateLimitEngine.Core.Models;

var services = new ServiceCollection();

services.AddRateLimitEngineInMemory();

using var provider = services.BuildServiceProvider();

var factory = provider.GetRequiredService<IRateLimiterFactory>();

var limiter = factory.Create(
    RateLimitAlgorithm.FixedWindow);

var request = new RateLimitRequest(
    key: "client:123",
    cost: 1);

var policy = new RateLimitPolicy(
    permitLimit: 10,
    window: TimeSpan.FromMinutes(1));

var decision = await limiter.EvaluateAsync(
    request,
    policy);

Console.WriteLine(
    $"Allowed: {decision.Allowed}");

Console.WriteLine(
    $"Remaining: {decision.Remaining}");
```

The dependency-injection registration manages the concrete backend implementation and reuses the algorithm instances so that in-memory state is preserved between requests.

---

# ASP.NET Core

The ASP.NET Core package provides middleware around the core engine.

A minimal setup looks like:

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

    options.FailureStrategy =
        RateLimitFailureStrategy.FailOpen;
});

var app = builder.Build();

app.UseRateLimitEngine();

app.MapGet("/", () =>
    Results.Ok(new { status = "ok" }));

app.Run();
```

The middleware:

1. resolves the client key
2. creates a rate-limit request
3. creates the configured policy
4. selects the backend and algorithm
5. evaluates the request
6. writes applicable response headers
7. either continues the pipeline or returns a rate-limit/failure response

---

# Redis + ASP.NET Core

For a distributed ASP.NET Core application:

```csharp
using RateLimitEngine.AspNetCore;
using RateLimitEngine.Core.Models;
using RateLimitEngine.Redis;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var connection =
    await ConnectionMultiplexer.ConnectAsync(
        "localhost:6379");

builder.Services.AddSingleton<
    IConnectionMultiplexer>(connection);

builder.Services.AddRateLimitEngineRedis(
    connection.GetDatabase());

builder.Services.AddRateLimitEngine(options =>
{
    options.Backend = RateLimitBackend.Redis;
    options.Algorithm = RateLimitAlgorithm.FixedWindow;

    options.PermitLimit = 100;
    options.Window = TimeSpan.FromMinutes(1);
    options.Cost = 1;

    options.FailureStrategy =
        RateLimitFailureStrategy.FailClosed;
});

var app = builder.Build();

app.UseRateLimitEngine();

app.MapGet("/", () =>
    Results.Ok(new { status = "ok" }));

app.Run();
```

All application instances can point to the same Redis deployment and therefore share the rate-limit state for the same logical keys.

---

# Client Identity and Key Resolution

A rate limiter needs a way to identify the client.

RateLimitEngine does not force the application to use a particular identity model.

ASP.NET Core provides:

```csharp
IRateLimitKeyResolver
```

The built-in resolver is:

```csharp
RemoteIpRateLimitKeyResolver
```

By default, the middleware can therefore derive a key from the remote IP address.

Applications can replace it with their own implementation.

For example, a custom resolver could use:

```text
user:{userId}
```

or:

```text
tenant:{tenantId}
```

or:

```text
api-key:{key}
```

This makes the same rate-limiting engine applicable to multiple identity models.

---

# Per-IP Rate Limiting

A simple ASP.NET Core configuration can use the default remote-IP resolver:

```csharp
builder.Services.AddRateLimitEngine(
    options =>
    {
        options.Backend =
            RateLimitBackend.InMemory;

        options.Algorithm =
            RateLimitAlgorithm.FixedWindow;

        options.PermitLimit = 100;
        options.Window =
            TimeSpan.FromMinutes(1);
    });
```

This is appropriate when the network-level client identity is the desired unit of rate limiting.

For distributed deployments, use Redis so all application instances share the same IP-based state.

---

# Per-User Rate Limiting

Applications with authentication can implement `IRateLimitKeyResolver` using the authenticated identity.

Conceptually:

```csharp
public sealed class UserRateLimitKeyResolver
    : IRateLimitKeyResolver
{
    public string Resolve(HttpContext context)
    {
        var userId =
            context.User.FindFirst("sub")?.Value;

        return $"user:{userId}";
    }
}
```

Register the custom resolver instead of the default one.

This allows:

```text
User A → 100 requests/minute
User B → 100 requests/minute
User C → 100 requests/minute
```

instead of sharing one global counter.

---

# Per-Tenant Rate Limiting

The same pattern works for multi-tenant applications.

A resolver can derive:

```text
tenant:acme
tenant:contoso
tenant:example
```

Each tenant can then have independent rate-limit state.

This is especially useful when Redis is used for shared state across several application instances.

---

# API-Key Rate Limiting

For APIs that authenticate through API keys, the key itself can become the logical rate-limit identity.

For example:

```text
api-key:customer-001
api-key:customer-002
```

The rate limiter does not need to know how the key was authenticated.

It only receives the logical identity from `IRateLimitKeyResolver`.

---

# Weighted Requests

Not every request should necessarily cost one permit.

RateLimitEngine supports request costs:

```csharp
var cheapRequest =
    new RateLimitRequest(
        "client:123",
        cost: 1);

var expensiveRequest =
    new RateLimitRequest(
        "client:123",
        cost: 10);
```

This makes it possible to model APIs where operations have different resource costs.

For example:

```text
GET /products          cost = 1
GET /reports           cost = 5
POST /export           cost = 10
```

A single rate-limit policy can therefore represent resource-weighted usage rather than only request count.

---

# HTTP Response Semantics

When a request is rejected because the configured rate limit has been reached, the middleware returns:

```text
429 Too Many Requests
```

Applicable rate-limit metadata may include:

```text
X-RateLimit-Limit
X-RateLimit-Remaining
X-RateLimit-Reset-After
Retry-After
```

`X-RateLimit-Reset` is not currently emitted by the middleware.

The exact response metadata depends on the decision produced by the selected algorithm.

---

# Failure Handling

Distributed infrastructure can fail.

RateLimitEngine makes this behavior explicit through:

```csharp
RateLimitFailureStrategy.FailOpen
```

and:

```csharp
RateLimitFailureStrategy.FailClosed
```

## FailOpen

When rate-limit infrastructure cannot evaluate the request:

```text
Request continues
```

This prioritizes availability.

It is useful when temporarily losing rate-limit enforcement is preferable to rejecting otherwise valid application traffic.

## FailClosed

When rate-limit infrastructure cannot evaluate the request:

```text
503 Service Unavailable
```

This prioritizes enforcement.

It is useful when allowing traffic without a functioning rate limiter is considered unacceptable.

---

# 429 vs 503

The distinction is intentional.

| Situation                                     | Result                    |
| --------------------------------------------- | ------------------------- |
| Valid evaluation exceeds the configured limit | `429 Too Many Requests`   |
| Backend failure with `FailOpen`               | Request continues         |
| Backend failure with `FailClosed`             | `503 Service Unavailable` |

A `429` means the rate limit was evaluated successfully and the request was rejected.

A `503` means the application could not obtain a valid rate-limit evaluation under a fail-closed policy.

---

# Retry Semantics

`RetryAfter` on `RateLimitDecision` describes when a rejected request can meaningfully retry the **rate limit**.

It is not the same thing as infrastructure retry.

For example:

```text
RetryAfter = 2 seconds
```

means that the client should consider retrying the rate-limited operation after approximately that interval.

It does not mean that RateLimitEngine is retrying a failed Redis command.

---

# Redis Atomicity

The Redis backend uses Lua scripts when an operation requires a check and state mutation to happen together.

A typical transition may include:

```text
Read state
   ↓
Get Redis server time
   ↓
Evaluate request
   ↓
Apply cost
   ↓
Update state
   ↓
Calculate remaining/retry metadata
   ↓
Apply expiration
   ↓
Return result
```

The exact state model depends on the algorithm.

### Fixed Window

Updates the window counter atomically.

### Sliding Window

Prunes expired entries, evaluates consumption, adds the new entry, and updates aggregate state.

### Token Bucket

Replenishes tokens, evaluates cost, persists the resulting token state, and calculates recovery information.

### GCRA

Reads TAT, evaluates admissibility, advances TAT when accepted, and calculates remaining/retry information.

---

# Distributed Deployment

A typical distributed deployment is:

```text
                    Load Balancer
                         |
            +------------+------------+
            |            |            |
            v            v            v
        API #1        API #2        API #3
            \            |            /
             \           |           /
              +----------+----------+
                         |
                         v
                    Redis Cluster
```

All instances must use the same logical key namespace when they are expected to share a rate limit.

For example:

```text
API #1 → tenant:42
API #2 → tenant:42
API #3 → tenant:42
```

with Redis as the shared backend results in one shared logical rate-limit state.

---

# Time

Time is abstracted through:

```csharp
IClock
```

This provides:

```csharp
DateTimeOffset UtcNow { get; }

long GetTimestamp();

TimeSpan GetElapsedTime(
    long startingTimestamp);
```

The production implementation is based on .NET `TimeProvider`.

The test infrastructure uses deterministic clocks so algorithms can be tested without depending on arbitrary real-time delays.

Redis-backed algorithms use Redis server time for the relevant Lua state transitions.

---

# Observability

RateLimitEngine uses standard .NET diagnostics primitives rather than requiring a specific monitoring vendor.

The observability layer includes:

* `Meter`
* `ActivitySource`
* allowed request measurements
* rejected request measurements
* evaluation failure measurements
* Redis retry measurements
* evaluation duration measurements

The meter name is:

```text
RateLimitEngine
```

Current metric instruments include:

```text
ratelimit.requests.allowed
ratelimit.requests.rejected
ratelimit.evaluation.failures
ratelimit.redis.retry.attempts
ratelimit.evaluation.duration
```

The ActivitySource name is also:

```text
RateLimitEngine
```

This allows applications to connect the library to their existing metrics and tracing infrastructure.

---

# Configuration

The ASP.NET Core configuration model exposes:

| Option            | Type                       | Default       |
| ----------------- | -------------------------- | ------------- |
| `Backend`         | `RateLimitBackend`         | `InMemory`    |
| `Algorithm`       | `RateLimitAlgorithm`       | `FixedWindow` |
| `FailureStrategy` | `RateLimitFailureStrategy` | `FailOpen`    |
| `PermitLimit`     | `int`                      | `100`         |
| `Window`          | `TimeSpan`                 | 1 minute      |
| `Cost`            | `int`                      | `1`           |

Token Bucket capacity is configured separately through `TokenBucketOptions`.

---

# Configuration from appsettings.json

The ASP.NET Core integration supports configuration through `IConfiguration`.

Example:

```json
{
  "RateLimit": {
    "Backend": "Redis",
    "Algorithm": "SlidingWindow",
    "PermitLimit": 100,
    "WindowSeconds": 60,
    "Cost": 1,
    "FailureStrategy": "FailClosed"
  }
}
```

Then:

```csharp
builder.Services.AddRateLimitEngine(
    builder.Configuration);
```

The configuration API validates:

* supported backend values
* supported algorithm values
* supported failure strategies
* positive permit limits
* positive windows
* positive request costs

---

# Example: Token Bucket for Burst Traffic

Suppose an API normally processes roughly 100 operations per minute but should allow short bursts.

Token Bucket can model:

```text
permit limit = 100
window       = 1 minute
capacity     = 50
```

The application can temporarily consume the available burst capacity while tokens are continuously replenished according to the configured rate.

This can be preferable to Fixed Window when traffic arrives in short bursts.

---

# Example: Tenant-Based Distributed Limiting

A SaaS API may have:

```text
100 requests / minute / tenant
```

The application can:

1. derive `tenant:{id}` as the logical key
2. use `RateLimitBackend.Redis`
3. deploy several ASP.NET Core instances
4. let Redis provide the shared state

The result is a logical rate limit associated with the tenant rather than with a particular application process.

---

# Example: Expensive Endpoints

An application may assign:

```text
GET /search       → cost 1
GET /analytics    → cost 5
POST /export      → cost 10
```

A request model can represent the cost directly:

```csharp
var request = new RateLimitRequest(
    key: $"user:{userId}",
    cost: 10);
```

The selected algorithm then evaluates the weighted request against the policy.

---

# Package Structure

RateLimitEngine is split into focused packages.

| Package                      | Purpose                                        |
| ---------------------------- | ---------------------------------------------- |
| `RateLimitEngine.Core`       | Core abstractions, models, time, diagnostics   |
| `RateLimitEngine.Algorithms` | Rate-limiting algorithms and in-memory backend |
| `RateLimitEngine.Redis`      | Redis-backed state implementations             |
| `RateLimitEngine.AspNetCore` | ASP.NET Core middleware and configuration      |

The packages are versioned together for the current `1.0.0` release.

---

# Architecture at a Glance

```text
                    Application
                         |
                         v
               ASP.NET Core Integration
                         |
                         v
                IRateLimiterFactory
                         |
                         v
                 Selected Algorithm
                         |
                         v
              Algorithm Store Contract
                    /            \
                   /              \
                  v                v
             In-Memory           Redis
```

The key architectural boundary is:

```text
Algorithm != Storage != HTTP
```

Algorithms define how rate limits behave.

Stores define how state is persisted or coordinated.

ASP.NET Core defines how HTTP requests are integrated with the engine.

---

# Testing and Reliability

RateLimitEngine uses several testing layers.

## Unit tests

Used for:

* algorithm behavior
* models
* validation
* state semantics
* time-dependent behavior

## Integration tests

Used for:

* Redis integration
* ASP.NET Core integration
* timeout behavior
* failure behavior
* cancellation
* recovery

## Concurrency tests

The concurrency suite exercises all four algorithms with high-concurrency workloads.

The current scenarios use 10,000 concurrent evaluations and verify that configured limits are not exceeded.

## Distributed tests

The distributed suite validates scenarios involving:

* multiple application instances
* multiple Redis connections
* shared logical keys
* shared rate limits
* weighted requests
* expiration
* recovery
* all four algorithms

The current local verification is:

```text
136 tests passed
0 failed
0 skipped
```

This is evidence of the current tested behavior, not a formal proof of correctness under every possible production failure mode.

---

# Sample Application

The repository contains an ASP.NET Core demonstration application:

```text
samples/RateLimitEngine.AspNetCoreDemo/
```

The sample demonstrates:

* ASP.NET Core middleware integration
* backend selection
* algorithm selection
* configuration
* Swagger/OpenAPI integration

---

# Production Guidance

## Choose In-Memory when

* the application is single-instance
* process-local state is acceptable
* no shared distributed limit is required

## Choose Redis when

* multiple application instances must share one limit
* a global logical rate limit is required
* Redis is already part of the infrastructure

## Choose FailOpen when

availability is more important than temporarily enforcing the rate limit during a backend outage.

## Choose FailClosed when

rate-limit enforcement must remain mandatory even when the backend is unavailable.

## Choose the algorithm based on behavior

Do not choose an algorithm only because it is popular.

Consider:

* burst requirements
* boundary behavior
* timing precision
* state characteristics
* request cost
* operational constraints

---

# Limitations

RateLimitEngine deliberately has a focused scope.

It is not:

* an API gateway
* an authentication system
* an authorization system
* a SaaS rate-limiting service
* a Kubernetes operator
* a universal traffic-management platform

Distributed behavior is bounded by the tested architecture and Redis execution model.

The library does not claim formal distributed consistency guarantees beyond what the implementation and tests establish.

---

# License

RateLimitEngine is licensed under the **MIT License**.

See [`LICENSE`](LICENSE) for the full license text.

---

# Repository

GitHub:

`https://github.com/peymanpro/RateLimitEngine`

The repository contains the source projects, tests, examples, benchmark infrastructure, architecture documentation, and CI configuration.
