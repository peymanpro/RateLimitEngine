# Redis Architecture

RateLimitEngine Redis support provides distributed rate limiting through Redis-backed algorithm-specific stores.

## Atomic Evaluation

Each Redis store executes its state transition through a Lua script.

The script performs the required read, decision, state mutation, and expiration operations as one Redis-side atomic operation.

This prevents concurrent application instances from observing and updating the same rate-limit state through separate non-atomic commands.

## Authoritative Time

Redis scripts obtain the current time using Redis TIME.

The application does not supply DateTimeOffset values as authoritative time to distributed stores.

This allows time acquisition and state transition to occur within the same Redis-side operation and avoids relying on independently running application clocks.

## Redis State

Each algorithm uses a dedicated key namespace:

- ratelimit:fixed-window:{key}
- ratelimit:token-bucket:{key}
- ratelimit:sliding-window:{key}
- ratelimit:gcra:{key}

Individual algorithms may use additional keys beneath their namespace for algorithm-specific state.

## Fixed Window

The Fixed Window script derives a window start from Redis server time and appends it to the base key.

It reads the current counter, checks the requested cost against the limit, increments accepted requests, and sets the window expiration.

## Token Bucket

The Token Bucket script stores token quantity and the timestamp of the last update.

It calculates elapsed time using Redis server time, refills tokens up to capacity, evaluates the request cost, and updates expiration state.

## Sliding Window

The Sliding Window script stores request entries in a Redis sorted set and maintains a total-consumption value.

Expired entries are removed before evaluation. Accepted entries are added with the current Redis timestamp and associated cost.

## GCRA

The GCRA script stores theoretical arrival time (TAT). It calculates the earliest allowed arrival using burst tolerance and updates TAT only for accepted requests.

## Infrastructure Retry

RedisScriptExecutor is the basic execution boundary.

RetryingRedisScriptExecutor can retry RedisConnectionException failures according to RedisRetryOptions.

MaxRetryAttempts specifies the number of retries after the initial attempt.

RetryDelay optionally pauses between attempts and observes cancellation.

Only infrastructure-level Redis connection exceptions are retried. Other exceptions are propagated.

## Cancellation

Cancellation is checked before execution and between retry attempts. Retry delays also observe the supplied CancellationToken.

Cancellation is therefore not converted into a successful or failed rate-limit decision.

## Failure Boundary

The Redis layer propagates infrastructure failures to the caller.

Failure policy is intentionally handled above the storage layer. ASP.NET Core middleware decides whether the application should FailOpen or FailClosed.
