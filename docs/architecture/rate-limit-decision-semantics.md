# RateLimitDecision Semantics

RateLimitDecision is the common result contract shared by all rate limiting algorithms.

## Limit

Limit represents the configured permit limit from the applicable RateLimitPolicy.

It is the policy-level rate, not necessarily the current storage capacity.

## Remaining

Remaining represents the number of permits that can be accepted immediately according to the current algorithm state.

Remaining may be greater than Limit for algorithms that support burst capacity.

## RetryAfter

RetryAfter is optional.

It represents the minimum duration the caller should wait before retrying when the current request cannot be accepted.

It is only populated when the algorithm can determine a meaningful retry time.

## ResetAfter

ResetAfter is optional.

It represents the duration until the algorithm reaches a meaningful reset or recovery point.

Not every algorithm has a classical reset boundary. For such algorithms, ResetAfter may be null.

## HTTP Mapping

The ASP.NET Core integration is responsible for mapping these values to HTTP headers.

The algorithm implementations must not depend on HTTP header semantics.
