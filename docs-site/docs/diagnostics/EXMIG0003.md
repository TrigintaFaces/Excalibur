# EXMIG0003: `using MediatR;` directive is swappable

| Property | Value |
|----------|-------|
| **Diagnostic ID** | EXMIG0003 |
| **Title** | MediatR using directive is swappable to the Excalibur.Dispatch compat namespace |
| **Category** | Migration |
| **Severity** | Info |
| **Enabled by default** | Yes |
| **Code-fix** | Yes |

## Cause

The analyzer found a `using MediatR;` directive. The Excalibur.Dispatch compat surface provides the
same MediatR interface shapes in the `Excalibur.Dispatch.Compat.MediatR` namespace, so the directive
can be mechanically swapped to resolve those shapes against the compat package.

## Example

```diff
- using MediatR;
+ using Excalibur.Dispatch.Compat.MediatR;
```

## How to Fix

Apply the code-fix. It swaps the directive idempotently and does not produce duplicate or orphaned
`using` directives. After referencing the `Excalibur.Dispatch.Compat.MediatR` package, your existing
`IRequest`/`IRequestHandler`/`INotification`/`IPipelineBehavior`/`IStreamRequest` code compiles
against the compat shapes.

See [Migrating from MediatR — swap the namespace](../migration/from-mediatr.md#step-2-swap-the-namespace).

## When to Suppress

Suppress while a file still depends on a MediatR construct that is not part of the compat surface (see
[EXMIG0002](./EXMIG0002.md)) and you want to keep the original directive until that construct is
migrated.

## See Also

- [Migrating from MediatR](../migration/from-mediatr.md)
- [EXMIG0001: registration swap](./EXMIG0001.md)
