# Resilience and Failure Handling

RateLimitEngine treats rate-limit infrastructure failures separately from normal allow/reject decisions.

## Redis Failures

Redis-backed evaluation may fail because of connection failures, timeouts, unavailable infrastructure, script failures, or cancellation.

Infrastructure failures are propagated from the Redis layer rather than being silently converted into rate-limit decisions.

## Retry Policy

RedisRetryOptions controls infrastructure retry behavior.

- MaxRetryAttempts controls the number of retries after the initial attempt.
- RetryDelay controls the optional delay between attempts.

RetryingRedisScriptExecutor currently retries RedisConnectionException only. Other exceptions are propagated immediately.

Every retry attempt is observable through the Redis retry metric.

## Cancellation

Cancellation is checked before Redis execution and before each retry attempt.

Retry delays use the supplied CancellationToken.

OperationCanceledException is propagated and is not treated as an infrastructure failure for the ASP.NET Core failure strategy.

## ASP.NET Core Failure Strategies

ASP.NET Core integration exposes two explicit strategies.

### FailOpen

When rate-limit evaluation fails, the middleware logs the failure and continues the request pipeline.

This prioritizes application availability when the rate-limit infrastructure is unavailable.

### FailClosed

When rate-limit evaluation fails, the middleware logs the failure and returns HTTP 503.

This prioritizes enforcement behavior over application availability when the rate-limit infrastructure cannot be trusted.

## Recovery

Recovery does not require a separate circuit-breaker state in the rate limiter.

When Redis becomes available again, subsequent evaluations proceed through the normal Redis execution path.

Retry behavior is therefore an infrastructure concern, while FailOpen and FailClosed are application-integration concerns.

## Design Principle

The storage layer does not decide whether an application request should fail open or fail closed.

This separation keeps infrastructure behavior reusable and makes the application-level availability policy explicit at the ASP.NET Core boundary.
