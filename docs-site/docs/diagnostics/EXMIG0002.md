# EXMIG0002: MediatR construct requires a manual migration step

| Property | Value |
|----------|-------|
| **Diagnostic ID** | EXMIG0002 |
| **Title** | MediatR construct requires a manual migration step |
| **Category** | Migration |
| **Severity** | Info |
| **Enabled by default** | Yes |
| **Code-fix** | No (manual step) |

## Cause

The analyzer found a MediatR construct that is **outside** the Excalibur.Dispatch compat contract
and has no deterministic mechanical rewrite. The diagnostic surfaces the construct
so the remaining manual migration step is explicit rather than silently skipped.

Constructs that trigger EXMIG0002:

- `IRequestPreProcessor<TRequest>`
- `IRequestPostProcessor<TRequest, TResponse>`
- `IRequestExceptionHandler<TRequest, TResponse, TException>`
- `IRequestExceptionAction<TRequest, TException>`
- `IStreamPipelineBehavior<TRequest, TResponse>`

## Example

```csharp
// Info EXMIG0002: 'OrderValidator' implements MediatR 'IRequestPreProcessor<T>', which is outside
// the Excalibur.Dispatch compat contract and has no automatic rewrite
public class OrderValidator : IRequestPreProcessor<CreateOrder>
{
    public Task Process(CreateOrder request, CancellationToken cancellationToken) => /* ... */;
}
```

## How to Fix

Re-express these constructs as Excalibur.Dispatch middleware (`IDispatchMiddleware`) on the canonical
path. Pre/post processing and exception handling map naturally onto middleware stages, which run
around the handler:

```csharp
public sealed class ValidationMiddleware : IDispatchMiddleware
{
    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate nextDelegate,
        CancellationToken cancellationToken)
    {
        // pre-processing / validation
        var result = await nextDelegate(message, context, cancellationToken);
        // post-processing
        return result;
    }
}
```

See [Migrating from MediatR — unsupported constructs](../migration/from-mediatr.md#unsupported-mediatr-constructs)
and [Pipeline Behaviors](../migration/from-mediatr.md#pipeline-behaviors).

## When to Suppress

Suppress only after you have migrated the construct (or deliberately deferred it) and want to silence
the informational notice.

## See Also

- [Migrating from MediatR](../migration/from-mediatr.md)
- [Custom Middleware](../middleware/custom.md)
