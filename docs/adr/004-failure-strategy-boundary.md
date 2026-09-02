# ADR-004: Keep Failure Strategy at the ASP.NET Core Boundary

## Status

Accepted

## Context

Rate-limit infrastructure failures do not inherently determine whether an application request should be allowed to continue.

Different applications may prefer availability or strict enforcement when the rate-limit backend is unavailable.

Embedding this decision inside Core or Redis storage would couple infrastructure behavior to application policy.

## Decision

Redis and other storage implementations propagate infrastructure failures to their caller.

ASP.NET Core middleware owns the application-level failure strategy.

The middleware supports:

- `FailOpen`: log the failure and continue the request pipeline.
- `FailClosed`: log the failure and return HTTP 503.

Cancellation remains distinct and is propagated without applying either failure strategy.

## Consequences

Core and storage layers remain reusable across different hosting environments.

Applications explicitly choose their availability versus enforcement behavior.

Failure-policy tests can be performed independently at the ASP.NET Core integration boundary.

Infrastructure components do not need to understand HTTP status codes or middleware behavior.
