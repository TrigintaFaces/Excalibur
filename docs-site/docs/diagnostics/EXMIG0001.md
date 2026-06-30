# EXMIG0001: MediatR registration is portable to Excalibur.Dispatch

| Property | Value |
|----------|-------|
| **Diagnostic ID** | EXMIG0001 |
| **Title** | MediatR registration is portable to Excalibur.Dispatch |
| **Category** | Migration |
| **Severity** | Info |
| **Enabled by default** | Yes |
| **Code-fix** | Yes |

## Cause

The analyzer found a MediatR dependency-injection registration call (`services.AddMediatR(...)`).
This call maps directly onto the Excalibur.Dispatch compat registration entry point
`AddMediatRCompat(...)` as part of migrating off the now-commercial MediatR package.

Detection is syntax-based on the invoked method name, so the diagnostic fires whether or not the
MediatR assembly is still referenced — the realistic state of code mid-migration.

## Example

The following code triggers EXMIG0001:

```csharp
// Info EXMIG0001: 'AddMediatR' can be mechanically migrated to 'AddMediatRCompat(...)'
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
```

## How to Fix

Apply the code-fix, or edit manually. The rewrite preserves the assembly-scan arguments:

```csharp
builder.Services.AddMediatRCompat(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
```

`AddMediatRCompat` self-bootstraps the Dispatch core (`AddDispatch()`, idempotent) and validates its
options at startup. See [Migrating from MediatR — drop-in shim](../migration/from-mediatr.md#drop-in-compatibility-shim).

## When to Suppress

Suppress while both frameworks intentionally run side by side and you are not yet ready to migrate a
given registration:

```csharp
#pragma warning disable EXMIG0001
builder.Services.AddMediatR(/* ... */);
#pragma warning restore EXMIG0001
```

## See Also

- [Migrating from MediatR](../migration/from-mediatr.md)
- [EXMIG0003: using directive swap](./EXMIG0003.md)
- [EXMIG0004: handler signature](./EXMIG0004.md)
