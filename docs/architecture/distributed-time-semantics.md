# Distributed Time Semantics

Rate limiting algorithms depend on time, but distributed implementations must not rely on independently running application clocks when correctness depends on precise timing.

## Local Implementations

In-memory algorithms may use the application's monotonic time abstraction for elapsed-time calculations.

Wall-clock time may be exposed separately for reporting and HTTP integration.

## Distributed Implementations

Redis-backed state transitions must use an authoritative time source associated with the distributed state operation.

The Redis implementation is responsible for obtaining and using a consistent time source for atomic state transitions.

Application-local clock values must not be treated as authoritative for distributed rate limiting decisions.

## Consequence

The store contracts intentionally avoid passing DateTimeOffset now for distributed operations.

This allows the Redis implementation to perform:

1. Time acquisition
2. State evaluation
3. State mutation
4. Expiration/update

as one atomic operation.

The exact Redis mechanism, including server time and Lua-based atomic execution, belongs to the infrastructure layer rather than the Core contract.
