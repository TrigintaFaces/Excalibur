# EXMIG0004: Handler signature differs from the compat shape

| Property | Value |
|----------|-------|
| **Diagnostic ID** | EXMIG0004 |
| **Title** | Handler signature differs from the Excalibur.Dispatch compat shape |
| **Category** | Migration |
| **Severity** | Warning |
| **Enabled by default** | Yes |
| **Code-fix** | Yes (deterministic deltas) |

## Cause

A handler implements a compat `IRequestHandler` / `INotificationHandler` but its handler method name
differs from the compat shape's `Handle`. Excalibur.Dispatch compat handlers implement:

```csharp
Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
```

A common delta is a `HandleAsync` method name (the canonical Excalibur convention) used on a handler
that implements the compat interface.

## Example

```csharp
// Warning EXMIG0004: Handler 'CreateOrderHandler' method 'HandleAsync' should be 'Handle'
// to match the Excalibur.Dispatch compat handler shape
public class CreateOrderHandler : IRequestHandler<CreateOrder, OrderResult>
{
    public Task<OrderResult> HandleAsync(CreateOrder request, CancellationToken cancellationToken)
        => /* ... */;
}
```

## How to Fix

For a deterministic delta (a method rename such as `HandleAsync` → `Handle`), apply the code-fix:

```csharp
public class CreateOrderHandler : IRequestHandler<CreateOrder, OrderResult>
{
    public Task<OrderResult> Handle(CreateOrder request, CancellationToken cancellationToken)
        => /* ... */;
}
```

Other signature deltas (parameter shape, return type) have no automatic rewrite and are described in
the diagnostic message for manual migration.

:::note Compat vs canonical handler names
This applies to handlers implementing the **compat** `IRequestHandler` (method `Handle`). If you are
rewriting to the **canonical** `IActionHandler`, that interface uses `HandleAsync` — see the
[canonical-API rewrite](../migration/from-mediatr.md#side-by-side-comparison).
:::

## When to Suppress

Suppress when the handler intentionally implements a non-compat signature you plan to migrate later.

## See Also

- [Migrating from MediatR](../migration/from-mediatr.md)
- [EXMIG0001: registration swap](./EXMIG0001.md)
