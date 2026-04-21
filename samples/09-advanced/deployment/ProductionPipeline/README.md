# Production Pipeline Sample

Demonstrates the canonical production middleware pipeline for Excalibur.Dispatch.

## Pipeline Order

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseSecurityStack();    // Auth first
    dispatch.UseResilienceStack();  // Resilience wraps everything below
    dispatch.UseValidationStack();  // Validate before business logic
    dispatch.UseTransaction();      // Transaction wraps handler
    dispatch.UseInbox();            // Idempotency inside transaction
    dispatch.UseOutbox();           // Outbox inside transaction
});
```

## Middleware Stacks (Sprint 656, ADR-220)

| Stack | Middleware |
|-------|-----------|
| `UseSecurityStack()` | Authentication, Authorization, TenantIdentity |
| `UseResilienceStack()` | Timeout, Retry, CircuitBreaker |
| `UseValidationStack()` | Validation, ExceptionMapping |

## Why This Order?

1. **Security first** -- reject unauthorized requests before any processing
2. **Resilience wraps everything** -- timeout/retry/circuit breaker protect downstream
3. **Validation before business logic** -- ensure payloads are correct early
4. **Transaction wraps handler + inbox + outbox** -- single atomic unit of work
5. **Inbox inside transaction** -- idempotency check participates in the transaction
6. **Outbox inside transaction** -- integration events committed atomically with state changes

## Configuration Only

This sample demonstrates pipeline configuration. No running database is required.
For a working transactional pipeline, see the `TransactionalHandlers` sample.
