# ASP.NET Core Integration

RateLimitEngine provides ASP.NET Core middleware for applying rate-limit decisions to HTTP requests.

## Middleware Flow

For each request, the middleware:

1. Resolves the configured rate-limit key.
2. Creates a RateLimitRequest.
3. Builds a RateLimitPolicy from the configured options.
4. Evaluates the selected algorithm through the limiter factory.
5. Applies the resulting RateLimitDecision to the HTTP response.

## Configuration

RateLimitOptions controls the HTTP integration.

Important options include:

- Backend
- Algorithm
- FailureStrategy
- PermitLimit
- Window
- Cost

The default backend is InMemory and the default algorithm is FixedWindow.

Configuration values are validated before use, including enum values and positive numeric/time values.

## Response Mapping

Accepted requests continue through the ASP.NET Core request pipeline.

Rejected requests return HTTP 429.

The middleware maps decision metadata to response headers:

- X-RateLimit-Limit
- X-RateLimit-Remaining
- X-RateLimit-Reset-After when ResetAfter is available
- Retry-After when a rejected decision provides RetryAfter

Algorithm implementations remain independent from these HTTP header names and status codes.

## Failure Handling

Infrastructure failures are handled according to FailureStrategy.

### FailOpen

The middleware logs the evaluation error and continues the request pipeline.

### FailClosed

The middleware logs the evaluation error and returns HTTP 503.

OperationCanceledException is rethrown so request cancellation retains its normal ASP.NET Core semantics.

## Logging

ASP.NET Core integration emits structured logging around rate-limit evaluation failures.

Logging is kept at the integration boundary so Core algorithms do not depend on ASP.NET Core logging infrastructure.

## Backend Selection

The middleware can use the configured in-memory or Redis-backed engine.

The Redis backend receives its state through the Redis-specific dependency-injection registration and store abstractions.

## Separation of Concerns

The middleware is responsible for HTTP concerns.

The algorithms are responsible for rate-limit semantics.

Stores are responsible for state management.

Observability is layered through the instrumented limiter and standard .NET diagnostics APIs.
