---
sidebar_position: 4
---

# Version Upgrade Guide

:::info Pre-Release
Excalibur has not yet had its initial public release. This page will be updated with upgrade guidance when version transitions occur.
:::

## Overview

Excalibur follows [Semantic Versioning](https://semver.org/):
- **Major version** (X.0.0): Breaking changes, significant API changes
- **Minor version** (0.X.0): New features, backward compatible
- **Patch version** (0.0.X): Bug fixes, backward compatible

## Current Status

The framework is in active development. The current API surface includes:

### Registration

```csharp
// Recommended entry point
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.AddPipeline("default", pipeline => pipeline.UseValidation());
});

// Or simple registration
services.AddDispatch(typeof(Program).Assembly);
```

### Automatic Handler Discovery

`AddHandlersFromAssembly` automatically registers all 9 handler types with the DI container:
- `IDispatchHandler<>`, `IActionHandler<>`, `IActionHandler<,>`
- `IEventHandler<>`, `IDocumentHandler<>`
- `IStreamingDocumentHandler<,>`, `IStreamConsumerHandler<>`
- `IStreamTransformHandler<,>`, `IProgressDocumentHandler<>`

**Customizing Lifetime:**
```csharp
dispatch.AddHandlersFromAssembly(
    typeof(Program).Assembly,
    lifetime: ServiceLifetime.Transient,  // Override default Scoped
    registerWithContainer: false);        // Skip DI registration (advanced)
```

## Upgrade Best Practices

When future versions are released, follow these guidelines:

1. **Test Before Upgrading** - Run your full test suite on the current version
2. **Backup Databases** - Event store, outbox, and saga stores
3. **Read Release Notes** - Check for breaking changes
4. **Upgrade in Staging First** - Validate before production deployment
5. **Plan Rollback** - Always have a rollback strategy

## See Also

- [Migration Overview](index.md) - All migration guides
- [Getting Started](../getting-started/index.md) - New project setup from scratch
- [Introduction](../intro.md) - Dispatch framework overview
