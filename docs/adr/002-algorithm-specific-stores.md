# ADR-002: Use Algorithm-Specific Store Contracts

## Status

Accepted

## Context

Rate-limiting algorithms have different state models and atomic transition requirements.

Fixed Window requires window counters, Token Bucket requires token and refill state, Sliding Window requires timestamped entries, and GCRA requires theoretical arrival time.

A single generic mutable store abstraction would either expose algorithm-specific implementation details or hide important semantics behind an overly generic contract.

## Decision

Each algorithm defines and consumes its own store abstraction:

- `IFixedWindowStore`
- `ITokenBucketStore`
- `ISlidingWindowStore`
- `IGcraStore`

Algorithm implementations depend on these abstractions rather than concrete in-memory or Redis stores.

## Consequences

Algorithm semantics remain independent from storage technology.

In-memory and Redis implementations can provide the same algorithm contract.

Store operations can expose the exact state transition required by each algorithm.

Adding a new algorithm requires defining a store contract appropriate to its state model.
