# Rate Limiting Algorithms

RateLimitEngine implements four rate-limiting algorithms. Each algorithm is exposed through the common IRateLimiter contract while retaining its own state model and store abstraction.

## Fixed Window

Fixed Window divides time into discrete windows.

For each key, the store maintains the number of consumed permits in the current window. A request is accepted when the new consumption does not exceed the configured permit limit.

The Redis implementation derives the current window from Redis server time and stores state under a window-specific Redis key. The Lua script performs the read, limit check, increment, and expiration update atomically.

On rejection, RetryAfter corresponds to the remaining lifetime of the current window.

## Token Bucket

Token Bucket models a bucket with a configurable capacity and continuously refilling tokens.

The refill rate is derived from the configured policy rate. Requests consume tokens according to their cost.

The in-memory implementation uses elapsed monotonic time for refill calculations.

The Redis implementation stores token quantity and the last update timestamp. Redis server time is used inside the Lua script so refill calculations are consistent across application instances.

When insufficient tokens are available, RetryAfter represents the estimated time required for the requested cost to become available.

## Sliding Window

Sliding Window tracks accepted requests within a moving time interval rather than a fixed boundary.

Each accepted request is represented by a timestamp and its associated cost. Expired entries are removed before evaluating the next request.

The Redis implementation maintains a sorted set of entries and a total-consumption value. A Lua script removes expired entries, evaluates the request, updates state, and applies expiration atomically.

RetryAfter is calculated from the timestamp at which sufficient capacity is expected to be released.

## GCRA

GCRA (Generic Cell Rate Algorithm) represents the rate using a theoretical arrival time (TAT) rather than storing every request.

The implementation derives an inter-arrival interval from the policy and uses burst tolerance to determine whether a request can be accepted.

The Redis implementation stores TAT and performs the acceptance decision and TAT update in one Lua operation using Redis server time.

GCRA does not expose a classical fixed-window reset boundary. Its decision therefore uses RetryAfter when a request is too early and does not require ResetAfter.

## Cost-Aware Evaluation

All algorithms support request cost.

A cost greater than the configured permit limit is invalid for acceptance and is rejected by the algorithm semantics. Individual algorithms may represent this condition differently in their underlying state calculations.

## Common Decision

All algorithms produce RateLimitDecision through the shared contract.

- Allowed indicates whether the request was accepted.
- Limit represents the configured policy permit limit.
- Remaining represents immediately available capacity according to the algorithm state.
- RetryAfter is populated when a rejected request has a meaningful retry time.
- ResetAfter is populated when the algorithm has a meaningful reset or recovery boundary.

HTTP-specific behavior is not part of the algorithms. ASP.NET Core integration maps decisions to HTTP status codes and headers.
