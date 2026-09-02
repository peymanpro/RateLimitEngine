# ADR-001: Use Redis Server Time and Lua for Distributed Decisions

## Status

Accepted

## Context

Distributed rate limiting requires a consistent time source and an atomic state transition.

Using application-local clocks can introduce differences between application instances. Performing Redis reads and writes as separate commands can allow concurrent requests to observe inconsistent intermediate state.

## Decision

Redis-backed stores obtain time using Redis TIME inside Lua scripts.

Each rate-limit state transition is implemented as a single Redis Lua operation that performs the required read, decision, mutation, and expiration work atomically.

## Consequences

Distributed instances share the same authoritative time source for Redis decisions.

Concurrent requests cannot interleave the individual state-transition commands.

The Redis infrastructure becomes responsible for the distributed time and atomicity guarantees.

Lua scripts are part of the Redis implementation rather than the Core API.
