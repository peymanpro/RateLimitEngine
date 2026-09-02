# Architecture Overview

RateLimitEngine is a .NET 8 rate-limiting library focused on correctness, concurrency safety, distributed consistency, explicit failure behavior, and measurable performance.

## Architecture

The system is divided into four runtime layers:

- Core: requests, policies, decisions, abstractions, clocks, and IRateLimiter.
- Algorithms: Fixed Window, Token Bucket, Sliding Window, and GCRA.
- Redis: distributed stores using Redis Lua scripts and Redis server time.
- ASP.NET Core: middleware, dependency injection, HTTP mapping, logging, and failure strategies.

## Evaluation Flow

Application request
-> RateLimitRequest + RateLimitPolicy
-> IRateLimiter
-> Algorithm
-> Algorithm-specific store
-> RateLimitDecision
-> Observability
-> ASP.NET Core HTTP response

## State Ownership

Each algorithm has its own store abstraction:

- IFixedWindowStore
- ITokenBucketStore
- ISlidingWindowStore
- IGcraStore

Algorithm-specific stores are intentional because each algorithm has different state-transition and timing semantics.

## Time Model

In-memory algorithms use local clock abstractions appropriate to their timing requirements.

Redis-backed algorithms obtain authoritative time inside the Redis Lua operation using Redis TIME. Application-local clocks are therefore not authoritative for distributed decisions.

## Failure Model

Redis connection failures can be retried through the Redis retry executor.

ASP.NET Core middleware supports:

- FailOpen: continue the request when rate-limit infrastructure fails.
- FailClosed: return HTTP 503 when evaluation fails.

OperationCanceledException is propagated rather than converted into a failure-strategy response.

## Observability

Instrumentation uses System.Diagnostics.Metrics and ActivitySource without requiring OpenTelemetry as a hard dependency.

Metrics include allowed requests, rejected requests, evaluation failures, Redis retry attempts, and evaluation duration.

Activities include algorithm/backend context and error information.

## Performance Philosophy

Benchmarks are measurement tools rather than universal production-throughput claims.

In-memory benchmarks compare algorithm and instrumentation overhead. Redis benchmarks are strongly influenced by Redis and network round trips.
